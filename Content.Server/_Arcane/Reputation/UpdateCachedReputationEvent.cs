// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Network;

namespace Content.Server._Arcane.Reputation;

[Serializable]
public sealed class UpdateCachedReputationEvent : EntityEventArgs
{
    public NetUserId Player;

    public UpdateCachedReputationEvent(NetUserId player)
    {
        Player = player;
    }
}
