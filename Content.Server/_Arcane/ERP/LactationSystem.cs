using Content.Server._Arcane.ERP;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._Arcane.ErpPanel;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;

namespace Content.Server._Arcane.ERP;

public sealed class LactationSystem : EntitySystem
{
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LactationRequestedEvent>(OnLactationRequested);
    }

    private void OnLactationRequested(ref LactationRequestedEvent ev)
    {
        Log.Info($"MILK: {ev.User} -> {ev.Target}, кнопка {ev.Prototype.ID}");
        var data = ev.Prototype.Lactation;
        if (data == null) return;

        if (!TryComp<LactationComponent>(ev.User, out var lactComp))
            return;
        if (!TryComp<HungerComponent>(ev.User, out var hungerComp))
            return;
        if (hungerComp.CurrentHunger < lactComp.MinHunger)
            return;

        LossNutrients(ev.User, lactComp.NutrientCostPerUnit, data.AmountPerAct);

        if (data.Express)
            DoExpress(ev.User, ev.Target, data.Reagent, data.AmountPerAct);
        else
            DoSuck(ev.User, ev.Target, data.Reagent, data.AmountPerAct);
    }

    private void LossNutrients(EntityUid uid, float costPerUnit, FixedPoint2 amount)
    {
        float totalCost = costPerUnit * amount.Float();
        _hunger.ModifyHunger(uid, -totalCost);
    }

    private void DoExpress(EntityUid user, EntityUid target, ProtoId<ReagentPrototype> reagent, FixedPoint2 amount)
    {
        if (!_hands.ActiveHandIsEmpty(target) && _hands.TryGetActiveItem(target, out var item))
        {
            if (!TryComp<SolutionContainerManagerComponent>(item, out var solutionComp))
            {
                SpillPuddle(user, reagent, amount);
                return;
            }

            Entity<SolutionComponent> solutionEntity;
            if (!_solutionContainer.TryGetSolution((item, solutionComp), "drink", out solutionEntity)
                && !_solutionContainer.TryGetSolution((item, solutionComp), "beaker", out solutionEntity))
            {
                SpillPuddle(user, reagent, amount);
                return;
            }

            var availableVolume = solutionEntity.Comp.AvailableVolume;
            if (availableVolume >= amount)
                _solutionContainer.TryAddReagent(solutionEntity, reagent, amount);
            else
                SpillPuddle(user, reagent, amount);
        }
        else
        {
            SpillPuddle(user, reagent, amount);
        }
    }

    private void SpillPuddle(EntityUid user, ProtoId<ReagentPrototype> reagent, FixedPoint2 amount)
    {
        var solution = new Solution(reagent, amount);
        _puddle.TrySplashSpillAt(user, Transform(user).Coordinates, solution, out _);
    }

    private void DoSuck(EntityUid user, EntityUid target, ProtoId<ReagentPrototype> reagent, FixedPoint2 amount)
    {
        // TODO: Фаза 3.5 - добавить молоко в chemicals приёмника
        Log.Info($"Suck: {reagent} {amount} to {target}");
    }
}
