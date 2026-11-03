using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.Surgery;

/// <summary>
///     Makes a surgery unavailable on robotic parts, where <see cref="Variant"/> takes its place, also as a requirement of other surgeries.
/// </summary>
[RegisterComponent]
public sealed partial class SurgeryRoboticVariantComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Variant;
}
