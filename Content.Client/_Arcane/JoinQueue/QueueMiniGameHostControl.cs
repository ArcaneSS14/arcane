using Content.Client._Arcane.JoinQueue.GoGoShitcurity;
using Content.Client._Arcane.JoinQueue.Gyruss;
using Content.Client._Arcane.JoinQueue.SpaceInvaders;
using Content.Shared._Arcane.JoinQueue;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client._Arcane.JoinQueue;

public sealed class QueueMiniGameHostControl : BoxContainer
{
    [Dependency] private readonly IClientNetManager _net = default!;
    [Dependency] private readonly IAudioManager _audioManager = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    private static readonly Random Random = new();
    private static readonly QueueMiniGameKind[] MiniGameKinds = Enum.GetValues<QueueMiniGameKind>();
    private static readonly Vector2 GameCanvasSize = new(
        MathF.Max(GyrussGame.GameW, MathF.Max(GoGoShitcurityGame.GameW, SpaceInvadersGame.GameW)),
        MathF.Max(GyrussGame.GameH, MathF.Max(GoGoShitcurityGame.GameH, SpaceInvadersGame.GameH)));

    private const float MinLoadSeconds = 3f;
    private const float MaxLoadSeconds = 5f;
    private const float MusicGain = 0.35f;
    private const float EffectGain = 0.55f;

    private readonly Control _gameHolder;
    private readonly Button _gyrussButton;
    private readonly Button _goGoShitcurityButton;
    private readonly Button _spaceInvadersButton;
    private readonly Button _muteButton;
    private readonly Label _scoreLabel;
    private readonly Label _livesLabel;
    private readonly Label _waveLabel;

    private QueueMiniGameKind _currentGame;
    private QueueMiniGameKind? _pendingGame;
    private Control? _currentControl;
    private IQueueMiniGameScoreSource? _currentScoreSource;
    private int _currentBestScore;
    private int _lastHudScore = int.MinValue;
    private int _lastHudLives = int.MinValue;
    private int _lastHudWave = int.MinValue;
    private float _loadTimer;

    private IAudioSource? _musicSource;
    private IAudioSource?[] _shootSources = [];
    private IAudioSource?[] _killSources = [];
    private IAudioSource?[] _gameoverSources = [];
    private int _shootSourceIndex;
    private int _killSourceIndex;
    private int _gameoverSourceIndex;
    private bool _muted;

    public QueueMiniGameHostControl()
    {
        IoCManager.InjectDependencies(this);

        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 4;
        HorizontalAlignment = HAlignment.Center;

        // Game selector buttons
        var buttons = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            HorizontalAlignment = HAlignment.Center,
        };

        _gyrussButton = MakeButton("queue-minigame-gyruss", QueueMiniGameKind.Gyruss);
        _goGoShitcurityButton = MakeButton("queue-minigame-gogo-shitcurity", QueueMiniGameKind.GoGoShitcurity);
        _spaceInvadersButton = MakeButton("queue-minigame-space-invaders", QueueMiniGameKind.SpaceInvaders);

        buttons.AddChild(_gyrussButton);
        buttons.AddChild(_goGoShitcurityButton);
        buttons.AddChild(_spaceInvadersButton);
        AddChild(buttons);

