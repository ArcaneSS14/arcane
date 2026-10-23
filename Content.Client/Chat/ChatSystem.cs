// SPDX-License-Identifier: MIT

using Content.Shared.Chat;
using Content.Shared._Arcane.Chat; // Arcane

namespace Content.Client.Chat;

public sealed class ChatSystem : SharedChatSystem
{
    // Arcane-Start
    public void NotifyChatInputFocusChanged(bool isFocused)
    {
        RaiseNetworkEvent(new ChatInputFocusChangedEvent(isFocused));
    }
    // Arcane-End
}
