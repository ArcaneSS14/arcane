namespace Content.Client._Arcane.JoinQueue.SpaceInvaders;

/// <summary>
/// Pure client-side logic for the Space Invaders queue mini-game.
/// </summary>
public sealed class SpaceInvadersGame
{
    public const float GameW = 560f;
    public const float GameH = 380f;
    public const float GroundY = GameH - 26f;
    public const float PlayerW = 30f;
    public const float PlayerH = 14f;
    public const float CrawlerW = 22f;
    public const float CrawlerH = 16f;

    private const float PlayerSpeed = 205f;
    private const float PlayerBulletSpeed = 330f;
    private const float EnemyBulletSpeed = 155f;
    private const float ShootCooldown = 0.42f;
    private const int MaxPlayerBullets = 2;
    private const float CrawlerPadX = 9f;
    private const float CrawlerPadY = 9f;
    private const int CrawlerCols = 9;
    private const int CrawlerRows = 4;

    public readonly List<Bullet> Bullets = [];
    public readonly List<Crawler> Crawlers = [];

    public float PlayerX { get; private set; }
    public float PlayerY => GroundY - PlayerH;
    public float CoreX { get; private set; }
    public float CoreY { get; private set; }
    public int CoreHealth { get; private set; }
    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int Wave { get; private set; }
    public bool GameOver { get; private set; }
    public bool Victory { get; private set; }
    public float CrawlerPulse { get; private set; }

    public Action? OnShoot;
    public Action<float, float>? OnEnemyKilled;
    public Action? OnGameOver;
    public Action? OnVictory;

    private float _shootTimer;
    private float _crawlerStepTimer;
    private float _crawlerShootTimer;
    private float _coreDrift;
    private int _crawlerDirection = 1;
    private readonly Random _rng = new();

    public void Reset()
    {
        PlayerX = GameW / 2f - PlayerW / 2f;
        Score = 0;
        Lives = 3;
        Wave = 0;
        GameOver = false;
        Victory = false;
        Bullets.Clear();
        StartWave();
    }

    public void Update(float frameTime, bool moveLeft, bool moveRight, bool shoot)
    {
        if (GameOver || Victory)
            return;

        if (moveLeft)
            PlayerX = Math.Max(4f, PlayerX - PlayerSpeed * frameTime);
        if (moveRight)
            PlayerX = Math.Min(GameW - PlayerW - 4f, PlayerX + PlayerSpeed * frameTime);

        _shootTimer -= frameTime;
        if (shoot && _shootTimer <= 0f)
        {
            var playerBullets = 0;
            foreach (var bullet in Bullets)
            {
                if (bullet.IsPlayer)
                    playerBullets++;
            }

            if (playerBullets < MaxPlayerBullets)
            {
                Bullets.Add(new Bullet(PlayerX + PlayerW / 2f - 2f, PlayerY - 8f, 0f, -PlayerBulletSpeed, true));
                _shootTimer = ShootCooldown;
                OnShoot?.Invoke();
            }
        }

        CrawlerPulse += frameTime * 8f;
        _coreDrift += frameTime;
        CoreX = GameW / 2f - 22f + MathF.Sin(_coreDrift * 1.4f) * (28f + Wave * 2f);

        MoveCrawlers(frameTime);
        ShootCrawlerBullet(frameTime);
        MoveBullets(frameTime);

        if (Crawlers.Count == 0 && CoreHealth <= 0)
        {
            if (Wave >= 7)
            {
                Victory = true;
                OnVictory?.Invoke();
            }
            else
                StartWave();
        }
    }

    private void StartWave()
    {
        Wave++;
        CoreHealth = 7 + Wave * 3;
        CoreX = GameW / 2f - 22f;
        CoreY = 30f;
        _coreDrift = 0f;
        _crawlerDirection = 1;
        _crawlerStepTimer = 0.45f;
        _crawlerShootTimer = 0.55f;
        _shootTimer = 0f;
        Crawlers.Clear();
        Bullets.Clear();

        BuildWaveFormation();
    }

