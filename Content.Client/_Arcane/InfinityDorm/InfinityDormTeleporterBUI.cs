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
using Robust.Client.UserInterface;

namespace Content.Client._Arcane.InfinityDorm;

public sealed class InfinityDormTeleporterBUI : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entities = default!;
    private readonly InfinityDormSystem _infinityDorm;

    private InfinityDormTeleporterWindow? _menu;
    private bool _needDormsAmountRequest = true; // todo: найти нормальный вариант, потому что Open() вызывается единожды.

    public InfinityDormTeleporterBUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _infinityDorm = _entities.System<InfinityDormSystem>();
    }

    public void SetRoomsAvailable(int available)
    {
        _menu?.SetRoomsAvailable(available);
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<InfinityDormTeleporterWindow>();

        _menu.ConfirmPressed += () =>
        {
            Teleport();
            _menu.Close();
        };
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (message is OpenBoundInterfaceMessage && _needDormsAmountRequest)
        {
            _infinityDorm.SetDormsAmount(Owner);
            _needDormsAmountRequest = false;
        }

        _needDormsAmountRequest = message is CloseBoundInterfaceMessage ? true : _needDormsAmountRequest;
    }

    private void Teleport()
    {
        if (_menu == null || _menu.SelectedNumber == 0)
            return;

        SendMessage(new InfinityDormTeleportMessage(_menu.SelectedRoom, _menu.SelectedNumber));
    }
}
