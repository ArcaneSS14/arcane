using Content.Shared.Humanoid;

namespace Content.Shared._Arcane.Invisibility;

public sealed class SharedArcaneInvisibilitySystem : EntitySystem
{
    /// <summary>
    ///     Humanoid body visual layers (skin and markings, never clothing) that the arcane
    ///     invisibility shader applies to.
    /// </summary>
    public static readonly HumanoidVisualLayers[] BodyLayers =
    [
        HumanoidVisualLayers.Special,
        HumanoidVisualLayers.Tail,
        HumanoidVisualLayers.TailBehind,
        HumanoidVisualLayers.TailBehindBackpack,
        HumanoidVisualLayers.TailOversuit,
        HumanoidVisualLayers.Wings,
        HumanoidVisualLayers.Hair,
        HumanoidVisualLayers.FacialHair,
        HumanoidVisualLayers.UndergarmentTop,
        HumanoidVisualLayers.UndergarmentBottom,
        HumanoidVisualLayers.Face,
        HumanoidVisualLayers.Chest,
        HumanoidVisualLayers.Groin,
        HumanoidVisualLayers.Head,
        HumanoidVisualLayers.Snout,
        HumanoidVisualLayers.SnoutCover,
        HumanoidVisualLayers.HeadSide,
        HumanoidVisualLayers.HeadTop,
        HumanoidVisualLayers.Eyes,
        HumanoidVisualLayers.RArm,
        HumanoidVisualLayers.LArm,
        HumanoidVisualLayers.RHand,
        HumanoidVisualLayers.LHand,
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RFoot,
        HumanoidVisualLayers.LFoot,
    ];
}
