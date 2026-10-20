using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Clothing.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ArcaneItemClothingVisualsComponent : Component
{
    [DataField("clothingVisuals")]
    public Dictionary<string, List<ArcaneClothingLayerData>> ClothingVisuals = new();

    [DataField("containerWhitelist")]
    public HashSet<string>? ContainerWhitelist;
}
