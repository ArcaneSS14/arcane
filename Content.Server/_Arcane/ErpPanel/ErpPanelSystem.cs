using Content.Server.Chat.Systems;
using Content.Server.Interaction;
using Content.Shared._Arcane.ERP;
using Content.Shared._Arcane.ErpPanel;
using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Arcane.ErpPanel;

public sealed partial class ErpPanelSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _ticking = default!;
    [Dependency] private readonly ArousalSystem _arousal = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly EntProtoId _heartsProto = new("EffectHearts");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ErpPanelOwnerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ErpPanelOwnerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);

        SubscribeLocalEvent<ErpPanelOwnerComponent, BoundUIClosedEvent>(OnBoundUIClosedEvent);

        Subs.BuiEvents<ErpPanelOwnerComponent>(ErpPanelKey.Key, subs =>
        {
            subs.Event<ErpPanelSendMessage>(OnSendMessage);
        });
    }

    private void OnMapInit(Entity<ErpPanelOwnerComponent> entity, ref MapInitEvent args)
    {
        var interfaceData = new InterfaceData(
            clientType: "Content.Client._Arcane.ErpPanel.ErpPanelWindowBUI"
        );

        _ui.SetUi(entity.Owner, ErpPanelKey.Key, interfaceData);
    }

    private void OnGetVerbs(EntityUid uid, ErpPanelOwnerComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!component.Enabled)
            return;

        if (!TryComp<ErpPanelOwnerComponent>(args.User, out var userPanel))
            return;

        if (!userPanel.Enabled)
            return;

        if (!IsValidUI(args.User, args.Target))
            return;

        AlternativeVerb verb = new()
        {
            Act = () => {
                TryOpenPanel(args.User, args.Target);
                userPanel.Target = args.Target;
            },
            Text = Loc.GetString("erp-panel-open-verb"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/_Arcane/Interface/heartIcon.png")),
            Disabled = !_interaction.InRangeAndAccessible(args.User, args.Target),
            Priority = 2
        };

        args.Verbs.Add(verb);
    }

    private void OnBoundUIClosedEvent(Entity<ErpPanelOwnerComponent> entity, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is not ErpPanelKey.Key)
            return;

        entity.Comp.Target = null;
    }

    private void OnSendMessage(Entity<ErpPanelOwnerComponent> entity, ref ErpPanelSendMessage args)
    {
        var user = args.Actor;
        var target = entity.Comp.Target;
        if (target == null || user != entity.Owner)
            return;

        ProccessInteraction(user, target.Value, args.Interaction, args.CustomArousal, args.CustomMoaning);
    }

    public void ProccessInteraction(EntityUid user, EntityUid target, string interactionId, float customArousal, float customMoaning)
    {
        if (!_prototype.TryIndex<PanelInteractionPrototype>(interactionId, out var interaction))
            return;

        if (!IsValidInteraction(user, target, interaction))
            return;

        if (!TryComp<ErpPanelOwnerComponent>(user, out var userPanel))
            return;

        if (!CheckRequirements(user, target, interaction))
            return;

        customArousal = Math.Clamp(customArousal, 0, 300);
        customMoaning = Math.Clamp(customMoaning, 0, 300);

        if (interaction.TargetArouse > 0 && _arousal.CanAddArousal(target))
        {
            Spawn(_heartsProto, _transform.GetMapCoordinates(target));
            _arousal.AddArousal(target, interaction.TargetArouse * customArousal / 100);
            ProccessMoan(target, customMoaning);
        }

        userPanel.Cooldowns[interaction.ID] = _ticking.CurTime;
        Dirty(user, userPanel);

        ProccessMessages(user, target, interaction);
        ProccessSounds(user, interaction);

        if (user == target)
            return;

        if (interaction.UserArouse > 0)
        {
            if (_arousal.CanAddArousal(user))
            {
                _arousal.AddArousal(user, interaction.UserArouse * customArousal / 100);
                ProccessMoan(user, customMoaning);
            }
        }

    }

    private void ProccessMoan(EntityUid uid, float customMoaning)
    {
        if (!TryComp<ArousalComponent>(uid, out var userArousal) || userArousal.LastValue <= 0 || userArousal.MaxArousal == 0)
            return;

        if (!TryComp<HumanoidAppearanceComponent>(uid, out var userHumanoid))
            return;

        var userMoanChance = userArousal.LastValue / userArousal.MaxArousal * customMoaning / 100f;
        userMoanChance = Math.Clamp(userMoanChance, 0f, 1f);

        if (_random.Prob(userMoanChance))
            MoanWithGender(uid, userHumanoid.Gender, userArousal.LastValue / userArousal.MaxArousal);
    }

    private void MoanWithGender(EntityUid uid, Gender userHumanoid, float arousalPercent)
    {
        var collection = ErpAudio.MoanSounds.GetValueOrDefault(userHumanoid, ErpAudio.MoanSounds[Gender.Female]);

        if (!_prototype.TryIndex(collection, out var soundCollection))
            return;

        if (soundCollection.PickFiles.Count == 0)
            return;

        arousalPercent = Math.Clamp(arousalPercent, 0f, 1f);

        var index = (int) Math.Ceiling(arousalPercent * soundCollection.PickFiles.Count) - 1; // WTF??????
        index += _random.Next(-2, 1);
        index = Math.Clamp(index, 0, soundCollection.PickFiles.Count - 1);

        var audioParams = new AudioParams()
        {
            Variation = 0.125f,
            MaxDistance = 4f,
        };

        _audio.PlayPvs(new ResolvedCollectionSpecifier(collection, index), uid, audioParams);

        _chat.TrySendInGameICMessage(uid, Loc.GetString("moan-message"), InGameICChatType.Emote, true);
    }

    private void ProccessMessages(EntityUid user, EntityUid target, PanelInteractionPrototype interaction)
    {
        var messagesCollection = user == target ? interaction.SelfMessages : interaction.Messages;

        var message = _random.Pick(messagesCollection)
            .Replace("$target", Identity.Name(target, EntityManager, user));

        _chat.TrySendInGameICMessage(user, message, InGameICChatType.Emote, false);
    }

    private void ProccessSounds(EntityUid user, PanelInteractionPrototype interaction)
    {
        if (interaction.Sounds.Count == 0)
            return;

        var resSound = _random.Pick(interaction.Sounds);
        var sound = new SoundPathSpecifier(resSound);
        _audio.PlayPvs(sound, user);
    }

    private void TryOpenPanel(EntityUid user, EntityUid target)
    {
        if (!IsValidUI(user, target))
            return;

        _ui.TryOpenUi(user, ErpPanelKey.Key, user);

        var state = new ErpPanelBuiState(GetNetEntity(user), GetNetEntity(target));
        _ui.SetUiState(user, ErpPanelKey.Key, state);
    }

    private bool IsValidUI(EntityUid user, EntityUid target)
    {
        if (!TryComp<ErpPanelOwnerComponent>(user, out var userPanel) || !userPanel.Enabled)
            return false;

        if (!TryComp<ErpPanelOwnerComponent>(target, out var targetPanel) || !targetPanel.Enabled)
            return false;

        if (!HasComp<ArousalComponent>(user) || !HasComp<ArousalComponent>(target))
            return false;

        return true;
    }

    private bool IsValidInteraction(EntityUid user, EntityUid target, PanelInteractionPrototype interaction)
    {
        if (!_interaction.InRangeAndAccessible(user, target, interaction.Range))
            return false;

        if (interaction.Messages.Count == 0)
            return false;

        if (user == target && interaction.SelfMessages.Count == 0 || interaction.Messages.Count == 0)
            return false;

        if (!TryComp<ErpPanelOwnerComponent>(user, out var userPanel) || !userPanel.Enabled)
            return false;

        if (!TryComp<ErpPanelOwnerComponent>(target, out var targetPanel) || !targetPanel.Enabled)
            return false;

        if (!HasComp<ArousalComponent>(user) || !HasComp<ArousalComponent>(target))
            return false;

        if (IsErpInteraction(interaction) && !CanUseErp(user, target))
            return false;

        if (userPanel.Cooldowns.TryGetValue(interaction.ID, out var lastUse) && lastUse + interaction.Cooldown > _ticking.CurTime)
            return false;

        return true;
    }

    private bool IsErpInteraction(PanelInteractionPrototype interaction)
    {
        return interaction.Tags.Contains(ErpPanelConstants.ErpInteractionTag);
    }

    private bool CanUseErp(EntityUid user, EntityUid target)
    {
        if (TryComp<ErpStatusComponent>(user, out var userStatus) && userStatus.Preference == ErpPreference.No)
            return false;

        if (user != target &&
            TryComp<ErpStatusComponent>(target, out var targetStatus) &&
            targetStatus.Preference == ErpPreference.No)
            return false;

        return true;
    }

    private bool CheckRequirements(EntityUid user, EntityUid target, PanelInteractionPrototype interaction)
    {
        var passed = true;

        if (interaction.UserRequirements != null)
        {
            foreach (var requirement in interaction.UserRequirements)
            {
                if (!requirement.IsAvailable(user, EntityManager))
                    passed = false;
            }
        }

        if (interaction.TargetRequirements != null)
        {
            foreach (var requirement in interaction.TargetRequirements)
            {
                if (!requirement.IsAvailable(target, EntityManager))
                    passed = false;
            }
        }

        return passed;
    }
}
