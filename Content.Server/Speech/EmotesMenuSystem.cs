// SPDX-FileCopyrightText: 2024 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2024 Morb <14136326+Morb0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Players.RateLimiting;
using Robust.Shared.Prototypes;

namespace Content.Server.Speech;

public sealed class EmotesMenuSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IChatManager _chatManager = default!; // Arcane

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<PlayEmoteMessage>(OnPlayEmote);
    }

    private void OnPlayEmote(PlayEmoteMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (!player.HasValue)
            return;

        // Arcane-start
        if (_chatManager.HandleRateLimit(args.SenderSession) != RateLimitStatus.Allowed)
            return;
        // Arcane-end

        if (!_prototypeManager.TryIndex(msg.ProtoId, out var proto) || proto.ChatTriggers.Count == 0)
            return;

        _chat.TryEmoteWithChat(player.Value, msg.ProtoId); // Goob - emotespam
    }
}
