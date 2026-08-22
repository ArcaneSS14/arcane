// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Changeling.Components;
using Content.Goobstation.Shared.InternalResources.Components;
using Content.Goobstation.Shared.InternalResources.Data;
using Content.Goobstation.Shared.InternalResources.EntitySystems;
using Content.Server.Body.Systems;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.Changeling;

public sealed class ChangelingEggSystem : EntitySystem
{
    // Arcane-Start
    public static readonly IReadOnlyList<Type> TransferredComponents =
    [
        typeof(ChangelingComponent),
        typeof(ChangelingIdentityComponent),
        typeof(ChangelingChemicalComponent),
        typeof(ChangelingRegenerateComponent),
        typeof(ChangelingStasisComponent),
        typeof(ChangelingBiomassComponent),
        typeof(VoidAdaptionComponent),
        typeof(DarknessAdaptionComponent),
        typeof(AugmentedEyesightComponent),
        typeof(ChameleonSkinComponent),
    ];
    // Arcane-End

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly ChangelingSystem _changeling = default!;
    // Arcane-Start
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedInternalResourcesSystem _resource = default!;
    // Arcane-End

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ChangelingEggComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.UpdateTimer)
                continue;

            comp.UpdateTimer = _timing.CurTime + TimeSpan.FromSeconds(comp.UpdateCooldown);

            Cycle(uid, comp);
        }
    }

    public void Cycle(EntityUid uid, ChangelingEggComponent comp)
    {
        if (!comp.active)
        {
            comp.active = true;
            return;
        }

        if (TerminatingOrDeleted(comp.lingMind))
        {
            _bodySystem.GibBody(uid);
            return;
        }

        var newUid = Spawn("MobMonkey", Transform(uid).Coordinates);

        EnsureComp<MindContainerComponent>(newUid);
        _mind.TransferTo(comp.lingMind, newUid);

        // Arcane-Edit-Start
        foreach (var snapshot in comp.LingComponents)
        {
            if (HasComp(newUid, snapshot.GetType()))
                continue;

            EntityManager.AddComponent(newUid, snapshot);
        }

        var identity = EnsureComp<ChangelingIdentityComponent>(newUid);
        identity.IsInLastResort = false;

        if (TryComp<ChangelingBiomassComponent>(newUid, out var biomass))
        {
            biomass.ResourceData = RestoreInternalResources(newUid, biomass.ResourceData);
            Dirty(newUid, biomass);
        }

        if (TryComp<ChangelingChemicalComponent>(newUid, out var chem))
        {
            chem.ResourceData = RestoreInternalResources(newUid, chem.ResourceData);
            Dirty(newUid, chem);
        }

        if (TryComp<InternalResourcesComponent>(newUid, out var internalRes))
            Dirty(newUid, internalRes);
        // Arcane-Edit-End

        EntityManager.AddComponent(newUid, comp.lingStore);

        _bodySystem.GibBody(uid);
    }

    // Arcane-Start
    private InternalResourcesData? RestoreInternalResources(EntityUid uid, InternalResourcesData? oldData)
    {
        if (oldData == null
            || !_proto.TryIndex<InternalResourcesPrototype>(oldData.InternalResourcesType, out var proto)
            || !_resource.EnsureInternalResources(uid, proto, out var data))
            return null;

        data!.CurrentAmount = oldData.CurrentAmount;
        data.MaxAmount = oldData.MaxAmount;
        data.RegenerationRate = oldData.RegenerationRate;
        data.Thresholds = oldData.Thresholds;

        return data;
    }
    // Arcane-End
}
