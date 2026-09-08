using Robust.Shared.Serialization;

namespace Content.Goobstation.Common.Barks;

[Serializable, NetSerializable]
public sealed class PlayBarkEvent(NetEntity sourceUid, string message, bool whisper, string? barkProtoId = null) : EntityEventArgs // Arcane
{
    public NetEntity SourceUid { get; } = sourceUid;
    public string Message { get; } = message;
    public bool Whisper { get; } = whisper;

    // Arcane-Start
    /// <summary>
    /// Bark prototype id, when the bark is played for a radio listener that cannot see the speaker entity.
    /// </summary>
    public string? BarkProtoId { get; } = barkProtoId;
    // Arcane-End
}

[Serializable, NetSerializable]
public sealed class PreviewBarkEvent(string barkProtoID) : EntityEventArgs
{
    public string BarkProtoID { get; } = barkProtoID;
}
