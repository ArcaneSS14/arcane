// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.Chat;

[Serializable, NetSerializable]
public sealed class ChatInputFocusChangedEvent : EntityEventArgs
{
    public readonly bool IsFocused;

    public ChatInputFocusChangedEvent(bool isFocused)
    {
        IsFocused = isFocused;
    }
}
