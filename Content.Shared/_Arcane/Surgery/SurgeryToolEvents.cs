// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.Surgery;

/// <summary>
/// A step a held tool can perform on a part. <see cref="Surgery"/> owns the step, which may be a requirement of
/// <see cref="Target"/>; the target is null when the step leads to several surgeries at once.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct SurgeryToolOption(EntProtoId Surgery, EntProtoId Step, EntProtoId? Target);

/// <summary>
/// Sent by the server when a tool click could perform several steps, or one that needs confirming.
/// </summary>
[Serializable, NetSerializable]
public sealed class SurgeryToolOptionsEvent(NetEntity part, List<SurgeryToolOption> options) : EntityEventArgs
{
    public readonly NetEntity Part = part;
    public readonly List<SurgeryToolOption> Options = options;
}

/// <summary>
/// Sent by the client after picking one of several steps its tool click could perform.
/// </summary>
[Serializable, NetSerializable]
public sealed class SurgeryToolOptionPickedEvent(NetEntity part, SurgeryToolOption option) : EntityEventArgs
{
    public readonly NetEntity Part = part;
    public readonly SurgeryToolOption Option = option;
}

/// <summary>
/// Sets or clears the surgeon's planned surgery from the surgery chart.
/// </summary>
[Serializable, NetSerializable]
public sealed class SurgeryPlanBuiMsg(NetEntity part, EntProtoId? surgery) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
    public readonly EntProtoId? Surgery = surgery;
}
