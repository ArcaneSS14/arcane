// SPDX-License-Identifier: MIT

using Content.Shared.Containers.ItemSlots;
using Content.Goobstation.Server.Chemistry.EntitySystems;
using Content.Goobstation.Shared.Chemistry;
using Content.Shared._Orion.Construction.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Server.Chemistry.Components
{
    /// <summary>
    /// A machine that dispenses reagents into a solution container from containers in its storage slots.
    /// </summary>
    [RegisterComponent]
    [Access(typeof(EnergyReagentDispenserSystem))]
    public sealed partial class EnergyReagentDispenserComponent : Component
    {
        [DataField]
        public ItemSlot EnergyBeakerSlot = new();

        [DataField]
        public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

        [ViewVariables(VVAccess.ReadWrite)]
        public EnergyReagentDispenserDispenseAmount DispenseAmount = EnergyReagentDispenserDispenseAmount.U10;

        [DataField, ViewVariables]
        public SoundSpecifier PowerSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");

        [DataField]
        public Dictionary<string, float> Reagents = [];

        // Orion-Start
        [DataField]
        public float RefundEnergyEfficiency = 0.5f;

        [DataField]
        public Dictionary<string, float>? ReagentsEmagged = [];

        [DataField]
        public bool Emagged;

        [ViewVariables(VVAccess.ReadWrite)]
        public float BaseRechargeRate = 25f;

        [ViewVariables(VVAccess.ReadWrite)]
        public float FinalRechargeRate = 25f;

        [ViewVariables(VVAccess.ReadWrite)]
        public float BaseEnergyCostMultiplier = 1f;

        [ViewVariables(VVAccess.ReadWrite)]
        public float FinalEnergyCostMultiplier = 1f;

        [DataField]
        public ProtoId<MachinePartPrototype> CapacitorPart = "Capacitor";

        [DataField]
        public ProtoId<MachinePartPrototype> MatterBinPart = "MatterBin";
        // Orion-End
    }
}
