namespace Content.Shared._Arcane.Surgery;

[RegisterComponent]
public sealed partial class SurgeryAutopsyComponent : Component
{
    [DataField(required: true)]
    public List<AutopsyExperiment> Experiments = new();
}

public sealed partial class AutopsyExperiment : SurgeryBodyKind
{
    [DataField(required: true)]
    public string Action = string.Empty;
}
