namespace Content.Client._Arcane.JoinQueue.Gyruss;

/// <summary>
/// Pure game logic for the Gyruss queue mini-game. No UI dependencies.
/// </summary>
public sealed class GyrussGame
{
    public const float GameW = 560f;
    public const float GameH = 380f;
    public const float CenterX = GameW / 2f;
    public const float CenterY = GameH / 2f;
    public const float PlayerRadius = 158f;
    public const float InnerRadius = 18f;

    private const float PlayerAngularSpeed = 3.5f;
    private const float PlayerShootCooldown = 0.24f;
    private const float PlayerBulletSpeed = 280f;
    private const float EnemyBulletSpeed = 65f;
    private const float EnemyBaseSpeed = 18f;
    private const float EnemySpawnInterval = 0.8f;
    private const float EnemyShootInterval = 1.6f;
    private const float EnemyHitAngle = 0.16f;
    private const float EnemyHitRadius = 14f;
    private const float Tau = MathF.PI * 2f;

    public readonly List<Bullet> Bullets = [];
    public readonly List<Enemy> Enemies = [];

    public float PlayerAngle { get; private set; }
    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int Wave { get; private set; }
    public bool GameOver { get; private set; }
    public bool Victory { get; private set; }

    public Action? OnShoot;
    public Action<float, float>? OnEnemyKilled;
    public Action? OnGameOver;
    public Action? OnVictory;

    private int _waveEnemiesLeft;
    private int _wavePattern;
    private int _spawnIndex;
    private float _shootTimer;
    private float _spawnTimer;
    private float _enemyShootTimer;
    private readonly Random _rng = new();

    public void Reset()
    {
        PlayerAngle = -MathF.PI / 2f;
        Score = 0;
        Lives = 3;
        Wave = 0;
        GameOver = false;
        Victory = false;
        Bullets.Clear();
        Enemies.Clear();
        StartNextWave();
    }

    public void Update(float dt, bool moveLeft, bool moveRight, bool shoot)
    {
        if (GameOver || Victory)
            return;

        if (moveLeft)
            PlayerAngle -= PlayerAngularSpeed * dt;
        if (moveRight)
            PlayerAngle += PlayerAngularSpeed * dt;
        PlayerAngle = NormalizeAngle(PlayerAngle);

        _shootTimer -= dt;
        if (shoot && _shootTimer <= 0f)
        {
            Bullets.Add(new Bullet(PlayerAngle, PlayerRadius - 10f, -PlayerBulletSpeed, true));
            _shootTimer = PlayerShootCooldown;
            OnShoot?.Invoke();
        }

        SpawnEnemies(dt);
        FireEnemyBullets(dt);
        MoveEnemies(dt);
        MoveBullets(dt);

        if (_waveEnemiesLeft <= 0 && Enemies.Count == 0)
        {
            StartNextWave();
        }
    }

    private void StartNextWave()
    {
        Wave++;
        _waveEnemiesLeft = 5 + Wave * 3;
        _wavePattern = (Wave - 1) % 4;
        _spawnIndex = 0;
        _spawnTimer = 0.35f;
        _enemyShootTimer = EnemyShootInterval;
        Bullets.Clear();
    }

    private void SpawnEnemies(float dt)
    {
        if (_waveEnemiesLeft <= 0)
            return;

        _spawnTimer -= dt;
        if (_spawnTimer > 0f)
            return;

        _spawnTimer = Math.Max(0.22f, EnemySpawnInterval - Wave * 0.07f);
        _waveEnemiesLeft--;
        _spawnIndex++;

        var angle = GetSpawnAngle();
        var angularSpeed = GetSpawnAngularSpeed();
        Enemies.Add(new Enemy
        {
            Angle = angle,
            Radius = InnerRadius,
            RadialSpeed = EnemyBaseSpeed + Wave * 5f + _rng.NextSingle() * 12f,
            AngularSpeed = angularSpeed,
            Kind = (_spawnIndex + _wavePattern) % 3,
        });
    }

    private float GetSpawnAngle()
    {
        return _wavePattern switch
        {
            0 => Tau * (_spawnIndex % 8) / 8f,
            1 => NormalizeAngle(_spawnIndex * 0.78f + Wave * 0.35f),
            2 => NormalizeAngle((_spawnIndex % 2) * MathF.PI + _spawnIndex / 2 * 0.18f),
            _ => NormalizeAngle(_rng.NextSingle() * Tau),
        };
    }

