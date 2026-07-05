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

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.InfinityDorm;

[Serializable, NetSerializable]
public sealed class RequestDormsAmountEvent() : EntityEventArgs { }

[Serializable, NetSerializable]
public sealed class UserDormCountMessage(int amount) : EntityEventArgs
{
    public int Amount { get; } = amount;
}

[Serializable, NetSerializable]
public sealed partial class InfinityDormExitDoAfterEvent : SimpleDoAfterEvent { }

[Serializable, NetSerializable]
public sealed class InfinityDormTeleportMessage(string room, int number) : BoundUserInterfaceMessage
{
    public string Room { get; } = room;
    public int Number { get; } = number;
}
