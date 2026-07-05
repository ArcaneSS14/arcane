using System.Numerics;
using Content.Goobstation.Common.JoinQueue;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Arcane.AfkKick;

public sealed class AfkKickSystem : EntitySystem
{
    private static readonly TimeSpan KickDelay = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(10);

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IJoinQueueManager _joinQueue = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;

    private readonly Dictionary<ICommonSession, TimeSpan> _lobbySince = new();
    private readonly Dictionary<ICommonSession, GhostAfkState> _ghostStates = new();

    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        _ghostQuery = GetEntityQuery<GhostComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        _preferences.CharacterProfileSaved += OnCharacterProfileSaved;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        _preferences.CharacterProfileSaved -= OnCharacterProfileSaved;
        _lobbySince.Clear();
        _ghostStates.Clear();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            ClearSession(args.Session);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status == SessionStatus.Disconnected)
            {
                ClearSession(session);
                continue;
            }

            if (_joinQueue.IsQueued(session.UserId))
            {
                ClearSession(session);
                continue;
            }

            if (IsLobbySession(session))
            {
                UpdateLobbySession(session, now);
                continue;
            }

            if (session.Status == SessionStatus.InGame)
            {
                UpdateInGameSession(session, now);
                continue;
            }

            ClearSession(session);
        }
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent args)
    {
        _lobbySince[args.PlayerSession] = _timing.CurTime;
        _ghostStates.Remove(args.PlayerSession);
    }

    private void OnCharacterProfileSaved(CharacterProfileSavedEventArgs args)
    {
        if (!IsLobbySession(args.Session) ||
            _joinQueue.IsQueued(args.Session.UserId))
            return;

        _lobbySince[args.Session] = _timing.CurTime;
        _ghostStates.Remove(args.Session);
    }

    private static bool IsLobbySession(ICommonSession session)
    {
        return session.Status == SessionStatus.Connected ||
               session.Status == SessionStatus.InGame && session.AttachedEntity == null;
    }

    private void UpdateLobbySession(ICommonSession session, TimeSpan now)
    {
        _ghostStates.Remove(session);

        if (!_lobbySince.TryGetValue(session, out var lobbySince))
        {
            _lobbySince[session] = now;
            return;
        }

        if (now - lobbySince < KickDelay)
            return;

        KickSession(session, "afk-kick-reason-lobby");
    }

    private void UpdateInGameSession(ICommonSession session, TimeSpan now)
    {
        _lobbySince.Remove(session);

        if (session.AttachedEntity is not { } attached ||
            !_ghostQuery.HasComp(attached) ||
            !_xformQuery.TryGetComponent(attached, out var xform))
        {
            _ghostStates.Remove(session);
            return;
        }

        var position = xform.LocalPosition;
        if (!_ghostStates.TryGetValue(session, out var state) ||
            state.Ghost != attached ||
            state.Parent != xform.ParentUid)
        {
            _ghostStates[session] = new GhostAfkState(attached, xform.ParentUid, position, now);
            return;
        }

        if (!position.EqualsApprox(state.Position))
        {
            state.Position = position;
            state.LastMoved = now;
            return;
        }

        if (now - state.LastMoved < KickDelay)
            return;

        KickSession(session, "afk-kick-reason-ghost");
    }

    private void KickSession(ICommonSession session, string reason)
    {
        _lobbySince.Remove(session);
        _ghostStates.Remove(session);

        if (!session.Channel.IsConnected)
            return;

        session.Channel.Disconnect(Loc.GetString(reason));
    }

    private void ClearSession(ICommonSession session)
    {
        _lobbySince.Remove(session);
        _ghostStates.Remove(session);
    }

    private sealed class GhostAfkState(EntityUid ghost, EntityUid parent, Vector2 position, TimeSpan lastMoved)
    {
        public readonly EntityUid Ghost = ghost;
        public readonly EntityUid Parent = parent;
        public Vector2 Position = position;
        public TimeSpan LastMoved = lastMoved;
    }
}