        // HUD bar: Score | Wave | Lives | [Mute]
        var hudBar = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 0,
            MinWidth = GameCanvasSize.X,
            Margin = new Thickness(0, 2, 0, 0),
        };

        _scoreLabel = new Label
        {
            HorizontalExpand = true,
            StyleClasses = { "LabelKeyText" },
            Align = Label.AlignMode.Left,
            Margin = new Thickness(6, 0, 0, 0),
        };
        _waveLabel = new Label
        {
            HorizontalExpand = true,
            StyleClasses = { "LabelKeyText" },
            Align = Label.AlignMode.Center,
        };
        _livesLabel = new Label
        {
            HorizontalExpand = true,
            StyleClasses = { "LabelKeyText" },
            Align = Label.AlignMode.Right,
        };

        _muteButton = new Button
        {
            Text = Loc.GetString("queue-minigame-mute"),
            MinWidth = 32f,
            Margin = new Thickness(4, 0, 4, 0),
        };
        _muteButton.OnPressed += _ => ToggleMute();

        hudBar.AddChild(_scoreLabel);
        hudBar.AddChild(_waveLabel);
        hudBar.AddChild(_livesLabel);
        hudBar.AddChild(_muteButton);
        AddChild(hudBar);

        // Game canvas
        _gameHolder = new Control
        {
            MinSize = GameCanvasSize,
            HorizontalAlignment = HAlignment.Center,
        };
        AddChild(_gameHolder);

        UpdateHudLabels();
        LoadAudio();
        SetGame(MiniGameKinds[Random.Next(MiniGameKinds.Length)]);
    }

    private void LoadAudio()
    {
        try
        {
            var musicStream = _cache.GetResource<AudioResource>("/Audio/_Arcane/MiniGames/minigame_music.ogg").AudioStream;
            _musicSource = _audioManager.CreateAudioSource(musicStream);
            if (_musicSource != null)
            {
                _musicSource.Global = true;
                _musicSource.Looping = true;
                _musicSource.Gain = MusicGain;
                _musicSource.StartPlaying();
            }

            _shootSources = CreateEffectSources(
                _cache.GetResource<AudioResource>("/Audio/_Arcane/MiniGames/minigame_shoot.ogg").AudioStream,
                4,
                EffectGain);

            _killSources = CreateEffectSources(
                _cache.GetResource<AudioResource>("/Audio/_Arcane/MiniGames/minigame_kill.ogg").AudioStream,
                3,
                EffectGain);

            _gameoverSources = CreateEffectSources(
                _cache.GetResource<AudioResource>("/Audio/_Arcane/MiniGames/minigame_gameover.ogg").AudioStream,
                2,
                EffectGain + 0.1f);
        }
        catch (Exception ex)
        {
            // Audio is optional; if resources are missing, continue silently.
            Logger.GetSawmill("minigame.audio").Warning($"Failed to load mini-game audio: {ex}");
        }
    }

    private IAudioSource?[] CreateEffectSources(AudioStream stream, int count, float gain)
    {
        var sources = new IAudioSource?[count];
        for (var i = 0; i < sources.Length; i++)
        {
            sources[i] = _audioManager.CreateAudioSource(stream);
            if (sources[i] == null)
                continue;

            sources[i]!.Global = true;
            sources[i]!.Gain = gain;
        }

        return sources;
    }

    private void ToggleMute()
    {
        _muted = !_muted;
        _muteButton.Text = Loc.GetString(_muted ? "queue-minigame-unmute" : "queue-minigame-mute");
        if (_musicSource != null)
            _musicSource.Gain = _muted ? 0f : MusicGain;
    }

    private void PlayEffect(IAudioSource?[] sources, ref int sourceIndex)
    {
        if (_muted || sources.Length == 0)
            return;

        var source = sources[sourceIndex];
        sourceIndex = (sourceIndex + 1) % sources.Length;
        if (source == null)
            return;

        source.Restart();
        source.StartPlaying();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Poll game state for HUD
        if (_currentScoreSource != null)
            UpdateHudLabels();

        if (_pendingGame == null)
            return;

        _loadTimer -= args.DeltaSeconds;
        if (_loadTimer > 0f)
            return;

        var nextGame = _pendingGame.Value;
        _pendingGame = null;
        SetGame(nextGame);
    }

    private void UpdateHudLabels()
    {
        if (_currentScoreSource == null)
        {
            _lastHudScore = int.MinValue;
            _lastHudLives = int.MinValue;
            _lastHudWave = int.MinValue;
            _scoreLabel.Text = string.Empty;
            _waveLabel.Text = string.Empty;
            _livesLabel.Text = string.Empty;
            return;
        }

        if (_lastHudScore != _currentScoreSource.Score)
        {
            _lastHudScore = _currentScoreSource.Score;
            _scoreLabel.Text = Loc.GetString("queue-minigame-score", ("score", _lastHudScore));
        }

        if (_lastHudWave != _currentScoreSource.Wave)
        {
            _lastHudWave = _currentScoreSource.Wave;
            _waveLabel.Text = Loc.GetString("queue-minigame-wave", ("wave", _lastHudWave));
        }

        if (_lastHudLives != _currentScoreSource.Lives)
        {
            _lastHudLives = _currentScoreSource.Lives;
            _livesLabel.Text = Loc.GetString("queue-minigame-lives", ("lives", _lastHudLives));
        }
    }

    private Button MakeButton(string locId, QueueMiniGameKind kind)
    {
        var button = new Button
        {
            Text = Loc.GetString(locId),
        };
        button.OnPressed += _ => SelectGame(kind);
        return button;
    }

    private void SelectGame(QueueMiniGameKind kind)
    {
        if (_pendingGame != null || _currentGame == kind && _currentControl != null)
            return;

        _pendingGame = kind;
        _loadTimer = MinLoadSeconds + Random.NextSingle() * (MaxLoadSeconds - MinLoadSeconds);
        ClearCurrentControl();
        _currentControl = MakeLoadingLabel();
        _gameHolder.AddChild(_currentControl);
        SetButtonsDisabled(true);
    }

    private void SetGame(QueueMiniGameKind kind)
    {
        _currentGame = kind;
        ClearCurrentControl();
        _currentControl = kind switch
        {
            QueueMiniGameKind.Gyruss => new GyrussControl(),
            QueueMiniGameKind.GoGoShitcurity => new GoGoShitcurityControl(),
            QueueMiniGameKind.SpaceInvaders => new SpaceInvadersControl(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        _gameHolder.AddChild(_currentControl);
        if (_currentControl is IQueueMiniGameScoreSource scoreSource)
        {
            _currentScoreSource = scoreSource;
            _currentScoreSource.ScoreChanged += OnCurrentGameScoreChanged;
            _currentScoreSource.BulletFired += OnBulletFired;
            _currentScoreSource.EnemyKilled += OnEnemyKilled;
            _currentScoreSource.GameEnded += OnGameEnded;
            UpdateHudLabels();
        }

        UpdateButtonState();
    }

    private void ClearCurrentControl(bool orphan = true)
    {
        if (_currentScoreSource != null)
        {
            _currentScoreSource.ScoreChanged -= OnCurrentGameScoreChanged;
            _currentScoreSource.BulletFired -= OnBulletFired;
            _currentScoreSource.EnemyKilled -= OnEnemyKilled;
            _currentScoreSource.GameEnded -= OnGameEnded;
            _currentScoreSource = null;
        }

        _currentBestScore = 0;
        if (orphan)
            _currentControl?.Orphan();
        _currentControl = null;
        UpdateHudLabels();
    }

    private void OnCurrentGameScoreChanged(int score)
    {
        if (score <= _currentBestScore)
            return;

        _currentBestScore = score;
        _net.ClientSendMessage(new QueueMiniGameScoreMessage
        {
            Game = _currentGame,
            Score = score,
        });
    }

    private void OnBulletFired() => PlayEffect(_shootSources, ref _shootSourceIndex);
    private void OnEnemyKilled() => PlayEffect(_killSources, ref _killSourceIndex);
    private void OnGameEnded(bool victory) => PlayEffect(_gameoverSources, ref _gameoverSourceIndex);

    protected override void ExitedTree()
    {
        ClearCurrentControl(false);
        base.ExitedTree();
        _musicSource?.StopPlaying();
        _musicSource?.Dispose();
        _musicSource = null;
        DisposeEffectSources(_shootSources);
        _shootSources = [];
        DisposeEffectSources(_killSources);
        _killSources = [];
        DisposeEffectSources(_gameoverSources);
        _gameoverSources = [];
    }

    private static void DisposeEffectSources(IAudioSource?[] sources)
    {
        foreach (var source in sources)
            source?.Dispose();
    }

    private static Label MakeLoadingLabel()
    {
        return new Label
        {
            Text = Loc.GetString("queue-minigame-loading"),
            Align = Label.AlignMode.Center,
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            MinSize = GameCanvasSize,
        };
    }

    private void UpdateButtonState()
    {
        _gyrussButton.Disabled = _currentGame == QueueMiniGameKind.Gyruss;
        _goGoShitcurityButton.Disabled = _currentGame == QueueMiniGameKind.GoGoShitcurity;
        _spaceInvadersButton.Disabled = _currentGame == QueueMiniGameKind.SpaceInvaders;
    }

    private void SetButtonsDisabled(bool disabled)
    {
        _gyrussButton.Disabled = disabled;
        _goGoShitcurityButton.Disabled = disabled;
        _spaceInvadersButton.Disabled = disabled;
    }
}
