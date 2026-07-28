// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Redial;
using Robust.Client;

namespace Content.Goobstation.Client.Redial;

public sealed class RedialManager : SharedRedialManager
{
    public override void Initialize()
    {
        _netManager.RegisterNetMessage<MsgRedial>(RedialOnMessage);
    }

    private void RedialOnMessage(MsgRedial message)
        => IoCManager.Resolve<IGameController>().Redial(message.Address);
}