using System.IO;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ERP.Preferences;

/// <summary>Server → Client: current organ preferences for a character slot.</summary>
public sealed class MsgErpOrganPreferences : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public int Slot;
    public ErpOrganPreferences Preferences = ErpOrganPreferences.Default();

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Slot = buffer.ReadInt32();
        var length = buffer.ReadVariableInt32();
        if (length < 0 || length > 65_536)
            throw new InvalidOperationException($"ERP prefs payload size out of range: {length}");
        using var stream = new MemoryStream(length);
        buffer.ReadAlignedMemory(stream, length);
        stream.Position = 0;
        Preferences = serializer.Deserialize<ErpOrganPreferences>(stream);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Slot);
        using var stream = new MemoryStream();
        serializer.Serialize(stream, Preferences);
        buffer.WriteVariableInt32((int) stream.Length);
        stream.TryGetBuffer(out var segment);
        buffer.Write(segment);
    }
}

/// <summary>Client → Server: save organ preferences for a character slot.</summary>
public sealed class MsgUpdateErpOrganPreferences : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public int Slot;
    public ErpOrganPreferences Preferences = ErpOrganPreferences.Default();

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Slot = buffer.ReadInt32();
        var length = buffer.ReadVariableInt32();
        if (length < 0 || length > 65_536)
            throw new InvalidOperationException($"ERP prefs payload size out of range: {length}");
        using var stream = new MemoryStream(length);
        buffer.ReadAlignedMemory(stream, length);
        stream.Position = 0;
        Preferences = serializer.Deserialize<ErpOrganPreferences>(stream);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Slot);
        using var stream = new MemoryStream();
        serializer.Serialize(stream, Preferences);
        buffer.WriteVariableInt32((int) stream.Length);
        stream.TryGetBuffer(out var segment);
        buffer.Write(segment);
    }
}