    private float GetSpawnAngularSpeed()
    {
        return _wavePattern switch
        {
            0 => (_spawnIndex % 2 == 0 ? 1f : -1f) * (0.18f + Wave * 0.02f),
            1 => 0.34f + Wave * 0.035f,
            2 => (_spawnIndex % 2 == 0 ? 1f : -1f) * (0.12f + Wave * 0.025f),
            _ => (_rng.NextSingle() - 0.5f) * (0.55f + Wave * 0.04f),
        };
    }

    private void FireEnemyBullets(float dt)
    {
        _enemyShootTimer -= dt;
        if (_enemyShootTimer > 0f || Enemies.Count == 0)
            return;

        _enemyShootTimer = Math.Max(0.45f, EnemyShootInterval - Wave * 0.08f) * (0.75f + _rng.NextSingle() * 0.5f);
        var enemy = Enemies[_rng.Next(Enemies.Count)];
        Bullets.Add(new Bullet(enemy.Angle, enemy.Radius + 4f, EnemyBulletSpeed + Wave * 5f, false));
    }

    private void MoveEnemies(float dt)
    {
        for (var i = Enemies.Count - 1; i >= 0; i--)
        {
            var enemy = Enemies[i];
            enemy.Radius += enemy.RadialSpeed * dt;
            enemy.Angle = NormalizeAngle(enemy.Angle + enemy.AngularSpeed * dt);
            enemy.Pulse += dt * 8f;

            if (enemy.Radius < PlayerRadius - 12f)
                continue;

            Enemies.RemoveAt(i);
            Lives--;
            if (Lives <= 0)
            {
                EndGame();
                return;
            }
        }
    }

    private void MoveBullets(float dt)
    {
        for (var i = Bullets.Count - 1; i >= 0; i--)
        {
            var bullet = Bullets[i];
            bullet.Radius += bullet.RadialSpeed * dt;

            if (bullet.Radius < InnerRadius - 12f || bullet.Radius > PlayerRadius + 18f)
            {
                Bullets.RemoveAt(i);
                continue;
            }

            if (bullet.IsPlayer)
            {
                if (TryHitEnemy(bullet))
                    Bullets.RemoveAt(i);
            }
            else if (MathF.Abs(AngleDelta(bullet.Angle, PlayerAngle)) < 0.13f &&
                     MathF.Abs(bullet.Radius - PlayerRadius) < 10f)
            {
                Bullets.RemoveAt(i);
                Lives--;
                if (Lives <= 0)
                {
                    EndGame();
                    return;
                }
            }
        }
    }

    private void EndGame()
    {
        if (GameOver)
            return;

        GameOver = true;
        OnGameOver?.Invoke();
    }

    private bool TryHitEnemy(Bullet bullet)
    {
        for (var i = Enemies.Count - 1; i >= 0; i--)
        {
            var enemy = Enemies[i];
            if (MathF.Abs(AngleDelta(bullet.Angle, enemy.Angle)) > EnemyHitAngle ||
                MathF.Abs(bullet.Radius - enemy.Radius) > EnemyHitRadius)
                continue;

            Score += 20 + Wave * 8 + enemy.Kind * 5;
            var point = PolarToPoint(enemy.Angle, enemy.Radius);
            OnEnemyKilled?.Invoke(point.X, point.Y);
            Enemies.RemoveAt(i);
            return true;
        }

        return false;
    }

    public static (float X, float Y) PolarToPoint(float angle, float radius)
    {
        return (CenterX + MathF.Cos(angle) * radius, CenterY + MathF.Sin(angle) * radius);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= Tau;
        return angle < 0f ? angle + Tau : angle;
    }

    private static float AngleDelta(float a, float b)
    {
        var delta = (a - b + MathF.PI) % Tau;
        if (delta < 0f)
            delta += Tau;
        return delta - MathF.PI;
    }

    public sealed class Enemy
    {
        public float Angle;
        public float Radius;
        public float RadialSpeed;
        public float AngularSpeed;
        public float Pulse;
        public int Kind;
    }

    public sealed class Bullet(float angle, float radius, float radialSpeed, bool isPlayer)
    {
        public float Angle = angle;
        public float Radius = radius;
        public float RadialSpeed = radialSpeed;
        public bool IsPlayer = isPlayer;
    }
}
