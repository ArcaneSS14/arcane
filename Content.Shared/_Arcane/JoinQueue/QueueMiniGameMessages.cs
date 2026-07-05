using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using System.IO;

namespace Content.Shared._Arcane.JoinQueue;

// Порядок значений — часть сетевого протокола (см. QueueMiniGameScoreMessage). Новые игры добавлять только в конец.
public enum QueueMiniGameKind : byte
{
    Gyruss,
    GoGoShitcurity,
    SpaceInvaders,
}

public readonly record struct QueueMiniGameLeaderboardEntry(QueueMiniGameKind Game, string PlayerName, int Score);

public sealed class QueueMiniGameScoreMessage : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public QueueMiniGameKind Game { get; set; }
    public int Score { get; set; }

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var game = buffer.ReadByte();
        if (!Enum.IsDefined(typeof(QueueMiniGameKind), game))
            throw new InvalidDataException("Queue mini-game kind out of range.");

        Game = (QueueMiniGameKind) game;
        Score = buffer.ReadInt32();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write((byte) Game);
        buffer.Write(Score);
    }
}
