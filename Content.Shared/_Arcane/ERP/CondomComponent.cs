using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ERP;

/// <summary>
///     Marks a garment as a worn condom. Ejaculation fills it with cum instead of leaving a stain on the floor.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CondomComponent : Component
{
    /// <summary>
    ///     Solution the cum is stored in.
    /// </summary>
    [DataField]
    public string SolutionId = "condom";

    /// <summary>
    ///     The only reagent this condom can ever hold.
    /// </summary>
    [DataField]
    public ReagentId CumReagent = new("Semen", null);

    /// <summary>
    ///     How much cum an ejaculation adds.
    /// </summary>
    [DataField]
    public FixedPoint2 FillPerEjaculation = 2;

    /// <summary>
    ///     How much cum the condom takes before it turns into <see cref="FilledPrototype"/>.
    /// </summary>
    [DataField]
    public FixedPoint2 Capacity = 2;

    /// <summary>
    ///     Prototype this condom becomes once it is used up. Should carry <see cref="Full"/>.
    /// </summary>
    [DataField]
    public EntProtoId FilledPrototype = "ClothingUnderwearCondomFilled";

    /// <summary>
    ///     Amount of <see cref="CumReagent"/> currently inside.
    /// </summary>
    [AutoNetworkedField]
    public FixedPoint2 Fill;

    /// <summary>
    ///     Whether this is the used-up variant, which no longer counts as transparent.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Full;

    [DataField]
    public SoundSpecifier PopSound = new SoundPathSpecifier("/Audio/Effects/balloon-pop.ogg");
}
