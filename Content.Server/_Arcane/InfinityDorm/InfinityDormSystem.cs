// MIT License

// Copyright (c) 2026 Vecortys (vecortys@gmail.com)

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Content.Goobstation.Common.Effects;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Arcane.CCVars;
using Content.Shared._Arcane.InfinityDorm;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.Server._Arcane.InfinityDorm;

public sealed partial class InfinityDormSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SparksSystem _sparks = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    private int _maxUserDorms = 0;

    private int _step = 300;
    private float _lastPosition;
    private MapId _dormsMapId;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InfinityDormTeleporterComponent, InfinityDormTeleportMessage>(HandleTeleporterMessage);
        SubscribeNetworkEvent<RequestDormsAmountEvent>(HandleDormsAmountRequest);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);

        Subs.CVar(_cfg, ACCVars.MaxUserInfinityDorms, SetMaxUserDorms, true);
    }

    private void HandleTeleporterMessage(EntityUid uid, InfinityDormTeleporterComponent component, InfinityDormTeleportMessage args)
    {
        var station = _station.GetOwningStation(uid);
        if (station != null && _alertLevel.GetLevel(station.Value) != component.NeedAlertLevel)
        {
            _chat.TrySendInGameICMessage(uid, Loc.GetString("infinity-dorm-warning-alert-level"), InGameICChatType.Speak, false);
            return;
        }

        EnsureDormsMap();

        if (!TryCreateDorm(uid, args.Actor, args.Room, args.Number))
            return;

        var accessReader = EnsureComp<AccessReaderComponent>(uid);
        _accessReader.LogAccess((uid, accessReader), args.Actor);

        _sparks.DoSparks(Transform(args.Actor).Coordinates, 3, 10);
        TeleportToDorm(args.Actor, args.Number);
        _sparks.DoSparks(Transform(args.Actor).Coordinates, 3, 10);
    }

    private void HandleDormsAmountRequest(RequestDormsAmountEvent args, EntitySessionEventArgs eventArgs)
    {
        var count = 0;
        var query = EntityQueryEnumerator<InfinityDormComponent>();

        while (query.MoveNext(out var _, out var comp))
        {
            if (comp.Creator == eventArgs.SenderSession.AttachedEntity)
                count++;
        }

        RaiseNetworkEvent(new UserDormCountMessage(count), eventArgs.SenderSession);
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        _dormsMapId = MapId.Nullspace;
        _lastPosition = -1000;
    }

    private void EnsureDormsMap()
    {
        if (_map.MapExists(_dormsMapId))
            return;

        _map.CreateMap(out _dormsMapId);
    }

    private void SetMaxUserDorms(int value)
    {
        _maxUserDorms = value;
    }

}
