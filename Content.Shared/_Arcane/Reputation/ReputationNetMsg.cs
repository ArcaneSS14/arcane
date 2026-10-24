// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Text.Json.Serialization;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.Reputation;

public sealed class ReputationNetMsg : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public ReputationInfo? Info;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var success = buffer.ReadBoolean();
        buffer.ReadPadBits();

        if (!success)
            return;

        var length = buffer.ReadVariableInt32();
        using var stream = new MemoryStream();
        buffer.ReadAlignedMemory(stream, length);
        serializer.DeserializeDirect(stream, out Info);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Info != null);
        buffer.WritePadBits();

        if (Info == null)
            return;

        var stream = new MemoryStream();
        serializer.SerializeDirect(stream, Info);
        buffer.WriteVariableInt32((int) stream.Length);
        buffer.Write(stream.AsSpan());
    }
}

[Serializable, NetSerializable]
public sealed class ReputationInfo
{
    [JsonPropertyName("value")]
    public float Value { get; set; }
}
