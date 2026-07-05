namespace Content.Client._Arcane.JoinQueue.GoGoShitcurity;

/// <summary>
/// Pure game logic for Go Go Shitcurity queue mini-game.
/// Player on the left, enemies fly in from the right.
/// </summary>
public sealed class GoGoShitcurityGame
{
    public const float GameW = 560f;
    public const float GameH = 380f;

    public const float PlayerStartX = 28f;
    public const float PlayerSize = 32f;
    private const float PlayerMinX = 8f;
    private const float PlayerMaxX = GameW - PlayerSize - 8f;
    private const float PlayerMinY = 16f;
    private const float PlayerMaxY = GameH - PlayerSize - 8f;
    private const float PlayerSpeed = 190f;

    private const float BulletSpeed = 380f;
    private const float EnemyBulletSpeed = 210f;
    private const float BulletW = 14f;
    private const float BulletH = 5f;
    private const float ShootCooldown = 0.22f;
    private const float RapidFireCooldown = 0.11f;
    private const float RapidFireDuration = 5.5f;
    private const float SpreadShotDuration = 6f;
    private const float SlowFieldDuration = 4.8f;
    private const float SlowFieldFactor = 0.48f;

    public const float EnemySize = 32f;
    public const float PowerUpSize = 18f;
    private const float BaseEnemySpeed = 115f;

    // How many enemy types map to sprite pool entries (see GoGoShitcurityControl)
    public const int EnemyTypeCount = 6;

    public float PlayerX { get; private set; } = PlayerStartX;
    public float PlayerY { get; private set; } = GameH / 2f - PlayerSize / 2f;
    public int PlayerAnimFrame { get; private set; }
    public float PlayerAnimTimer { get; private set; }

    public readonly List<Enemy> Enemies = [];
    public readonly List<Bullet> Bullets = [];
    public readonly List<PowerUp> PowerUps = [];

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public float RapidFireTime { get; private set; }
    public float SpreadShotTime { get; private set; }
    public float SlowFieldTime { get; private set; }
    public int ShieldCharges { get; private set; }
    public bool GameOver { get; private set; }
    public bool Victory { get; private set; }

    private float _shootTimer;
    private float _enemyShootTimer;
    private float _spawnTimer;
    private float _spawnInterval;
    private int _waveEnemiesLeft;
    private int _waveNumber;
    private int _wavePattern;
    private int _spawnIndex;
    private float _wavePauseTimer;
    private bool _inWavePause;

    private readonly Random _rng = new();

    public Action? OnShoot;
    public Action<float, float>? OnEnemyKilled;
    public Action? OnGameOver;
    public Action? OnVictory;

    public void Reset()
    {
        PlayerX = PlayerStartX;
        PlayerY = GameH / 2f - PlayerSize / 2f;
        PlayerAnimFrame = 0;
        PlayerAnimTimer = 0f;
        Enemies.Clear();
        Bullets.Clear();
        PowerUps.Clear();
        Score = 0;
        Lives = 3;
        RapidFireTime = 0f;
        SpreadShotTime = 0f;
        SlowFieldTime = 0f;
        ShieldCharges = 0;
        GameOver = false;
        Victory = false;
        _shootTimer = 0f;
        _enemyShootTimer = 0.85f;
        _waveNumber = 0;
        _inWavePause = false;
        StartNextWave();
    }

    private void StartNextWave()
    {
        _waveNumber++;
        _wavePattern = (_waveNumber - 1) % 4;
        _spawnIndex = 0;
        _waveEnemiesLeft = 7 + _waveNumber * 4;
        _spawnInterval = Math.Max(0.22f, 0.92f - _waveNumber * 0.08f);
        _spawnTimer = 0.35f;
        _enemyShootTimer = Math.Min(_enemyShootTimer, 0.85f);
        _inWavePause = false;

    }

