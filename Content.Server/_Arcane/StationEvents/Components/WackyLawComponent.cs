using Content.Server._Arcane.StationEvents.Events;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Server._Arcane.StationEvents.Components;

[RegisterComponent]
[Access(typeof(WackyLawRule))]
public sealed partial class WackyLawComponent : Component
{
    /// <summary>
    /// Датасет предметов для законов
    /// </summary>
    [DataField]
    public ProtoId<DatasetPrototype> ItemDataset = "ArcaneItem";

    /// <summary>
    /// Датасет напитков (используй текст вместо айдишников)
    /// </summary>
    [DataField]
    public ProtoId<DatasetPrototype> HalfProhibitionDataset = "ArcaneHalfProhibition";

    /// <summary>
    /// Датасет отделов
    /// </summary>
    [DataField]
    public ProtoId<DatasetPrototype> DepartmentDataset = "ArcaneDepartments";

    /// <summary>
    /// Датасет законов
    /// </summary>
    [DataField]
    public ProtoId<DatasetPrototype> LawsDataset = "ArcaneLaws";

}
