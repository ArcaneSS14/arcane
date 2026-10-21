// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Shared.Chat;
using Content.Shared._Arcane.Chat;
using Content.Shared.Database;
using Robust.Shared.Player;

namespace Content.Server._Arcane.Chat.Systems;

public sealed class ChatInputLoggingSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    private readonly Dictionary<ICommonSession, EntityUid?> _focusedSessions = new();
    private readonly HashSet<EntityUid> _focusedEntities = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ChatInputFocusChangedEvent>(OnChatInputFocusChanged);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    public bool IsChatInputFocused(EntityUid entity)
    {
        return _focusedEntities.Contains(entity);
    }

    public void LogChatInterruption(EntityUid target, EntityUid interrupter)
    {
        if (!_focusedEntities.Contains(target) || target == interrupter || !Exists(interrupter))
            return;

        _adminLogger.Add(LogType.Chat, LogImpact.Medium,
            $"{ToPrettyString(target):target}'s open chat was interrupted by {ToPrettyString(interrupter):user} after taking damage.");
    }

    private void OnChatInputFocusChanged(ChatInputFocusChangedEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (ev.IsFocused)
        {
            if (_focusedSessions.ContainsKey(session))
                return;

            var attached = session.AttachedEntity is { } attachedUid && Exists(attachedUid) ? attachedUid : (EntityUid?) null;
            _focusedSessions.Add(session, attached);
            if (attached is { } focusedUid)
                _focusedEntities.Add(focusedUid);

            var entityInfo = attached is { } openedUid ? $" for {ToPrettyString(openedUid):entity}" : string.Empty;
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"{session:Player} opened the chat input{entityInfo}.");
            return;
        }

        if (!_focusedSessions.Remove(session, out var focusedEntity))
            return;

        if (focusedEntity is { } focused)
            _focusedEntities.Remove(focused);

        var focusedEntityInfo = focusedEntity is { } closedUid && Exists(closedUid) ? $" for {ToPrettyString(closedUid):entity}" : string.Empty;
        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"{session:Player} closed the chat input{focusedEntityInfo}.");
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (!_focusedSessions.TryGetValue(ev.Player, out var focusedEntity) || focusedEntity != ev.Entity)
            return;

        _focusedEntities.Remove(ev.Entity);
        _focusedSessions[ev.Player] = null;
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!_focusedSessions.TryGetValue(ev.Player, out var focusedEntity) || focusedEntity != null)
            return;

        _focusedSessions[ev.Player] = ev.Entity;
        _focusedEntities.Add(ev.Entity);
    }
}
