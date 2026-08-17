using Content.Server._Arcane.StationEvents.Events;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.StationEvents.Components;

[RegisterComponent]
[Access(typeof(WackyLawRule))]
public sealed partial class WackyLawComponent : Component
{
    /// <summary>
    /// Department Dataset for event "wacky law"
    /// </summary>
    [DataField]
    public ProtoId<DatasetPrototype> DepartmentDataset = "WackyLawDepartments";

    /// <summary>
    /// Laws dataset for event "wacky law"
    /// </summary>
    [DataField]
    public ProtoId<DatasetPrototype> LawsDataset = "WackyLaws";

}
