// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Shared._Arcane.Slime;

/// <summary>
/// Raised whenever the regrow limb action is performed on an entity
/// with a <see cref="SlimeRegrowComponent"/>.
/// </summary>
public sealed partial class SlimeRegrowLimbEvent : InstantActionEvent;
