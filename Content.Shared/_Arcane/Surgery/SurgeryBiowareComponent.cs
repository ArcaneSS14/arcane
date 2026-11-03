// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryBiowareComponent : Component
{
    [DataField(required: true)]
    public BiowareCategory Category;

    [DataField]
    public float BleedMultiplier = 1f;

    [DataField]
    public float StunMultiplier = 1f;

    [DataField]
    public float StaminaMultiplier = 1f;

    [DataField]
    public bool ShockImmune;
}

public enum BiowareCategory : byte
{
    Blood,
    Nerves,
}
