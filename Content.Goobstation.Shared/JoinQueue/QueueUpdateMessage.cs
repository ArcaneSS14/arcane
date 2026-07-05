using Content.Shared._Arcane.JoinQueue;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using System.IO; // Arcane

namespace Content.Goobstation.Shared.JoinQueue;

/// <summary>
///     Sent from server to client with queue state for player
///     Also initiates queue state on client
/// </summary>
public sealed class QueueUpdateMessage : NetMessage
{
    // Arcane-edit-start
    private const int MaxQueuePlayerEntries = 1000;
    private const int MaxMiniGameLeaderboardEntries = 1000;
    // Arcane-edit-end
    public override MsgGroups MsgGroup => MsgGroups.Command;

    // Queue info
    public int Total { get; set; }
    public int Position { get; set; }
    public bool IsPatron { get; set; }

    // Estimated wait
    public float EstimatedWaitSeconds { get; set; } = -1f;

    // Server info
    public string MapName { get; set; } = string.Empty;
    public string GameMode { get; set; } = string.Empty;
    public int ServerPlayerCount { get; set; }
    public int MaxPlayerCount { get; set; }
    public int RoundDurationMinutes { get; set; }

    // Critter display
    public string YourName { get; set; } = string.Empty;
    public List<string> PlayerNames { get; set; } = new();

    // Arcane-edit-start
    public List<float> PlayerWaitSeconds { get; set; } = new();
    public List<string> QueueWaitLeaderboardNames { get; set; } = new();
    public List<float> QueueWaitLeaderboardSeconds { get; set; } = new();
    public List<QueueMiniGameLeaderboardEntry> MiniGameLeaderboard { get; set; } = new();
    // Arcane-edit-end
    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Total = buffer.ReadInt32();
        Position = buffer.ReadInt32();
        IsPatron = buffer.ReadBoolean();
        EstimatedWaitSeconds = buffer.ReadFloat();

        MapName = buffer.ReadString();
        GameMode = buffer.ReadString();
        ServerPlayerCount = buffer.ReadInt32();
        MaxPlayerCount = buffer.ReadInt32();
        RoundDurationMinutes = buffer.ReadInt32();

        YourName = buffer.ReadString();
        var count = buffer.ReadInt32();
        // Arcane-edit-start
        if (count < 0 || count > MaxQueuePlayerEntries)
            throw new InvalidDataException("Queue player count out of range.");
        // Arcane-edit-end

        PlayerNames = new List<string>(count);
        PlayerWaitSeconds = new List<float>(count); // Arcane-edit
        for (var i = 0; i < count; i++)
        {
            PlayerNames.Add(buffer.ReadString());
            PlayerWaitSeconds.Add(buffer.ReadFloat()); // Arcane-edit
        }

        // Arcane-edit-start
        var waitLeaderboardCount = buffer.ReadInt32();
        if (waitLeaderboardCount < 0 || waitLeaderboardCount > MaxQueuePlayerEntries)
            throw new InvalidDataException("Queue wait leaderboard count out of range.");

        QueueWaitLeaderboardNames = new List<string>(waitLeaderboardCount);
        QueueWaitLeaderboardSeconds = new List<float>(waitLeaderboardCount);
        for (var i = 0; i < waitLeaderboardCount; i++)
        {
            QueueWaitLeaderboardNames.Add(buffer.ReadString());
            QueueWaitLeaderboardSeconds.Add(buffer.ReadFloat());
        }
        // Arcane-edit-end

        var leaderboardCount = buffer.ReadInt32();

        // Arcane-edit-start
        if (leaderboardCount < 0 || leaderboardCount > MaxMiniGameLeaderboardEntries)
            throw new InvalidDataException("Queue mini-game leaderboard count out of range.");

        MiniGameLeaderboard = new List<QueueMiniGameLeaderboardEntry>(leaderboardCount);
        for (var i = 0; i < leaderboardCount; i++)
        {
            var game = buffer.ReadByte();
            if (!Enum.IsDefined(typeof(QueueMiniGameKind), game))
                throw new InvalidDataException("Queue mini-game kind out of range.");

            MiniGameLeaderboard.Add(new QueueMiniGameLeaderboardEntry(
                (QueueMiniGameKind) game,
                buffer.ReadString(),
                buffer.ReadInt32()));
        }
        // Arcane-edit-end
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Total);
        buffer.Write(Position);
        buffer.Write(IsPatron);
        buffer.Write(EstimatedWaitSeconds);

        buffer.Write(MapName);
        buffer.Write(GameMode);
        buffer.Write(ServerPlayerCount);
        buffer.Write(MaxPlayerCount);
        buffer.Write(RoundDurationMinutes);

        buffer.Write(YourName);
        // Arcane-edit-start
        var playerCount = Math.Min(Math.Min(PlayerNames.Count, PlayerWaitSeconds.Count), MaxQueuePlayerEntries);
        buffer.Write(playerCount);
        for (var i = 0; i < playerCount; i++)
        {
            buffer.Write(PlayerNames[i]);
            buffer.Write(PlayerWaitSeconds[i]);
        }

        var waitLeaderboardCount = Math.Min(Math.Min(QueueWaitLeaderboardNames.Count, QueueWaitLeaderboardSeconds.Count), MaxQueuePlayerEntries);
        buffer.Write(waitLeaderboardCount);
        for (var i = 0; i < waitLeaderboardCount; i++)
        {
            buffer.Write(QueueWaitLeaderboardNames[i]);
            buffer.Write(QueueWaitLeaderboardSeconds[i]);
        }

        var miniGameLeaderboardCount = Math.Min(MiniGameLeaderboard.Count, MaxMiniGameLeaderboardEntries);
        buffer.Write(miniGameLeaderboardCount);
        for (var i = 0; i < miniGameLeaderboardCount; i++)
        {
            var entry = MiniGameLeaderboard[i];
            buffer.Write((byte) entry.Game);
            buffer.Write(entry.PlayerName);
            buffer.Write(entry.Score);
        }
        // Arcane-edit-end
    }
}
