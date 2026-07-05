namespace Content.Shared._Arcane.Clothing;

/// <summary>
/// When this clothing item is folded while worn, blocks the wearer's hands with virtual items.
/// Requires <see cref="Content.Shared.Foldable.FoldableComponent"/> with CanFoldInsideContainer = true
/// and <see cref="Content.Shared.Clothing.Components.FoldableClothingComponent"/> for the sprite swap.
/// </summary>
[RegisterComponent]
public sealed partial class FoldedHandsClothingComponent : Component;
