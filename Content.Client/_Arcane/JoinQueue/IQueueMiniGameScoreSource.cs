namespace Content.Client._Arcane.JoinQueue;

public interface IQueueMiniGameScoreSource
{
    event Action<int>? ScoreChanged;
    event Action? BulletFired;
    event Action? EnemyKilled;
    event Action<bool>? GameEnded; // true = victory

    int Score { get; }
    int Lives { get; }
    int Wave { get; }
}
