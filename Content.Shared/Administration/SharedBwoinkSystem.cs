// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using Content.Shared.Administration.Logs;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration
{
    public abstract class SharedBwoinkSystem : EntitySystem
    {
        // System users
        public static NetUserId SystemUserId { get; } = new NetUserId(Guid.Empty);

        public override void Initialize()
        {
            base.Initialize();

            SubscribeNetworkEvent<BwoinkTextMessage>(OnBwoinkTextMessage);
        }

        protected virtual void OnBwoinkTextMessage(BwoinkTextMessage message, EntitySessionEventArgs eventArgs)
        {
            // Specific side code in target.
        }

        protected void LogBwoink(BwoinkTextMessage message)
        {
        }

        [Serializable, NetSerializable]
        public sealed class BwoinkTextMessage : EntityEventArgs
        {
            public DateTime SentAt { get; }

            public NetUserId UserId { get; }

            // This is ignored from the client.
            // It's checked by the client when receiving a message from the server for bwoink noises.
            // This could be a boolean "Incoming", but that would require making a second instance.
            public NetUserId TrueSender { get; }
            public string Text { get; }

            public bool PlaySound { get; }

            public readonly bool AdminOnly;
            public int? RoundId { get; } // Arcane

            public BwoinkTextMessage(NetUserId userId, NetUserId trueSender, string text, DateTime? sentAt = default, bool playSound = true, bool adminOnly = false, int? roundId = null) // Arcane
            {
                SentAt = sentAt ?? DateTime.Now;
                UserId = userId;
                TrueSender = trueSender;
                Text = text;
                PlaySound = playSound;
                AdminOnly = adminOnly;
                RoundId = roundId; // Arcane
            }
        }
    }

    /// <summary>
    ///     Sent by the server to notify all clients when the webhook url is sent.
    ///     The webhook url itself is not and should not be sent.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkDiscordRelayUpdated : EntityEventArgs
    {
        public bool DiscordRelayEnabled { get; }

        public BwoinkDiscordRelayUpdated(bool enabled)
        {
            DiscordRelayEnabled = enabled;
        }
    }

    /// <summary>
    ///     Sent by the client to notify the server when it begins or stops typing.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkClientTypingUpdated : EntityEventArgs
    {
        public NetUserId Channel { get; }
        public bool Typing { get; }

        public BwoinkClientTypingUpdated(NetUserId channel, bool typing)
        {
            Channel = channel;
            Typing = typing;
        }
    }

    /// <summary>
    ///     Sent by server to notify admins when a player begins or stops typing.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkPlayerTypingUpdated : EntityEventArgs
    {
        public NetUserId Channel { get; }
        public string PlayerName { get; }
        public bool Typing { get; }

        public BwoinkPlayerTypingUpdated(NetUserId channel, string playerName, bool typing)
        {
            Channel = channel;
            PlayerName = playerName;
            Typing = typing;
        }
    }

    // Arcane-start
    [Serializable, NetSerializable]
    public sealed class BwoinkHistoryRequest : EntityEventArgs
    {
        public NetUserId Channel { get; }
        public AdminLogCursor? Cursor { get; }

        public BwoinkHistoryRequest(NetUserId channel, AdminLogCursor? cursor = null)
        {
            Channel = channel;
            Cursor = cursor;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BwoinkHistoryResponse : EntityEventArgs
    {
        public NetUserId Channel { get; }
        public List<BwoinkHistoryMessage> Messages { get; }
        public AdminLogCursor? NextCursor { get; }
        public bool HasMore { get; }
        public bool IsContinuation { get; }

        public BwoinkHistoryResponse(NetUserId channel, List<BwoinkHistoryMessage> messages, AdminLogCursor? nextCursor, bool hasMore, bool isContinuation)
        {
            Channel = channel;
            Messages = messages;
            NextCursor = nextCursor;
            HasMore = hasMore;
            IsContinuation = isContinuation;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BwoinkHistoryMessage
    {
        public DateTime SentAt { get; }
        public string Text { get; }
        public bool AdminOnly { get; }
        public int RoundId { get; }

        public BwoinkHistoryMessage(DateTime sentAt, string text, bool adminOnly, int roundId)
        {
            SentAt = sentAt;
            Text = text;
            AdminOnly = adminOnly;
            RoundId = roundId;
        }
    }
    // Arcane-end
}
