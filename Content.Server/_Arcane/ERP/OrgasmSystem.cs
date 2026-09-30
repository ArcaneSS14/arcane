using System.Numerics;
using Content.Goobstation.Common.Effects;
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Forensics;
using Content.Shared.Interaction;
using Content.Shared._Arcane.ERP;
using Content.Shared.Chat;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Dataset;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.ERP;

public sealed class OrgasmSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ArousalSystem _arousal = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly CondomSystem _condom = default!;
    [Dependency] private readonly ForensicsSystem _forensics = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SparksSystem _sparks = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly EntProtoId HeartsProto = "EffectHearts";
    private static readonly EntProtoId CumWallProto = "EffectCumWall";
    private static readonly EntProtoId SemenPuddleProto = "PuddleSemen";
    private static readonly EntProtoId FemCumPuddleProto = "PuddleFemCum";
    private const float EjaculationEffectDistance = 0.6f;
    private const float EjaculationTargetDistance = 1.4f;
    private const float EjaculationBlockedDistance = 0.1f;
    private const float EjaculationWallCheckExtraRange = 0.1f;
    private const float EjaculationForwardDot = 0.6f;
    private static readonly ProtoId<LocalizedDatasetPrototype> OrgasmMessagesDataset = "OrgasmMessages";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArousalComponent, ArousalOrgasmEvent>(OnOrgasm);
    }

    private void OnOrgasm(Entity<ArousalComponent> ent, ref ArousalOrgasmEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        DoOrgasmEffects(ent, humanoid);
    }

    public void DoOrgasmEffects(EntityUid uid, HumanoidAppearanceComponent? humanoid = null)
    {
        Resolve(uid, ref humanoid, false);

        Spawn(HeartsProto, _transform.GetMapCoordinates(uid));
        PlayOrgasmSound(uid, humanoid?.Gender ?? Gender.Female);

        _arousal.NotifyMoan(uid);

        if (_prototype.TryIndex(OrgasmMessagesDataset, out var dataset))
            _chat.TrySendInGameICMessage(uid, Loc.GetString(_random.Pick(dataset.Values)), InGameICChatType.Emote, false, checkEmote: false);

        _popup.PopupEntity(Loc.GetString("orgasm-popup-self"), uid, uid, PopupType.MediumCaution);

        if (humanoid != null)
            SpawnEjaculation(uid, humanoid.Sex);

        var weakness = EnsureComp<OrgasmWeaknessComponent>(uid);
        weakness.ExpiresAt = _timing.CurTime + weakness.WeaknessDuration;
        Dirty(uid, weakness);
    }

    // TODO: move overlay logic to Content.Shared for prediction
    private void SpawnEjaculation(EntityUid uid, Sex sex)
    {
        if (sex is Sex.Unsexed)
            return;

        if (HasComp<SparkEjaculationComponent>(uid))
        {
            _sparks.DoSparks(Transform(uid).Coordinates, playSound: true);
            return;
        }

        if (_condom.TryFill(uid))
            return;

        var xform = Transform(uid);
        var (sourcePos, sourceRot) = _transform.GetWorldPositionRotation(xform);
        var sourceMap = _transform.ToMapCoordinates(xform.Coordinates);
        var forward = sourceRot.ToWorldVec();

        SpawnPuddle(uid, sourcePos, sourceMap, forward, sex);

        foreach (var target in _lookup.GetEntitiesInRange<HumanoidAppearanceComponent>(sourceMap, EjaculationTargetDistance))
        {
            if (target.Owner == uid)
                continue;

            var toTarget = _transform.GetWorldPosition(target.Owner) - sourcePos;
            if (toTarget == Vector2.Zero || Vector2.Dot(toTarget.Normalized(), forward) < EjaculationForwardDot)
                continue;

            if (!_interaction.InRangeUnobstructed(uid, target.Owner, EjaculationTargetDistance))
                continue;

            AddCumOverlay(target.Owner);
        }
    }

    private void SpawnPuddle(EntityUid uid, Vector2 sourcePos, MapCoordinates sourceMap, Vector2 forward, Sex sex)
    {
        var puddleProto = sex is Sex.Female ? FemCumPuddleProto : SemenPuddleProto;

        var forwardMap = new MapCoordinates(sourcePos + forward * EjaculationEffectDistance, sourceMap.MapId);

        var wallBlocked = !_interaction.InRangeUnobstructed(uid, forwardMap, EjaculationEffectDistance + EjaculationWallCheckExtraRange);
        var coords = wallBlocked
            ? new MapCoordinates(sourcePos + forward * EjaculationBlockedDistance, sourceMap.MapId)
            : forwardMap;

        if (wallBlocked)
            Spawn(CumWallProto, forwardMap);

        var puddle = Spawn(puddleProto, coords);
        _forensics.TransferDna(puddle, uid, false);

        var dnaData = _bloodstream.GetEntityBloodData(uid);
        if (dnaData.Count > 0 && _solutionContainer.TryGetSolution(puddle, "puddle", out _, out var puddleSolution))
        {
            foreach (var reagent in puddleSolution.Contents)
            {
                reagent.Reagent.EnsureReagentData().AddRange(dnaData);
            }
        }
    }

    public void AddCumOverlay(EntityUid uid)
    {
        var overlay = EnsureComp<CumOverlayComponent>(uid);
        overlay.Count++;
        Dirty(uid, overlay);
    }

    private void PlayOrgasmSound(EntityUid uid, Gender gender)
    {
        var collection = ErpAudio.OrgasmSounds.GetValueOrDefault(gender, ErpAudio.OrgasmSounds[Gender.Female]);

        var audioParams = new AudioParams
        {
            Variation = 0.1f,
            MaxDistance = 6f,
            Volume = 3f,
        };

        _audio.PlayPvs(new SoundCollectionSpecifier(collection), uid, audioParams);
    }
}