    private void BuildWaveFormation()
    {
        var pattern = (Wave - 1) % 5;
        switch (pattern)
        {
            case 0:
                BuildGridFormation();
                break;
            case 1:
                BuildVFormation();
                break;
            case 2:
                BuildColumnFormation();
                break;
            case 3:
                BuildDiamondFormation();
                break;
            default:
                BuildGuardFormation();
                break;
        }
    }

    private void BuildGridFormation()
    {
        for (var row = 0; row < CrawlerRows; row++)
        for (var col = 0; col < CrawlerCols; col++)
            AddCrawler(col, row, col, row);
    }

    private void BuildVFormation()
    {
        for (var row = 0; row < 5; row++)
        {
            AddCrawler(4 - row, row, row, row);
            if (row != 0)
                AddCrawler(4 + row, row, row + 4, row);
        }
    }

    private void BuildColumnFormation()
    {
        for (var row = 0; row < 5; row++)
        {
            AddCrawler(1, row, 0, row);
            AddCrawler(3, row, 1, row);
            AddCrawler(5, row, 2, row);
            AddCrawler(7, row, 3, row);
        }
    }

    private void BuildDiamondFormation()
    {
        var widths = new[] { 1, 3, 5, 3, 1 };
        for (var row = 0; row < widths.Length; row++)
        {
            var width = widths[row];
            var start = 4 - width / 2;
            for (var i = 0; i < width; i++)
                AddCrawler(start + i, row, i, row);
        }
    }

    private void BuildGuardFormation()
    {
        for (var col = 0; col < CrawlerCols; col++)
        {
            if (col is 3 or 4 or 5)
                continue;

            AddCrawler(col, 0, col, 0);
            AddCrawler(col, 2, col, 2);
        }

        for (var row = 1; row < 5; row++)
        {
            AddCrawler(2, row, 2, row);
            AddCrawler(6, row, 6, row);
        }
    }

    private void AddCrawler(int col, int row, int seedCol, int seedRow)
    {
        var startX = (GameW - CrawlerCols * (CrawlerW + CrawlerPadX) + CrawlerPadX) / 2f;
        var startY = 70f;
        Crawlers.Add(new Crawler
        {
            X = startX + col * (CrawlerW + CrawlerPadX),
            Y = startY + row * (CrawlerH + CrawlerPadY),
            Type = (seedRow + seedCol) % 3,
            SpawnAge = (seedRow + seedCol) * -0.035f,
        });
    }

    private void MoveCrawlers(float frameTime)
    {
        foreach (var crawler in Crawlers)
            crawler.SpawnAge += frameTime;

        _crawlerStepTimer -= frameTime;
        if (_crawlerStepTimer > 0f)
            return;

        _crawlerStepTimer = Math.Max(0.055f, 0.40f - Wave * 0.05f - (CrawlerRows * CrawlerCols - Crawlers.Count) * 0.008f);

        var edge = false;
        foreach (var crawler in Crawlers)
        {
            crawler.X += _crawlerDirection * (9f + Wave * 1.4f);
            crawler.LegFrame ^= 1;

            if (crawler.X <= 6f || crawler.X + CrawlerW >= GameW - 6f)
                edge = true;

            if (crawler.Y + CrawlerH >= PlayerY - 4f)
            {
                EndGame();
                return;
            }
        }

        if (!edge)
            return;

        _crawlerDirection *= -1;
        foreach (var crawler in Crawlers)
            crawler.Y += 14f + Wave * 1.5f;
    }

    private void ShootCrawlerBullet(float frameTime)
    {
        _crawlerShootTimer -= frameTime;
        if (_crawlerShootTimer > 0f)
            return;

        _crawlerShootTimer = Math.Max(0.24f, 0.95f - Wave * 0.09f) * (0.65f + _rng.NextSingle() * 0.55f);

        if (Crawlers.Count == 0)
        {
            FireCorePattern();
            return;
        }

        var shooter = Crawlers[_rng.Next(Crawlers.Count)];
        Bullets.Add(new Bullet(shooter.X + CrawlerW / 2f - 2f, shooter.Y + CrawlerH + 2f, 0f, EnemyBulletSpeed + Wave * 7f, false));
        if (Wave < 4 || Crawlers.Count < 4)
            return;

        var secondShooter = Crawlers[_rng.Next(Crawlers.Count)];
        if (secondShooter != shooter)
            Bullets.Add(new Bullet(secondShooter.X + CrawlerW / 2f - 2f, secondShooter.Y + CrawlerH + 2f, 0f, EnemyBulletSpeed + Wave * 6f, false));
    }

