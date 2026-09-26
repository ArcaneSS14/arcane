// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Arcane.Chemistry.Components;

/// <summary>
///     When present on a reactive entity, reaction effects are applied after a delay instead of instantly.
///     Used by slime extracts so a reaction only fires a short while after the reactive substance is injected.
/// </summary>
[RegisterComponent]
public sealed partial class DelayedReactionComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(5);
}
