using Content.Shared._Arcane.Radio;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Arcane.Radio;

[UsedImplicitly]
public sealed class HeadsetChannelBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private HeadsetChannelWindow? _window;

    public HeadsetChannelBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<HeadsetChannelWindow>();
        _window.ChannelMuteToggled += (frequency, muted) => SendMessage(new HeadsetChannelMuteMessage(frequency, muted));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not HeadsetChannelUiState update)
            return;

        _window?.UpdateState(update.Channels, update.MutedFrequencies);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _window?.Close();
        _window = null;
    }
}