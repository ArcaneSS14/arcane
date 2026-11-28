using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Shared._Arcane.ERP;
using Content.Shared._Arcane.Silicons;
using Content.Shared._Arcane.ErpPanel;
using Content.Shared.Actions;
using Content.Shared.Humanoid;
using Content.Shared.InteractionVerbs;
using Content.Shared.Mind.Components;
using Content.Shared.Preferences;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Tag;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.Silicons;

public sealed class SiliconInteractableSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly IServerPreferencesManager _preferencesManager = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    private static readonly EntProtoId ModestyActionId = "ActionToggleSiliconModesty";

    private static readonly string[] GenderTags =
    {
        "MobGenderMale",
        "MobGenderFemale",
        "MobGenderFutanari",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconInteractableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SiliconInteractableComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<SiliconInteractableComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SiliconInteractableComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnMapInit(Entity<SiliconInteractableComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<ErpStatusComponent>(ent.Owner);
        EnsureComp<ArousalComponent>(ent.Owner);
        EnsureComp<ErpPanelOwnerComponent>(ent.Owner);
        EnsureComp<ErpAllowedComponent>(ent.Owner);
        EnsureComp<OwnInteractionVerbsComponent>(ent.Owner);
        EnsureComp<SiliconModestyComponent>(ent.Owner);
        _actions.AddAction(ent.Owner, ModestyActionId);
    }

    private void OnComponentStartup(Entity<SiliconInteractableComponent> ent, ref ComponentStartup args)
    {
        TryApplyPlayerSettings(ent.Owner);
    }

    private void OnPlayerAttached(Entity<SiliconInteractableComponent> ent, ref PlayerAttachedEvent args)
    {
        TryApplyPlayerSettings(ent.Owner);
    }

    private void OnMindAdded(Entity<SiliconInteractableComponent> ent, ref MindAddedMessage args)
    {
        TryApplyPlayerSettings(ent.Owner);
    }

    private void TryApplyPlayerSettings(EntityUid uid)
    {
        if (!HasComp<BorgChassisComponent>(uid))
            return;

        if (!_mindSystem.TryGetMind(uid, out _, out var mind) || mind.UserId == null)
            return;

        if (!_preferencesManager.TryGetCachedPreferences(mind.UserId.Value, out var prefs))
            return;

        if (prefs.SelectedCharacter is not HumanoidCharacterProfile profile)
            return;

        ApplyGenderTag(uid, profile.Sex);

        var preference = profile.ErpPreference;
        var erpStatus = EnsureComp<ErpStatusComponent>(uid);

        var oldPreference = erpStatus.Preference;
        erpStatus.Preference = preference;
        Dirty(uid, erpStatus);

        if (oldPreference != preference)
            RaiseLocalEvent(uid, new ErpPreferenceChangedEvent(oldPreference, preference));
    }

    private void ApplyGenderTag(EntityUid uid, Sex sex)
    {
        string genderTag = sex switch
        {
            Sex.Male => "MobGenderMale",
            Sex.Female => "MobGenderFemale",
            Sex.Futanari => "MobGenderFutanari",
            _ => "MobGenderMale",
        };

        foreach (var tag in GenderTags)
        {
            _tagSystem.RemoveTag(uid, tag);
        }

        _tagSystem.AddTag(uid, genderTag);
    }
}
