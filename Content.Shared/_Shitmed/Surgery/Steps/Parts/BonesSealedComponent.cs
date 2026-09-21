// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;

/// <summary>
///     Arcane: marks a bone as sealed after the sealing step.
///     Sawing the bone removes this component again, so a sealed part must be sawed once more to be reopened.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BonesSealedComponent : Component;
