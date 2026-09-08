// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.DoAfter;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Weapons.Melee.Events;
using Content.Shared._Shitmed.Weapons.Ranged.Events;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    private void InitBones()
    {
        SubscribeLocalEvent<BoneComponent, BoneSeverityChangedEvent>(OnBoneSeverityChanged);
        SubscribeLocalEvent<BoneComponent, BoneIntegrityChangedEvent>(OnBoneIntegrityChanged);
        SubscribeLocalEvent<BoneComponent, GetDoAfterDelayMultiplierEvent>(OnGetDoAfterDelayMultiplier);
        SubscribeLocalEvent<BoneComponent, AttemptHandsMeleeEvent>(OnAttemptHandsMelee);
        SubscribeLocalEvent<BoneComponent, AttemptHandsShootEvent>(OnAttemptHandsShoot);
        // Arcane-Start
        SubscribeLocalEvent<BodyComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshLegTraumaSpeed);
        SubscribeLocalEvent<BodyComponent, RefreshFrictionModifiersEvent>(OnRefreshLegTraumaFriction);
        SubscribeLocalEvent<BodyComponent, BodyTopologyChangedEvent>(OnBodyTopologyChanged);
        // Arcane-End
    }

    #region Event Handling

    private void OnBoneSeverityChanged(Entity<BoneComponent> bone, ref BoneSeverityChangedEvent args)
    {
        if (bone.Comp.BoneWoundable == null
            || args.NewSeverity < args.OldSeverity)
            return;

        var bodyComp = Comp<BodyPartComponent>(bone.Comp.BoneWoundable.Value);

        if (!bodyComp.Body.HasValue)
            return;

        var part = bodyComp.ParentSlot is null
            ? bodyComp.PartType.ToString().ToLower()
            : bodyComp.ParentSlot.Value.Id;

        _popup.PopupClient(Loc.GetString($"popup-trauma-BoneDamage-{args.NewSeverity.ToString()}", ("part", part)),
            bodyComp.Body.Value,
            PopupType.SmallCaution);

        var volumeFloat = args.NewSeverity switch
        {
            BoneSeverity.Damaged => -8f,
            BoneSeverity.Cracked => 1f,
            BoneSeverity.Broken => 6f,
            _ => 0f,
        };

        _audio.PlayPvs(bone.Comp.BoneBreakSound, bodyComp.Body.Value, AudioParams.Default.WithVolume(volumeFloat));
    }

    private void OnBoneIntegrityChanged(Entity<BoneComponent> bone, ref BoneIntegrityChangedEvent args)
    {
        if (bone.Comp.BoneWoundable == null)
            return;

        var bodyComp = Comp<BodyPartComponent>(bone.Comp.BoneWoundable.Value);
        if (!bodyComp.Body.HasValue)
            return;

        if (args.NewIntegrity == bone.Comp.IntegrityCap)
        {
            if (bodyComp.PartType == BodyPartType.Hand)
                _virtual.DeleteInHandsMatching(bodyComp.Body.Value, bone);

            if (TryGetWoundableTrauma(bone.Comp.BoneWoundable.Value, out var traumas, BoneDamage))
                foreach (var trauma in traumas.Where(trauma => trauma.Comp.TraumaTarget == bone))
                    RemoveTrauma(trauma);
        }
        /* // Arcane-Edit-Start
        switch (bodyComp.PartType)
        {
            case BodyPartType.Leg:
            case BodyPartType.Foot:
                ProcessLegsState(bodyComp.Body.Value);

                break;
        }
        */ // Arcane-Edit-End
    }

    // Arcane-Start
    private void OnRefreshLegTraumaSpeed(
        Entity<BodyComponent> body,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        var modifiers = GetLegTraumaModifiers(body);
        args.ModifySpeed(modifiers.Walk, modifiers.Sprint);
    }

    private void OnRefreshLegTraumaFriction(
        Entity<BodyComponent> body,
        ref RefreshFrictionModifiersEvent args)
    {
        var modifiers = GetLegTraumaModifiers(body);
        args.ModifyAcceleration(modifiers.Acceleration);
    }
    // Arcane-End

    private void OnGetDoAfterDelayMultiplier(Entity<BoneComponent> bone, ref GetDoAfterDelayMultiplierEvent args)
    {
        args.Multiplier *= bone.Comp.BoneSeverity switch
        {
            BoneSeverity.Damaged => 1.09f,
            BoneSeverity.Cracked => 1.19f,
            BoneSeverity.Broken => 1.33f,
            _ => 1f,
        };
    }

    private void OnAttemptHandsMelee(Entity<BoneComponent> bone, ref AttemptHandsMeleeEvent args)
    {
        var odds = bone.Comp.BoneSeverity switch
        {
            BoneSeverity.Cracked => 0.10f,
            BoneSeverity.Broken => 0.25f,
            _ => 0f,
        };

        if (odds == 0f
            || args.Handled
            || bone.Comp.BoneWoundable is null
            || !TryComp(bone.Comp.BoneWoundable.Value, out BodyPartComponent? bodyPart)
            || bodyPart.Body is not { } body)
            return;

        if (_wound.TryFumble("arm-fumble", new SoundPathSpecifier("/Audio/Effects/slip.ogg"), body, odds))
        {
            args.Handled = true;
            args.Cancel();
        }
    }

    private void OnAttemptHandsShoot(Entity<BoneComponent> bone, ref AttemptHandsShootEvent args)
    {
        var odds = bone.Comp.BoneSeverity switch
        {
            BoneSeverity.Cracked => 0.10f,
            BoneSeverity.Broken => 0.25f,
            _ => 0f,
        };

        if (odds == 0f
            || args.Handled
            || bone.Comp.BoneWoundable is null
            || !TryComp(bone.Comp.BoneWoundable.Value, out BodyPartComponent? bodyPart)
            || bodyPart.Body is not { } body)
            return;

        if (_wound.TryFumble("arm-fumble", new SoundPathSpecifier("/Audio/Effects/slip.ogg"), body, odds))
            args.Handled = true;
    }

    #endregion

    #region Public API

    public bool ApplyDamageToBone(EntityUid bone, FixedPoint2 severity, BoneComponent? boneComp = null)
    {
        if (severity == 0
            || !Resolve(bone, ref boneComp))
            return false;

        var newIntegrity = FixedPoint2.Clamp(boneComp.BoneIntegrity - severity, 0, boneComp.IntegrityCap);
        if (boneComp.BoneIntegrity == newIntegrity)
            return false;

        var ev = new BoneIntegrityChangedEvent((bone, boneComp), boneComp.BoneIntegrity, newIntegrity);
        RaiseLocalEvent(bone, ref ev);

        boneComp.BoneIntegrity = newIntegrity;
        CheckBoneSeverity(bone, boneComp);

        Dirty(bone, boneComp);
        return true;
    }

    public bool ApplyBoneTrauma(
        EntityUid boneEnt,
        Entity<WoundableComponent> woundable,
        Entity<TraumaInflicterComponent> inflicter,
        FixedPoint2 inflicterSeverity,
        BoneComponent? boneComp = null)
    {
        if (!Resolve(boneEnt, ref boneComp))
            return false;

        if (_net.IsServer)
            AddTrauma(boneEnt, woundable, inflicter, BoneDamage, inflicterSeverity);

        ApplyDamageToBone(boneEnt, inflicterSeverity, boneComp);

        return true;
    }

    public bool SetBoneIntegrity(EntityUid bone, FixedPoint2 integrity, BoneComponent? boneComp = null)
    {
        if (!Resolve(bone, ref boneComp))
            return false;

        var newIntegrity = FixedPoint2.Clamp(integrity, 0, boneComp.IntegrityCap);
        if (boneComp.BoneIntegrity == newIntegrity)
            return false;

        var ev = new BoneIntegrityChangedEvent((bone, boneComp), boneComp.BoneIntegrity, newIntegrity);
        RaiseLocalEvent(bone, ref ev);

        boneComp.BoneIntegrity = newIntegrity;
        CheckBoneSeverity(bone, boneComp);

        Dirty(bone, boneComp);
        return true;
    }

    /// <summary>
    /// Updates the broken bones alert for a body based on its current bone state
    /// </summary>
    public void UpdateBodyBoneAlert(EntityUid body, BodyComponent? bodyComp = null)
    {
        if (!Resolve(body, ref bodyComp))
            return;

        bool hasBrokenBones = false;

        var rootPart = bodyComp.RootContainer?.ContainedEntity;
        if (rootPart.HasValue)
        {
            foreach (var (_, woundable) in _wound.GetAllWoundableChildren(rootPart.Value))
            {
                if (woundable.Bone == null)
                    continue;

                foreach (var boneEntity in woundable.Bone.ContainedEntities)
                {
                    if (!TryComp(boneEntity, out BoneComponent? boneComp))
                        continue;

                    if (boneComp.BoneSeverity == BoneSeverity.Broken)
                    {
                        hasBrokenBones = true;
                        break;
                    }
                }

                if (hasBrokenBones)
                    break;
            }
        }

        // Update the alert based on whether any bones are broken
        if (hasBrokenBones)
            _alert.ShowAlert(body, _brokenBonesAlertId);
        else
            _alert.ClearAlert(body, _brokenBonesAlertId);
    }

    #endregion

    #region Private API

    private void CheckBoneSeverity(EntityUid bone, BoneComponent boneComp)
    {
        var nearestSeverity = boneComp.BoneSeverity;

        foreach (var (severity, value) in BoneThresholds)
        {
            if (boneComp.BoneIntegrity < value)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity != boneComp.BoneSeverity)
        {
            var ev = new BoneSeverityChangedEvent((bone, boneComp), boneComp.BoneSeverity, nearestSeverity);
            RaiseLocalEvent(bone, ref ev, true);
        }

        boneComp.BoneSeverity = nearestSeverity;
        Dirty(bone, boneComp);

        if (boneComp.BoneWoundable != null
            && TryComp<BodyPartComponent>(boneComp.BoneWoundable.Value, out var bodyPartComp)
            && bodyPartComp.Body is { } body)
        // Arcane-Edit-Start
        {
            if (bodyPartComp.PartType is BodyPartType.Leg or BodyPartType.Foot)
                ProcessLegsState(body);
            UpdateBodyBoneAlert(body);
        }
        // Arcane-Edit-End
    }

    // Arcane-Edit-Start
    private (float Walk, float Sprint, float Acceleration, float DamagedWalk, float HealthyWalk, int MissingFeet)
        GetLegTraumaModifiers(Entity<BodyComponent> body)
    {
        if (body.Comp.RequiredLegs <= 0)
            return (1f, 1f, 1f, 0f, 0f, 0);

        var penalty = 0f;
        var missingFeet = 0;
        var presentLegs = 0;

        foreach (var legEntity in body.Comp.LegEntities)
        {
            if (!TryComp<BodyPartComponent>(legEntity, out var legPart))
                continue;

            presentLegs++;
            var legPenalty = 0f;

            if (TryComp<WoundableComponent>(legEntity, out var legWoundable)
                && TryComp<BoneComponent>(legWoundable.Bone.ContainedEntities.FirstOrNull(), out var legBone))
            {
                legPenalty += legBone.BoneSeverity switch
                {
                    BoneSeverity.Damaged => 0.05f,
                    BoneSeverity.Cracked => 0.125f,
                    BoneSeverity.Broken => 0.225f,
                    _ => 0f,
                };
            }

            var footEnt = _body.GetBodyChildrenOfType(
                    body,
                    BodyPartType.Foot,
                    symmetry: legPart.Symmetry)
                .FirstOrNull();

            if (footEnt == null)
            {
                missingFeet++;
                penalty += legPenalty + 0.15f;
                continue;
            }

            var footPenalty = 0f;

            if (TryComp<WoundableComponent>(footEnt.Value.Id, out var footWoundable)
                && TryComp<BoneComponent>(
                    footWoundable.Bone.ContainedEntities.FirstOrNull(),
                    out var footBone))
            {
                footPenalty = footBone.BoneSeverity switch
                {
                    BoneSeverity.Damaged => 0.04f,
                    BoneSeverity.Cracked => 0.08f,
                    BoneSeverity.Broken => 0.12f,
                    _ => 0f,
                };
            }

            penalty += legPenalty + footPenalty;
        }
        var missingLegs = Math.Max(
            0,
            body.Comp.RequiredLegs - presentLegs);

        penalty += missingLegs * 0.4f;
        missingFeet += missingLegs;

        if (HasComp<IgnoreSlowOnDamageComponent>(body))
            penalty *= 0.5f;

        var modifier = 1f - Math.Min(0.8f, penalty);

        return (modifier, modifier, modifier, modifier, 1f, missingFeet);
    }
    // Arcane-Edit-End

    // Arcane-Start
    private void OnBodyTopologyChanged(Entity<BodyComponent> body, ref BodyTopologyChangedEvent args) =>
        ProcessLegsState(body);

    private void ProcessLegsState(EntityUid body, BodyComponent? bodyComp = null)
    {
        if (!Resolve(body, ref bodyComp) || bodyComp.RequiredLegs <= 0)
            return;

        var modifiers = GetLegTraumaModifiers((body, bodyComp));

        _movementSpeed.RefreshMovementSpeedModifiers(body);
        _movementSpeed.RefreshFrictionModifiers(body);

        if (modifiers.DamagedWalk < modifiers.HealthyWalk * 0.4f || modifiers.MissingFeet >= 2)
            _standing.Down(body);
        else if (_standing.IsDown(body)
            && !HasComp<KnockedDownComponent>(body)
            && !HasComp<SleepingComponent>(body)
            && !_mobState.IsIncapacitated(body))
            _standing.Stand(body);
    }
    // Arcane-End

    #endregion
}
