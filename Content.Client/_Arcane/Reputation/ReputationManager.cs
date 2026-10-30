// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Arcane.Reputation;
using Robust.Shared.Network;

namespace Content.Client._Arcane.Reputation;

public sealed class ReputationManager
{
    [Dependency] private readonly IClientNetManager _netMgr = default!;

    private ReputationInfo? _info;

    public void Initialize()
    {
        _netMgr.RegisterNetMessage<ReputationNetMsg>(msg => _info = msg.Info);
    }

    public bool TryGetInfo([NotNullWhen(true)] out float? value)
    {
        value = _info?.Value;
        return _info != null;
    }
}
