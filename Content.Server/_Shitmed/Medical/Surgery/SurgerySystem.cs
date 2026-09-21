// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Maths.FixedPoint; // Arcane
using Content.Server.Administration.Logs; // Arcane
using Content.Server.Body.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage;
using Content.Shared.Database; // Arcane
using Content.Shared._Shitmed.Targeting;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Effects.Complete; // Arcane
using Content.Shared._Shitmed.Medical.Surgery.Effects.Step;
using Content.Shared._Shitmed.Medical.Surgery.Steps; // Arcane
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components; // Arcane
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Shitmed.Medical.Surgery;

public sealed class SurgerySystem : SharedSurgerySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!; // Arcane
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly WoundSystem _wounds = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryTargetComponent, SurgeryStepDamageEvent>(OnSurgeryStepDamage);
        // You might be wondering "why aren't we using StepEvent for these two?" reason being that StepEvent fires off regardless of success on the previous functions
        // so this would heal entities even if you had a used or incorrect organ.
        SubscribeLocalEvent<SurgeryDamageChangeEffectComponent, SurgeryStepDamageChangeEvent>(OnSurgeryDamageChange);
        SubscribeLocalEvent<SurgeryStepEmoteEffectComponent, SurgeryStepEvent>(OnStepScreamComplete);
        SubscribeLocalEvent<SurgeryStepSpawnEffectComponent, SurgeryStepEvent>(OnStepSpawnComplete);
        // Arcane-Start
        SubscribeLocalEvent<SurgeryStepEvent>(OnSurgeryStepTrace);
        SubscribeLocalEvent<SurgeryStepFailedEvent>(OnSurgeryStepFailedTrace);
        SubscribeLocalEvent<SurgeryStepCompleteCheckEvent>(OnSurgeryStepCheckTrace);
        SubscribeLocalEvent<SurgeryCompletedEvent>(OnSurgeryCompletedTrace);
        // Arcane-End
    }

    protected override void RefreshUI(EntityUid body)
    {
        if (!_ui.IsUiOpen(body, SurgeryUIKey.Key))
            return;

        var surgeries = new Dictionary<NetEntity, List<EntProtoId>>();
        foreach (var part in _body.GetBodyChildren(body))
        {
            var valid = new List<EntProtoId>();
            foreach (var surgery in AllSurgeries)
            {
                if (GetSingleton(surgery) is not { } surgeryEnt)
                    continue;

                var ev = new SurgeryValidEvent(body, part.Id);
                RaiseLocalEvent(surgeryEnt, ref ev);

                if (ev.Cancelled)
                    continue;

                valid.Add(surgery);
            }
            surgeries[GetNetEntity(part.Id)] = valid;
        }

        _ui.SetUiState(body, SurgeryUIKey.Key, new SurgeryBuiState(surgeries));
        /*
            Reason we do this is because when applying a BUI State, it rolls back the state on the entity temporarily,
            which just so happens to occur right as we're checking for step completion, so we end up with the UI
            not updating at all until you change tools or reopen the window. I love shitcode.
        */
        _ui.ServerSendUiMessage(body, SurgeryUIKey.Key, new SurgeryBuiRefreshMessage());
    }

    private DamageSpecifier? SetDamage(EntityUid body, // Arcane-Edit
        DamageSpecifier damage,
        float partMultiplier,
        EntityUid user,
        EntityUid part,
        bool affectAll = false,
        bool ignoreBlockers = false) // Arcane
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp))
            return null; // Arcane-Edit

        return _damageable.TryChangeDamage(body, // Arcane-Edit
            damage,
            true,
            origin: user,
            partMultiplier: partMultiplier,
            targetPart: affectAll ? TargetBodyPart.All : _body.GetTargetBodyPart(partComp),
            ignoreBlockers: ignoreBlockers); // Arcane
    }

    // Arcane-Edit-Start
    private void OnSurgeryStepDamage(Entity<SurgeryTargetComponent> ent, ref SurgeryStepDamageEvent args)
    {
        FixedPoint2? healable = TryComp<SurgeryWoundedConditionComponent>(args.Surgery, out var wounded)
            ? _wounds.GetWoundableSeverityPoint(args.Part, damageGroup: wounded.DamageGroup, healable: true, ignoreBlockers: true) // Arcane-Edit
            : null;
        FixedPoint2? total = TryComp<SurgeryWoundedConditionComponent>(args.Surgery, out wounded)
            ? _wounds.GetWoundableSeverityPoint(args.Part, damageGroup: wounded.DamageGroup, healable: false)
            : null;
        LogSurgeryTrace($"DAMAGE_REQUEST body={ToPrettyString(args.Body)} part={ToPrettyString(args.Part)} " +
                        $"surgery={ToPrettyString(args.Surgery)} requested={FormatDamage(args.Damage)} " +
                        $"ignoreBlockers={args.IgnoreBlockers} healable={healable?.ToString() ?? "n/a"} " +
                        $"total={total?.ToString() ?? "n/a"} before={GetRemainingDamage(args.Part, args.Surgery)}");

        var damageChanged = SetDamage(args.Body, args.Damage, args.PartMultiplier, args.User, args.Part, ignoreBlockers: args.IgnoreBlockers);
        LogTendWounds(args.User, args.Body, args.Part, args.Surgery, damageChanged);

        LogSurgeryTrace($"DAMAGE_RESULT body={ToPrettyString(args.Body)} part={ToPrettyString(args.Part)} " +
                        $"applied={FormatDamage(damageChanged)} appliedNone={damageChanged == null || damageChanged.Empty} " +
                        $"after={GetRemainingDamage(args.Part, args.Surgery)}");
    }
    // Arcane-Edit-End

    private void OnSurgeryDamageChange(Entity<SurgeryDamageChangeEffectComponent> ent, ref SurgeryStepDamageChangeEvent args)
    {
        var damageChange = ent.Comp.Damage;
        if (Status.HasEffectComp<ForcedSleepingStatusEffectComponent>(args.Body))
            damageChange = damageChange * ent.Comp.SleepModifier;

        SetDamage(args.Body, damageChange, 0.5f, args.User, args.Part, ent.Comp.AffectAll);
    }
    private void OnStepScreamComplete(Entity<SurgeryStepEmoteEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (Status.HasEffectComp<ForcedSleepingStatusEffectComponent>(args.Body))
            return;

        _chat.TryEmoteWithChat(args.Body, ent.Comp.Emote);
    }
    private void OnStepSpawnComplete(Entity<SurgeryStepSpawnEffectComponent> ent, ref SurgeryStepEvent args) =>
        SpawnAtPosition(ent.Comp.Entity, Transform(args.Body).Coordinates);

    // Arcane-Start
    private void OnSurgeryStepTrace(ref SurgeryStepEvent args)
    {
        LogSurgeryTrace($"STEP user={ToPrettyString(args.User)} body={ToPrettyString(args.Body)} " +
                        $"part={ToPrettyString(args.Part)} surgery={ToPrettyString(args.Surgery)} " +
                        $"step={ToPrettyString(args.Step)} complete={args.Complete} remaining={GetRemainingDamage(args.Part, args.Surgery)}");
    }

    private void OnSurgeryStepFailedTrace(ref SurgeryStepFailedEvent args)
    {
        LogSurgeryTrace($"STEP_FAILED user={ToPrettyString(args.User)} body={ToPrettyString(args.Body)} " +
                        $"surgery={args.SurgeryId} step={args.StepId}");
    }

    private void OnSurgeryStepCheckTrace(ref SurgeryStepCompleteCheckEvent args)
    {
        LogSurgeryTrace($"STEP_CHECK body={ToPrettyString(args.Body)} part={ToPrettyString(args.Part)} " +
                        $"surgery={ToPrettyString(args.Surgery)} cancelled={args.Cancelled} " +
                        $"remaining={GetRemainingDamage(args.Part, args.Surgery)}");
    }

    private void OnSurgeryCompletedTrace(ref SurgeryCompletedEvent args)
    {
        LogSurgeryTrace("SURGERY_COMPLETED");
    }

    #region Logging
    private const string SurgeryTraceMarker = "[SURGERY_TRACE]";

    private void LogSurgeryTrace(string message)
    {
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{SurgeryTraceMarker} {message}");
    }

    private string GetRemainingDamage(EntityUid part, EntityUid surgery)
    {
        if (!TryComp<SurgeryWoundedConditionComponent>(surgery, out var wounded))
            return "n/a";

        return _wounds.GetWoundableIntegrityDamage(part, damageGroup: wounded.DamageGroup, healable: false).ToString();
    }

    private string GetBodyRemainingDamage(EntityUid body)
    {
        var remaining = new List<string>();
        foreach (var child in _body.GetBodyChildren(body))
        {
            if (!TryComp<WoundableComponent>(child.Id, out _))
                continue;

            remaining.Add($"{ToPrettyString(child.Id)}:{_wounds.GetWoundableIntegrityDamage(child.Id, healable: false)}");
        }

        return remaining.Count == 0 ? "none" : string.Join(",", remaining);
    }

    private static string FormatDamage(DamageSpecifier? damage)
    {
        if (damage == null)
            return "none";

        return string.Join(",", damage.DamageDict.Select(x => $"{x.Key}:{x.Value}"));
    }

    private void LogTendWounds(EntityUid user, EntityUid body, EntityUid part, EntityUid surgery, DamageSpecifier? damageChanged)
    {
        if (damageChanged == null || damageChanged.GetTotal() >= 0)
            return;

        var healed = -damageChanged.GetTotal();
        var damageGroup = TryComp<SurgeryWoundedConditionComponent>(surgery, out var woundedComp)
            ? woundedComp.DamageGroup.Id
            : "wound";

        if (user != body)
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(user):user} surgically tended {damageGroup} wounds on {ToPrettyString(part):part} of {ToPrettyString(body):target} for {healed:damage} damage");
        }
        else
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(user):user} surgically tended {damageGroup} wounds on their own {ToPrettyString(part):part} for {healed:damage} damage");
        }
    }

    protected override void LogOrganHealed(EntityUid user, EntityUid body, EntityUid part, EntityUid organ, FixedPoint2 healed)
    {
        if (user != body)
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(user):user} surgically healed organ {ToPrettyString(organ):organ} on {ToPrettyString(part):part} of {ToPrettyString(body):target} for {healed:damage} integrity");
        }
        else
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(user):user} surgically healed organ {ToPrettyString(organ):organ} on their own {ToPrettyString(part):part} for {healed:damage} integrity");
        }
    }

    protected override void LogBoneMended(EntityUid user, EntityUid body, EntityUid part, EntityUid bone, FixedPoint2 healed)
    {
        if (user != body)
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(user):user} surgically mended bone {ToPrettyString(bone):bone} on {ToPrettyString(part):part} of {ToPrettyString(body):target} for {healed:damage} integrity");
        }
        else
        {
            _adminLogger.Add(LogType.Healed,
                $"{ToPrettyString(user):user} surgically mended bone {ToPrettyString(bone):bone} on their own {ToPrettyString(part):part} for {healed:damage} integrity");
        }
    }
    #endregion
    // Arcane-End
}
