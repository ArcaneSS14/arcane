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

using Content.Shared._Arcane.InfinityDorm;
using Robust.Client.GameObjects;
using Robust.Shared.Player;

namespace Content.Client._Arcane.InfinityDorm;

public sealed partial class InfinityDormSystem : EntitySystem
{
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private EntityUid _teleporter;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<UserDormCountMessage>(HandleUserDormCountMessage);
    }

    public void SetDormsAmount(EntityUid teleporter)
    {
        _teleporter = teleporter;
        RaiseNetworkEvent(new RequestDormsAmountEvent());
    }

    private void HandleUserDormCountMessage(UserDormCountMessage args)
    {
        if (_player.LocalEntity == null)
            return;

        if (!_ui.TryGetOpenUi<InfinityDormTeleporterBUI>(_teleporter, InfinityDormTeleporterUiKey.Key, out var bui))
            return;

        bui.SetRoomsAvailable(args.Amount);
    }
}
