// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Orion.Construction.Prototypes;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Lathe
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class LatheComponent : Component
    {
        /// <summary>
        /// All of the recipe packs that the lathe has by default
        /// </summary>
        [DataField]
        public List<ProtoId<LatheRecipePackPrototype>> StaticPacks = new();

        /// <summary>
        /// All of the recipe packs that the lathe is capable of researching
        /// </summary>
        [DataField]
        public List<ProtoId<LatheRecipePackPrototype>> DynamicPacks = new();
        // Note that this shouldn't be modified dynamically.
        // I.e., this + the static recipies should represent all recipies that the lathe can ever make
        // Otherwise the material arbitrage test and/or LatheSystem.GetAllBaseRecipes needs to be updated

        /// <summary>
        /// The lathe's construction queue.
        /// </summary>
        /// <remarks>
        /// This is a LinkedList to allow for constant time insertion/deletion (vs a List), and more efficient
        /// moves (vs a Queue).
        /// </remarks>
        [DataField]
        public LinkedList<LatheRecipeBatch> Queue = new();

        public Dictionary<ProtoId<MaterialPrototype>, int>? ActiveMaterialRefund; // Orion

        /// <summary>
        /// The sound that plays when the lathe is producing an item, if any
        /// </summary>
        [DataField]
        public SoundSpecifier? ProducingSound;

        [DataField]
        public string? ReagentOutputSlotId;

        /// <summary>
        /// The default amount that's displayed in the UI for selecting the print amount.
        /// </summary>
        [DataField, AutoNetworkedField]
        public int DefaultProductionAmount = 1;

        #region Visualizer info
        [DataField]
        public string? IdleState;

        [DataField]
        public string? RunningState;

        [DataField]
        public string? UnlitIdleState;

        [DataField]
        public string? UnlitRunningState;
        #endregion

        /// <summary>
        /// The recipe the lathe is currently producing
        /// </summary>
        [ViewVariables]
        public ProtoId<LatheRecipePrototype>? CurrentRecipe;

        #region MachineUpgrading
        /// <summary>
        /// A modifier that changes how long it takes to print a recipe
        /// </summary>
        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public float TimeMultiplier = 1;

        /// <summary>
        /// A modifier that changes how much of a material is needed to print a recipe
        /// </summary>
        [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
        public float MaterialUseMultiplier = 1;

        // Orion-Start
        [DataField, AutoNetworkedField]
        public float FinalTimeMultiplier = 1;

        [DataField, AutoNetworkedField]
        public float FinalMaterialMultiplier = 1;

        [DataField]
        public ProtoId<MachinePartPrototype> MachinePartPrintSpeed = "Servo";

        [DataField]
        public float BaseMachinePartEfficiency = 1.2f;

        [DataField]
        public float MachinePartEfficiencyTierStep = 0.1f;

        [DataField]
        public float MachinePartEfficiencyExponent = 0.8f;

        [DataField]
        public float MinMachinePartEfficiency = 0.1f;

        [DataField]
        public ProtoId<MachinePartPrototype> MachinePartMaterialCapacity = "MatterBin";

        [DataField]
        public int MaterialStorageTierCapacityBonus = 3750;

        [DataField]
        public int? BaseStorageLimit;
        // Orion-End

        #endregion

        // Goobstation change start
        // <summary>
        // Output to MaterialStorage instead of spawning it
        // </summary>
        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public bool OutputToStorage;
        // Goobstation change end
    }

    public sealed class LatheGetRecipesEvent : EntityEventArgs
    {
        public readonly EntityUid Lathe;
        public readonly LatheComponent Comp;

        public readonly bool GetUnavailable; // Orion-Edit: readonly

        public HashSet<ProtoId<LatheRecipePrototype>> Recipes = new();

        public LatheGetRecipesEvent(Entity<LatheComponent> lathe, bool forced)
        {
            (Lathe, Comp) = lathe;
            GetUnavailable = forced;
        }
    }

    [Serializable]
    public sealed partial class LatheRecipeBatch
    {
        public ProtoId<LatheRecipePrototype> Recipe;
        public int ItemsPrinted;
        public int ItemsRequested;

        // Orion-Edit: Retain the exact per-item cost so upgrades cannot change cancellation refunds.
        [DataField]
        public Dictionary<ProtoId<MaterialPrototype>, int> MaterialCost = new();

        public LatheRecipeBatch(
            ProtoId<LatheRecipePrototype> recipe,
            int itemsPrinted,
            int itemsRequested,
            Dictionary<ProtoId<MaterialPrototype>, int>? materialCost = null)
        {
            Recipe = recipe;
            ItemsPrinted = itemsPrinted;
            ItemsRequested = itemsRequested;
            MaterialCost = materialCost ?? new();
        }
    }

    /// <summary>
    /// Event raised on a lathe when it starts producing a recipe.
    /// </summary>
    [ByRefEvent]
    public readonly record struct LatheStartPrintingEvent(LatheRecipePrototype Recipe);
}
