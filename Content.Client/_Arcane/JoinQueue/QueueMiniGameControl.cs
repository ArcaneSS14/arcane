using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Arcane.JoinQueue;

public abstract class QueueMiniGameControl : Control, IQueueMiniGameScoreSource
{
    [Dependency] private readonly IInputManager _input = default!;

    private bool _wasSpace;
    private bool _started;
    private bool _reportedFinalScore;

    public event Action<int>? ScoreChanged;
    public event Action? BulletFired;
    public event Action? EnemyKilled;
    public event Action<bool>? GameEnded;

    public abstract int Score { get; }
    public abstract int Lives { get; }
    public abstract int Wave { get; }

    protected bool Started => _started;

    protected abstract bool IsGameFinished { get; }

    protected abstract void ResetGame();

    protected abstract void UpdateRunningGame(FrameEventArgs args, bool space);

    protected virtual void UpdateIdleGame(FrameEventArgs args)
    {
    }

    protected virtual void UpdatePostGame(FrameEventArgs args)
    {
    }

    protected virtual void UpdateSharedGame(FrameEventArgs args)
    {
    }

    protected bool IsKeyDown(Keyboard.Key key)
        => _input.IsKeyDown(key);

    protected void RaiseBulletFired()
        => BulletFired?.Invoke();

    protected void RaiseEnemyKilled()
        => EnemyKilled?.Invoke();

    protected void RaiseGameEnded(bool victory)
        => GameEnded?.Invoke(victory);

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!VisibleInTree)
            return;

        var space = IsKeyDown(Keyboard.Key.Space);
        var pressedSpace = space && !_wasSpace;

        if (!_started)
        {
            UpdateIdleGame(args);
            if (pressedSpace)
                StartOrResetGame();
        }
        else if (IsGameFinished)
        {
            UpdatePostGame(args);
            if (pressedSpace)
                StartOrResetGame();
        }
        else
        {
            UpdateRunningGame(args, space);
        }

        UpdateSharedGame(args);
        ReportFinalScore();
        _wasSpace = space;
    }

    private void StartOrResetGame()
    {
        _started = true;
        _reportedFinalScore = false;
        ResetGame();
    }

    private void ReportFinalScore()
    {
        if (_reportedFinalScore || !IsGameFinished)
            return;

        _reportedFinalScore = true;
        ScoreChanged?.Invoke(Score);
    }
}