    public void Update(float dt, bool moveLeft, bool moveRight, bool moveUp, bool moveDown, bool shoot)
    {
        if (GameOver || Victory)
            return;

        if (moveLeft)
            PlayerX = Math.Max(PlayerMinX, PlayerX - PlayerSpeed * dt);
        if (moveRight)
            PlayerX = Math.Min(PlayerMaxX, PlayerX + PlayerSpeed * dt);
        if (moveUp)
            PlayerY = Math.Max(PlayerMinY, PlayerY - PlayerSpeed * dt);
        if (moveDown)
            PlayerY = Math.Min(PlayerMaxY, PlayerY + PlayerSpeed * dt);

        // Player animation (idle bob)
        PlayerAnimTimer += dt;
        if (PlayerAnimTimer >= 0.18f)
        {
            PlayerAnimTimer = 0f;
            PlayerAnimFrame = (PlayerAnimFrame + 1) % 4;
        }

        // Shooting
        RapidFireTime = Math.Max(0f, RapidFireTime - dt);
        SpreadShotTime = Math.Max(0f, SpreadShotTime - dt);
        SlowFieldTime = Math.Max(0f, SlowFieldTime - dt);
        _shootTimer -= dt;
        if (shoot && _shootTimer <= 0f)
        {
            FirePlayerBullets();
            _shootTimer = RapidFireTime > 0f ? RapidFireCooldown : ShootCooldown;
            OnShoot?.Invoke();
        }

        // Spawn enemies
        if (!_inWavePause)
        {
            _spawnTimer -= dt;
            if (_spawnTimer <= 0f && _waveEnemiesLeft > 0)
            {
                SpawnEnemy();
                _waveEnemiesLeft--;
                _spawnTimer = _spawnInterval;
            }
            else if (_waveEnemiesLeft == 0 && Enemies.Count == 0)
            {
                _inWavePause = true;
                _wavePauseTimer = 1.0f;
            }
        }
        else
        {
            _wavePauseTimer -= dt;
            if (_wavePauseTimer <= 0f)
                StartNextWave();
        }

        ShootEnemyBullet(dt);

        for (var i = Enemies.Count - 1; i >= 0; i--)
        {
            var e = Enemies[i];
            var speedFactor = SlowFieldTime > 0f ? SlowFieldFactor : 1f;
            e.X += e.VX * dt * speedFactor;
            e.Y += e.VY * dt * speedFactor;

            // Sine wave vertical drift
            e.SinePhase += e.SineFreq * dt * speedFactor;
            e.Y += MathF.Sin(e.SinePhase) * e.SineAmp * dt * speedFactor;
            e.Y = Math.Clamp(e.Y, 0f, GameH - EnemySize);

            // Animation
            e.AnimTimer += dt;
            if (e.AnimTimer >= 0.2f)
            {
                e.AnimTimer = 0f;
                e.AnimFrame = (e.AnimFrame + 1) % 4;
            }

            // Enemy left the screen — lose a life
            if (e.X + EnemySize < -4f)
            {
                Enemies.RemoveAt(i);
                Lives--;
                if (Lives <= 0)
                {
                    EndGame();
                    return;
                }
                continue;
            }

            // Enemy hits player
            if (Overlaps(e.X, e.Y, EnemySize, EnemySize,
                    PlayerX, PlayerY, PlayerSize * 0.7f, PlayerSize * 0.8f))
            {
                Enemies.RemoveAt(i);
                LoseLife();
                if (Lives <= 0)
                {
                    EndGame();
                    return;
                }
            }
        }

        if (GameOver)
            return;

        MovePowerUps(dt);
        MoveBullets(dt);
    }

    private void FirePlayerBullets()
    {
        var x = PlayerX + PlayerSize - 2f;
        var y = PlayerY + PlayerSize / 2f - BulletH / 2f;
        Bullets.Add(new Bullet
        {
            X = x,
            Y = y,
            VX = BulletSpeed,
            IsPlayer = true,
        });

        if (SpreadShotTime <= 0f)
            return;

        Bullets.Add(new Bullet
        {
            X = x,
            Y = y,
            VX = BulletSpeed * 0.92f,
            VY = -115f,
            IsPlayer = true,
        });
        Bullets.Add(new Bullet
        {
            X = x,
            Y = y,
            VX = BulletSpeed * 0.92f,
            VY = 115f,
            IsPlayer = true,
        });
    }

    private void ShootEnemyBullet(float dt)
    {
        _enemyShootTimer -= dt;
        if (_enemyShootTimer > 0f || Enemies.Count == 0)
            return;

        _enemyShootTimer = Math.Max(0.35f, 1.05f - _waveNumber * 0.065f) * (0.7f + _rng.NextSingle() * 0.5f);
        var enemy = Enemies[_rng.Next(Enemies.Count)];
        Bullets.Add(new Bullet
        {
            X = enemy.X,
            Y = enemy.Y + EnemySize / 2f - BulletH / 2f,
            VX = -EnemyBulletSpeed - _waveNumber * 8f,
            IsPlayer = false,
        });
    }

    private void MovePowerUps(float dt)
    {
        for (var i = PowerUps.Count - 1; i >= 0; i--)
        {
            var powerUp = PowerUps[i];
            var speedFactor = SlowFieldTime > 0f ? SlowFieldFactor : 1f;
            powerUp.X -= 72f * dt * speedFactor;
            powerUp.Pulse += dt * 7f;

            if (powerUp.X + PowerUpSize < -4f)
            {
                PowerUps.RemoveAt(i);
                continue;
            }

            if (!Overlaps(powerUp.X, powerUp.Y, PowerUpSize, PowerUpSize,
                    PlayerX, PlayerY, PlayerSize * 0.8f, PlayerSize * 0.8f))
                continue;

            ApplyPowerUp(powerUp.Type);
            Score += 50;
            PowerUps.RemoveAt(i);
        }
    }