    private void FireCorePattern()
    {
        var centerX = CoreX + 22f;
        var centerY = CoreY + 30f;
        var speed = EnemyBulletSpeed + Wave * 9f;
        var aimX = Math.Clamp((PlayerX + PlayerW / 2f - centerX) * 1.35f, -135f, 135f);

        switch ((Wave - 1) % 6)
        {
            case 0:
                AddEnemyBullet(centerX, centerY, 0f, speed);
                break;
            case 1:
                AddEnemyBullet(centerX, centerY, aimX, speed);
                break;
            case 2:
                AddEnemyBullet(centerX, centerY, -90f, speed * 0.95f);
                AddEnemyBullet(centerX, centerY, 0f, speed);
                AddEnemyBullet(centerX, centerY, 90f, speed * 0.95f);
                break;
            case 3:
                for (var i = -2; i <= 2; i++)
                    AddEnemyBullet(centerX + i * 18f, centerY, i * 28f, speed * 0.9f);
                break;
            case 4:
                AddEnemyBullet(centerX - 18f, centerY, 115f, speed);
                AddEnemyBullet(centerX + 18f, centerY, -115f, speed);
                AddEnemyBullet(centerX, centerY, aimX * 0.6f, speed * 0.92f);
                break;
            default:
                AddEnemyBullet(centerX, centerY, aimX, speed * 1.05f);
                AddEnemyBullet(centerX - 16f, centerY, -95f, speed * 0.9f);
                AddEnemyBullet(centerX + 16f, centerY, 95f, speed * 0.9f);
                AddEnemyBullet(centerX, centerY, 0f, speed * 0.82f);
                break;
        }
    }

    private void AddEnemyBullet(float x, float y, float vx, float vy)
    {
        Bullets.Add(new Bullet(x - 2f, y, vx, vy, false));
    }

    private void MoveBullets(float frameTime)
    {
        for (var i = Bullets.Count - 1; i >= 0; i--)
        {
            var bullet = Bullets[i];
            bullet.X += bullet.VX * frameTime;
            bullet.Y += bullet.VY * frameTime;

            if (bullet.Y < -16f || bullet.Y > GameH + 16f || bullet.X < -18f || bullet.X > GameW + 18f)
            {
                Bullets.RemoveAt(i);
                continue;
            }

            if (bullet.IsPlayer)
            {
                if (TryHitCrawler(bullet) || TryHitCore(bullet))
                    Bullets.RemoveAt(i);
            }
            else if (Overlaps(bullet.X, bullet.Y, 4f, 10f, PlayerX, PlayerY, PlayerW, PlayerH))
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

    private bool TryHitCrawler(Bullet bullet)
    {
        for (var i = Crawlers.Count - 1; i >= 0; i--)
        {
            var crawler = Crawlers[i];
            if (!Overlaps(bullet.X, bullet.Y, 4f, 10f, crawler.X, crawler.Y, CrawlerW, CrawlerH))
                continue;

            Score += 10 + crawler.Type * 5 + Wave * 2;
            OnEnemyKilled?.Invoke(crawler.X + CrawlerW / 2f, crawler.Y + CrawlerH / 2f);
            Crawlers.RemoveAt(i);
            return true;
        }

        return false;
    }

    private bool TryHitCore(Bullet bullet)
    {
        if (!Overlaps(bullet.X, bullet.Y, 4f, 10f, CoreX, CoreY, 44f, 28f))
            return false;

        CoreHealth--;
        Score += 25;
        OnEnemyKilled?.Invoke(CoreX + 22f, CoreY + 14f);
        return true;
    }

    private static bool Overlaps(float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh)
    {
        return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
    }

    public sealed class Crawler
    {
        public float X;
        public float Y;
        public float SpawnAge;
        public int Type;
        public int LegFrame;
    }

    public sealed class Bullet(float x, float y, float vx, float vy, bool isPlayer)
    {
        public float X = x;
        public float Y = y;
        public float VX = vx;
        public float VY = vy;
        public bool IsPlayer = isPlayer;
    }
}
