// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Audio.Jukebox;
using Robust.Client.Audio;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Audio.Jukebox;

public sealed class JukeboxBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    [ViewVariables]
    private JukeboxMenu? _menu;

    public JukeboxBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<JukeboxMenu>();

        _menu.OnPlayPressed += args =>
        {
            if (args)
            {
                SendMessage(new JukeboxPlayingMessage());
            }
            else
            {
                SendMessage(new JukeboxPauseMessage());
            }
        };

        _menu.OnStopPressed += () =>
        {
            SendMessage(new JukeboxStopMessage());
        };

        // Orion-Start
        _menu.OnLoopToggled += () =>
        {
            SendMessage(new JukeboxToggleLoopMessage());
        };
        // Orion-End

        _menu.OnSongSelected += SelectSong;

        _menu.SetTime += SetTime;
        _menu.SetVolume += SetVolume; // Orion
        PopulateMusic();
        Reload();
    }

    /// <summary>
    /// Reloads the attached menu if it exists.
    /// </summary>
    public void Reload()
    {
        if (_menu == null || !EntMan.TryGetComponent(Owner, out JukeboxComponent? jukebox))
            return;

        // Arcane-Edit-Start
        _menu.SetAudioStream(EntMan.System<JukeboxSystem>().GetLocalStream(Owner));
        _menu.SetPlayPauseButton(jukebox.Playing, force: true);
        // Arcane-Edit-End
        _menu.SetVolumeSlider(jukebox.Volume); // Orion
        _menu.SetLoopButton(jukebox.LoopEnabled); // Orion

        if (_protoManager.Resolve(jukebox.SelectedSongId, out var songProto))
        {
            var length = EntMan.System<AudioSystem>().GetAudioLength(new ResolvedPathSpecifier(songProto.Path.Path)); // Arcane-Edit
            _menu.SetSelectedSong(songProto.Name, (float) length.TotalSeconds);
        }
        else
        {
            _menu.SetSelectedSong(string.Empty, 0f);
        }
    }

    public void PopulateMusic()
    {
        _menu?.Populate(_protoManager.EnumeratePrototypes<JukeboxPrototype>());
    }

    public void SelectSong(ProtoId<JukeboxPrototype> songid)
    {
        SendMessage(new JukeboxSelectedMessage(songid));
    }

    public void SetTime(float time)
    {
        var sentTime = time;

        // Arcane-Edit-Start
        if (EntMan.System<JukeboxSystem>().GetLocalStream(Owner) is { } stream &&
            EntMan.TryGetComponent(stream, out AudioComponent? audioComp))
        // Arcane-Edit-End
        {
            audioComp.PlaybackPosition = time;
        }

        SendMessage(new JukeboxSetTimeMessage(sentTime));
    }

// Arcane-Start
    public void SetVolume(float volume)
    {
        EntMan.System<JukeboxSystem>().ApplyLocalVolume(Owner, volume);

        SendMessage(new JukeboxSetVolumeMessage(volume));
    }
    // Arcane-End
}
