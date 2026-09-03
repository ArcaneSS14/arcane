// SPDX-License-Identifier: MIT

using Content.Server.Construction.Components;
using Content.Shared.Construction;

namespace Content.Server.Destructible.Thresholds.Behaviors
{
    [Serializable]
    [DataDefinition]
    public sealed partial class ChangeConstructionNodeBehavior : IThresholdBehavior
    {
        [DataField("node")]
        public string Node { get; private set; } = string.Empty;

        public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
        {
            if (string.IsNullOrEmpty(Node) || !system.EntityManager.TryGetComponent(owner, out ConstructionComponent? construction))
                return;

            // Arcane-Start
            // Raise MachineDeconstructedEvent before ChangeNode so that systems like
            // ChemMasterBeakerCapacitySystem can return buffer contents to machine_parts
            // before the containers are transferred to the new MachineFrame entity.
            if (Node == "machineFrame")
                system.EntityManager.EventBus.RaiseLocalEvent(owner, new MachineDeconstructedEvent());
            // Arcane-End

            system.ConstructionSystem.ChangeNode(owner, null, Node, true, construction);
        }
    }
}
