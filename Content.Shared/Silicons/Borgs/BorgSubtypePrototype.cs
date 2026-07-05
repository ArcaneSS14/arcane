// SPDX-FileCopyrightText: 2025 BeBright <98597725+be1bright@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Silicons.Borgs;

/// <summary>
///  Information relating to a borg's subtype. Should be purely cosmetic.
/// </summary>
[Prototype]
public sealed partial class BorgSubtypePrototype : IPrototype
{
    [IdDataField]
    public required string ID { get; set; }

    /// <summary>
    /// Prototype to display in the selection menu for the subtype.
    /// </summary>
    [DataField]
    public required EntProtoId DummyPrototype;

    /// <summary>
    /// The sprite path belonging to this particular subtype.
    /// </summary>
    [DataField]
    public required ResPath SpritePath;

    /// <summary>
    /// The parent borg type that the subtype will be shown under in the selection menu.
    /// </summary>
    [DataField]
    public required ProtoId<BorgTypePrototype> ParentBorgType = "generic";

    // Arcane-Edit-Start
    /// <summary>
    /// Optional visual overrides for this subtype.
    /// </summary>
    [DataField]
    public BorgSubtypeVisuals Visuals = new();
    // Arcane-Edit-End
}

// Arcane-Edit-Start
[DataDefinition]
public sealed partial class BorgSubtypeVisuals
{
    /// <summary>
    /// RSI state name to use when this borg is resting. Leave null if the subtype has no rest sprite.
    /// </summary>
    [DataField]
    public string? RestBodyState;

    /// <summary>
    /// RSI state name to use when this borg is moving. Falls back to the parent borg type movement state when null.
    /// </summary>
    [DataField]
    public string? MovementBodyState;

    /// <summary>
    /// RSI state name to use for the has-mind light layer. Falls back to the parent borg type state when null.
    /// </summary>
    [DataField]
    public string? HasMindState;

    /// <summary>
    /// RSI state name to use for the no-mind light layer. Falls back to the parent borg type state when null.
    /// </summary>
    [DataField]
    public string? NoMindState;

    /// <summary>
    /// RSI state name to use for the toggleable light layer. Falls back to the parent borg type state when null.
    /// </summary>
    [DataField]
    public string? ToggleLightState;

    /// <summary>
    /// If true, do not install movement body-state visuals for this subtype.
    /// </summary>
    [DataField]
    public bool DisableMovementVisuals;
}
// Arcane-Edit-End