    private void ApplyPowerUp(PowerUpKind type)
    {
        switch (type)
        {
            case PowerUpKind.RapidFire:
                RapidFireTime = RapidFireDuration;
                break;
            case PowerUpKind.Shield:
                ShieldCharges = Math.Min(2, ShieldCharges + 1);
                break;
            case PowerUpKind.SpreadShot:
                SpreadShotTime = SpreadShotDuration;
                break;
            case PowerUpKind.SlowField:
                SlowFieldTime = SlowFieldDuration;
                break;
            case PowerUpKind.Heal:
                Lives = Math.Min(3, Lives + 1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private void MoveBullets(float dt)
    {
        for (var i = Bullets.Count - 1; i >= 0; i--)
        {
            var b = Bullets[i];
            var speedFactor = !b.IsPlayer && SlowFieldTime > 0f ? SlowFieldFactor : 1f;
            b.X += b.VX * dt * speedFactor;
            b.Y += b.VY * dt * speedFactor;

            if (b.X > GameW + 8f || b.X < -16f || b.Y < -16f || b.Y > GameH + 16f)
            {
                Bullets.RemoveAt(i);
                continue;
            }

            if (b.IsPlayer)
            {
                var hit = false;
                for (var j = Enemies.Count - 1; j >= 0 && !hit; j--)
                {
                    var e = Enemies[j];
                    if (!Overlaps(b.X, b.Y, BulletW, BulletH, e.X + 4f, e.Y + 4f, EnemySize - 8f, EnemySize - 8f))
                        continue;

                    Enemies.RemoveAt(j);
                    TrySpawnPowerUp(e.X + EnemySize / 2f, e.Y + EnemySize / 2f);
                    Bullets.RemoveAt(i);
                    Score += 10 * _waveNumber;
                    OnEnemyKilled?.Invoke(e.X + EnemySize / 2f, e.Y + EnemySize / 2f);
                    hit = true;
                }
            }
            else if (Overlaps(b.X, b.Y, BulletW, BulletH,
                         PlayerX, PlayerY, PlayerSize * 0.76f, PlayerSize * 0.76f))
            {
                Bullets.RemoveAt(i);
                LoseLife();
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

    private void LoseLife()
    {
        if (ShieldCharges > 0)
        {
            ShieldCharges--;
            return;
        }

        Lives--;
    }

    private void TrySpawnPowerUp(float x, float y)
    {
        if (_rng.NextSingle() > 0.22f)
            return;

        PowerUps.Add(new PowerUp
        {
            X = x,
            Y = Math.Clamp(y - PowerUpSize / 2f, 8f, GameH - PowerUpSize - 8f),
            Type = PickPowerUpKind(),
        });
    }

    private PowerUpKind PickPowerUpKind()
    {
        var roll = _rng.NextSingle();
        return roll switch
        {
            < 0.28f => PowerUpKind.RapidFire,
            < 0.50f => PowerUpKind.SpreadShot,
            < 0.70f => PowerUpKind.SlowField,
            < 0.92f => PowerUpKind.Shield,
            _ => PowerUpKind.Heal,
        };
    }

    private void SpawnEnemy()
    {
        _spawnIndex++;
        var type = _rng.Next(EnemyTypeCount);
        var speed = BaseEnemySpeed + _waveNumber * 13f + _rng.NextSingle() * 34f;
        var spawnY = GetSpawnY();
        var xOffset = GetSpawnXOffset();

        Enemies.Add(new Enemy
        {
            X = GameW + 4f + xOffset,
            Y = spawnY,
            Type = type,
            VX = -speed,
            SineAmp = GetSineAmp(),
            SineFreq = 2f + _rng.NextSingle() * 3f,
            SinePhase = _spawnIndex * 0.65f,
        });
    }

    private float GetSpawnY()
    {
        var y = _wavePattern switch
        {
            0 => 36f + (_spawnIndex % 5) * 62f,
            1 => GameH / 2f - EnemySize / 2f + ((_spawnIndex % 2 == 0 ? 1f : -1f) * Math.Min(150f, _spawnIndex * 18f)),
            2 => _spawnIndex % 2 == 0 ? 42f + (_spawnIndex % 4) * 26f : GameH - 78f - (_spawnIndex % 4) * 26f,
            _ => _rng.NextSingle() * (GameH - EnemySize - 16f) + 8f,
        };

        return Math.Clamp(y, 8f, GameH - EnemySize - 8f);
    }

    private float GetSpawnXOffset()
    {
        return _wavePattern switch
        {
            1 => (_spawnIndex % 3) * 26f,
            2 => (_spawnIndex % 2) * 42f,
            _ => 0f,
        };
    }

    private float GetSineAmp()
    {
        return _wavePattern switch
        {
            0 => 10f,
            1 => 18f,
            2 => 8f,
            _ => 18f + _rng.NextSingle() * 34f,
        };
    }

    private static bool Overlaps(float ax, float ay, float aw, float ah,
        float bx, float by, float bw, float bh)
    {
        return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
    }

    public int WaveNumber => _waveNumber;

    public sealed class Enemy
    {
        public float X, Y;
        public int Type;
        public float VX, VY;
        public float SinePhase, SineFreq, SineAmp;
        public int AnimFrame;
        public float AnimTimer;
    }

    public sealed class Bullet
    {
        public float X, Y;
        public float VX;
        public float VY;
        public bool IsPlayer;
    }

    public sealed class PowerUp
    {
        public float X, Y;
        public float Pulse;
        public PowerUpKind Type;
    }

    public enum PowerUpKind : byte
    {
        RapidFire,
        Shield,
        SpreadShot,
        SlowField,
        Heal,
    }
}
