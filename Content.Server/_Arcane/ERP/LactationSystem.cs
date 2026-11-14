using Content.Shared._Arcane.ERP;
using Content.Shared._Arcane.ERP.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._Arcane.ErpPanel;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameObjects;
using Content.Shared.Body.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Body.Components;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;

namespace Content.Server._Arcane.ERP;

public sealed class LactationSystem : EntitySystem
{
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly StomachSystem _stomach = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(2.5);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LactationRequestedEvent>(OnLactationRequested);
        SubscribeLocalEvent<MobStateComponent, LactationDoAfterEvent>(OnDoAfterComplete);
    }

    private void OnLactationRequested(LactationRequestedEvent ev)
    {
        var data = ev.Prototype.Lactation;
        if (data == null)
            return;

        if (!TryComp<LactationComponent>(ev.Target, out var lactComp))
            return;
        if (!TryComp<HungerComponent>(ev.Target, out var hungerComp))
            return;
        if (_hunger.GetHunger(hungerComp) < lactComp.MinHunger)
            return;

        var args = new DoAfterArgs(EntityManager, ev.User, Delay, new LactationDoAfterEvent
        {
            Donor = GetNetEntity(ev.Target),
            Receiver = GetNetEntity(ev.User),
            Reagent = data.Reagent,
            Amount = data.AmountPerAct,
            Express = data.Express,
        }, ev.User, target: ev.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        _doAfter.TryStartDoAfter(args);
    }


    private void OnDoAfterComplete(EntityUid uid, MobStateComponent component, LactationDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var donor = GetEntity(args.Donor);
        var receiver = GetEntity(args.Receiver);

        if (!TryComp<LactationComponent>(donor, out var lactComp))
            return;
        if (!TryComp<HungerComponent>(donor, out var hungerComp))
            return;
        if (_hunger.GetHunger(hungerComp) < lactComp.MinHunger)
            return;

        LossNutrients(donor, lactComp.NutrientCostPerUnit, args.Amount);

        if (args.Express)
            DoExpress(donor, receiver, args.Reagent, args.Amount);
        else
            DoSuck(donor, receiver, args.Reagent, args.Amount);
    }

    private void LossNutrients(EntityUid uid, float costPerUnit, FixedPoint2 amount)
    {
        float totalCost = costPerUnit * amount.Float();
        _hunger.ModifyHunger(uid, -totalCost);
    }

    private void DoExpress(EntityUid donor, EntityUid receiver, ProtoId<ReagentPrototype> reagent, FixedPoint2 amount)
    {
        if (!_hands.ActiveHandIsEmpty(receiver) && _hands.TryGetActiveItem(receiver, out var item))
        {
            if (!TryComp<SolutionContainerManagerComponent>(item, out var solutionComp))
            {
                SpillPuddle(receiver, reagent, amount);
                return;
            }

            if (!_solutionContainer.TryGetSolution((item.Value, solutionComp), "drink", out var solutionEntity)
                && !_solutionContainer.TryGetSolution((item.Value, solutionComp), "beaker", out solutionEntity))
            {
                SpillPuddle(receiver, reagent, amount);
                return;
            }

            var solEnt = solutionEntity.Value;
            var availableVolume = solEnt.Comp.Solution.AvailableVolume;
            if (availableVolume >= amount)
                _solutionContainer.TryAddReagent(solEnt, new ReagentQuantity(reagent, amount), out _);
            else
                SpillPuddle(receiver, reagent, amount);
        }
        else
        {
            SpillPuddle(receiver, reagent, amount);
        }
    }

    private void SpillPuddle(EntityUid user, ProtoId<ReagentPrototype> reagent, FixedPoint2 amount)
    {
        var solution = new Solution(reagent, amount);
        _puddle.TrySplashSpillAt(user, Transform(user).Coordinates, solution, out _);
    }

    private void DoSuck(EntityUid donor, EntityUid receiver, ProtoId<ReagentPrototype> reagent, FixedPoint2 amount)
    {
        var solution = new Solution();
        var stomachList = _body.GetBodyOrganEntityComps<StomachComponent>(receiver);
        solution.AddReagent(reagent, amount);
        if (stomachList.Count == 0)
        {
            SpillPuddle(receiver, reagent, amount);
            return;
        }
        var stomach = stomachList[0];

        bool worked = _stomach.TryTransferSolution(stomach.Owner, solution, stomach.Comp1);
        if (!worked)
        {
            SpillPuddle(receiver, reagent, amount);
        }
    }
}
