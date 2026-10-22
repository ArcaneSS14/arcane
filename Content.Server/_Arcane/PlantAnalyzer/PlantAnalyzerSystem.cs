using Content.Server.Botany.Components;
using Content.Shared._Arcane.PlantAnalyzer;
using Content.Shared.Atmos;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using System.Collections.Generic;

namespace Content.Server._Arcane.PlantAnalyzer;

public sealed class PlantAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedAtmosphereSystem _atmosSystem = default!;

    private const float MaxScanDistance = 3.0f;

    private int _ticks;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, PlantAnalyzerComponent component, AfterInteractEvent args)
    {
        if (args.Target is not { } target || !args.CanReach)
            return;

        if (!_hands.IsHolding(args.User, uid, out _))
            return;

        if (!_transformSystem.InRange(args.User, target, MaxScanDistance))
            return;

        if (!TryAnalyzeTarget(target, out var uiState))
        {
            _popup.PopupEntity(Loc.GetString("plant-analyzer-target-invalid"), target, args.User);
            return;
        }

        args.Handled = true;
        component.Target = target;

        if (component.UiUser is not { } uiUser ||
            !_uiSystem.IsUiOpen(uid, PlantAnalyzerUiKey.Key, uiUser))
        {
            component.UiUser = args.User;
        }

        if (component.ScanSound != null)
        {
            _audio.PlayPredicted(component.ScanSound, uid, args.User);
        }

        if (_uiSystem.TryOpenUi(uid, PlantAnalyzerUiKey.Key, args.User))
        {
            _uiSystem.SetUiState(uid, PlantAnalyzerUiKey.Key, uiState);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _ticks++;
        if (_ticks % 5 != 0)
            return;

        var query = EntityQueryEnumerator<PlantAnalyzerComponent, UserInterfaceComponent>();
        while (query.MoveNext(out var uid, out var analyzer, out _))
        {
            if (!_uiSystem.IsUiOpen(uid, PlantAnalyzerUiKey.Key))
                continue;

            var parent = Transform(uid).ParentUid;

            if (!parent.IsValid() || !_hands.IsHolding(parent, uid, out _))
            {
                _uiSystem.CloseUi(uid, PlantAnalyzerUiKey.Key);
                analyzer.UiUser = null;
                analyzer.Target = null;
                continue;
            }

            if (analyzer.UiUser is { } uiUser && uiUser != parent)
            {
                _uiSystem.CloseUi(uid, PlantAnalyzerUiKey.Key);
                analyzer.UiUser = null;
                analyzer.Target = null;
                continue;
            }

            if (analyzer.Target is not { } target || Deleted(target))
            {
                _uiSystem.CloseUi(uid, PlantAnalyzerUiKey.Key);
                analyzer.UiUser = null;
                analyzer.Target = null;
                continue;
            }

            if (!_transformSystem.InRange(parent, target, MaxScanDistance))
            {
                _uiSystem.CloseUi(uid, PlantAnalyzerUiKey.Key);
                analyzer.UiUser = null;
                analyzer.Target = null;
                continue;
            }

            if (!TryAnalyzeTarget(target, out var uiState))
            {
                _uiSystem.CloseUi(uid, PlantAnalyzerUiKey.Key);
                analyzer.UiUser = null;
                analyzer.Target = null;
                continue;
            }

            _uiSystem.SetUiState(uid, PlantAnalyzerUiKey.Key, uiState);
        }
    }

    private string GetReagentLocalizedName(string reagentId)
    {
        if (_prototypeManager.TryIndex<ReagentPrototype>(reagentId, out var proto))
        {
            return proto.LocalizedName;
        }
        return reagentId;
    }

    private string GetGasLocalizedName(Gas gas)
    {
        var gasProto = _atmosSystem.GetGas(gas);
        return Loc.GetString(gasProto.Name);
    }

    private bool TryAnalyzeTarget(EntityUid target, out PlantAnalyzerUserInterfaceState state)
    {
        state = default!;
        var soilReagents = new List<PlantAnalyzerReagentInfo>();
        var produceReagents = new List<PlantAnalyzerReagentInfo>();

        var netTarget = GetNetEntity(target);

        if (TryComp<PlantHolderComponent>(target, out var plantHolder))
        {
            var targetName = Name(target);
            var seed = plantHolder.Seed;
            var hasPlant = seed != null;
            var plantName = hasPlant ? Loc.GetString(seed!.Name) : string.Empty;

            var potency = hasPlant ? (int) seed!.Potency : 0;
            var yield = hasPlant ? seed!.Yield : 0;
            var maxAge = hasPlant ? seed!.GrowthStages : 0;
            var mutationLevel = hasPlant ? (int) plantHolder.MutationLevel : 0;
            var growthRate = hasPlant ? (int) seed!.Production : 0;
            var exudeGases = new List<string>();
            var specialGene = PlantSpecialGene.None;

            var minTemp = 0f;
            var maxTemp = 0f;
            var minPressure = 0f;
            var maxPressure = 0f;

            if (hasPlant && seed != null)
            {
                minTemp = seed.IdealHeat - seed.HeatTolerance;
                maxTemp = seed.IdealHeat + seed.HeatTolerance;
                minPressure = seed.LowPressureTolerance;
                maxPressure = seed.HighPressureTolerance;

                if (seed.TurnIntoKudzu)
                {
                    specialGene = PlantSpecialGene.Kudzu;
                }
                //else if (seed.Carnivorous)
                //{
                //    specialGene = PlantSpecialGene.Lethal;
                //}

                foreach (var (gas, _) in seed.ExudeGasses)
                {
                    var localizedGasName = GetGasLocalizedName(gas);
                    if (!exudeGases.Contains(localizedGasName))
                        exudeGases.Add(localizedGasName);
                }
            }

            var age = plantHolder.Age;
            var harvestable = plantHolder.Harvest;
            var dead = plantHolder.Dead;
            var health = plantHolder.Health;
            var maxHealth = hasPlant ? seed!.Endurance : 100f;

            var weedLevel = (int) plantHolder.WeedLevel;
            var pestLevel = (int) plantHolder.PestLevel;
            var toxins = (int) plantHolder.Toxins;

            if (_solutionContainer.TryGetSolution(target, "soil", out var _, out var soilSolution))
            {
                foreach (var reagent in soilSolution.Contents)
                {
                    var name = GetReagentLocalizedName(reagent.Reagent.ToString());
                    soilReagents.Add(new PlantAnalyzerReagentInfo(name, (float) reagent.Quantity));
                }
            }

            if (hasPlant && seed != null)
            {
                foreach (var (reagentId, chemQuantity) in seed.Chemicals)
                {
                    var quantity = (float) chemQuantity.Min;
                    if (chemQuantity.PotencyDivisor > 0)
                        quantity += (float) potency / chemQuantity.PotencyDivisor;

                    var name = GetReagentLocalizedName(reagentId);
                    produceReagents.Add(new PlantAnalyzerReagentInfo(name, quantity));
                }
            }

            state = new PlantAnalyzerUserInterfaceState(
                netTarget, targetName, hasPlant, plantName, potency, yield, age, maxAge,
                harvestable, dead, health, maxHealth, weedLevel, pestLevel,
                toxins, mutationLevel, growthRate, minTemp, maxTemp, minPressure, maxPressure, exudeGases, soilReagents, produceReagents, specialGene
            );

            return true;
        }

        if (HasComp<ProduceComponent>(target))
        {
            var targetName = Name(target);

            if (_solutionContainer.TryGetSolution(target, "food", out var _, out var foodSolution))
            {
                foreach (var reagent in foodSolution.Contents)
                {
                    var name = GetReagentLocalizedName(reagent.Reagent.ToString());
                    produceReagents.Add(new PlantAnalyzerReagentInfo(name, (float) reagent.Quantity));
                }
            }

            state = new PlantAnalyzerUserInterfaceState(
                netTarget, targetName, true, targetName, 0, 0, 0, 0,
                false, false, 100f, 100f, 0, 0, 0, 0, 0, 0f, 0f, 0f, 0f,
                new List<string>(), soilReagents, produceReagents, PlantSpecialGene.None
            );

            return true;
        }

        return false;
    }
}
