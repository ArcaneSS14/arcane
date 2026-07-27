// SPDX-License-Identifier: MIT

using Content.Server.Chemistry.EntitySystems;
using Content.Shared._Orion.Construction.Prototypes;
using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Chemistry.Components
{
    /// <summary>
    /// A machine that dispenses reagents into a solution container from containers in its storage slots.
    /// </summary>
    [RegisterComponent]
    [Access(typeof(ReagentDispenserSystem))]
    public sealed partial class ReagentDispenserComponent : Component
    {
        [DataField]
        public ItemSlot BeakerSlot = new();

        [DataField("clickSound"), ViewVariables(VVAccess.ReadWrite)]
        public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

        [ViewVariables(VVAccess.ReadWrite)]
        public ReagentDispenserDispenseAmount DispenseAmount = ReagentDispenserDispenseAmount.U10;

        // Orion-Start
        [ViewVariables(VVAccess.ReadWrite)]
        public float BaseRechargeRate = 1f;

        [ViewVariables(VVAccess.ReadWrite)]
        public float FinalRechargeRate = 1f;

        [ViewVariables(VVAccess.ReadWrite)]
        public float BaseEnergyCostPerUnit = 1f;

        [ViewVariables(VVAccess.ReadWrite)]
        public float FinalEnergyCostPerUnit = 1f;

        [DataField]
        public ProtoId<MachinePartPrototype> CapacitorPart = "Capacitor";

        [DataField]
        public ProtoId<MachinePartPrototype> MatterBinPart = "MatterBin";
        // Orion-End
    }
}
