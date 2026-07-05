using Content.Client._Arcane.JoinQueue;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Client._Arcane.JoinQueue.GoGoShitcurity;

public sealed class GoGoShitcurityControl : QueueMiniGameControl
{
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly GoGoShitcurityGame _game = new();
    private readonly List<MiniGameParticle> _particles = [];
    private Font _font = default!;
    private Font _fontBig = default!;
    private SpriteSystem _sprites = default!;

    public override int Score => _game.Score;
    public override int Lives => _game.Lives;
    public override int Wave => _game.WaveNumber;

    protected override bool IsGameFinished => _game.GameOver || _game.Victory;

    // Player sprite
    private Texture _spacepodTexture = default!;

    // Enemy sprites mapped by type index (0..EnemyTypeCount-1)
    private readonly IRsiStateLike[] _enemySprites = new IRsiStateLike[GoGoShitcurityGame.EnemyTypeCount];

    private static readonly Color ColorBullet = new(255, 230, 80);
    private static readonly Color ColorEnemyBullet = new(255, 110, 90);
    private static readonly Color ColorRapidFire = new(255, 225, 70);
    private static readonly Color ColorShield = new(90, 185, 255);
    private static readonly Color ColorSpreadShot = new(255, 125, 225);
    private static readonly Color ColorSlowField = new(110, 255, 180);
    private static readonly Color ColorHeal = new(95, 255, 110);
    private static readonly Color ColorBg = new(0, 0, 15);

    // Stars for parallax background
    private readonly (float X, float Y, float Speed, float Size)[] _stars;
    private float _starScrollX;

    public GoGoShitcurityControl()
    {
        IoCManager.InjectDependencies(this);

        _sprites = _entMan.System<SpriteSystem>();
        _font = QueueMiniGameAssets.LoadRegularFont(_cache, 12);
        _fontBig = QueueMiniGameAssets.LoadBoldFont(_cache, 18);

        MinSize = new Vector2(GoGoShitcurityGame.GameW, GoGoShitcurityGame.GameH);
        RectClipContent = true;

        LoadSprites();

        _game.OnShoot = RaiseBulletFired;
        _game.OnEnemyKilled = (x, y) =>
        {
            QueueMiniGameDrawHelpers.AddBurst(_particles, x, y, new Color(255, 215, 85));
            RaiseEnemyKilled();
        };
        _game.OnGameOver = () => RaiseGameEnded(false);
        _game.OnVictory = () => RaiseGameEnded(true);

        var rng = new Random(42);
        _stars = new (float, float, float, float)[60];
        for (var i = 0; i < _stars.Length; i++)
            _stars[i] = (rng.NextSingle() * GoGoShitcurityGame.GameW,
                         rng.NextSingle() * GoGoShitcurityGame.GameH,
                         20f + rng.NextSingle() * 40f,
                         i < 20 ? 1f : i < 45 ? 1.5f : 2f);
    }

    private void LoadSprites()
    {
        _spacepodTexture = QueueMiniGameAssets.LoadSpacepodTexture(_cache);

        _enemySprites[0] = LoadState("Mobs/Aliens/Argocyte/argocyte_common.rsi", "crawler");
        _enemySprites[1] = LoadState("Mobs/Aliens/Argocyte/argocyte_common.rsi", "pouncer");
        _enemySprites[2] = LoadState("Mobs/Aliens/Argocyte/argocyte_common.rsi", "molder");
        _enemySprites[3] = LoadState("Mobs/Aliens/Argocyte/argocyte_common.rsi", "crawler");
        _enemySprites[4] = LoadState("Mobs/Aliens/Argocyte/argocyte_common.rsi", "pouncer");
        _enemySprites[5] = LoadState("Mobs/Aliens/Argocyte/argocyte_common.rsi", "molder");
    }

    private IRsiStateLike LoadState(string rsiPath, string stateName)
    {
        var spec = new SpriteSpecifier.Rsi(new ResPath(rsiPath), stateName);
        return _sprites.RsiStateLike(spec);
    }

    protected override void ResetGame()
    {
        _game.Reset();
    }

    protected override void UpdateRunningGame(FrameEventArgs args, bool space)
    {
        var left = IsKeyDown(Keyboard.Key.Left) || IsKeyDown(Keyboard.Key.A);
        var right = IsKeyDown(Keyboard.Key.Right) || IsKeyDown(Keyboard.Key.D);
        var up = IsKeyDown(Keyboard.Key.Up) || IsKeyDown(Keyboard.Key.W);
        var down = IsKeyDown(Keyboard.Key.Down) || IsKeyDown(Keyboard.Key.S);

        _game.Update(args.DeltaSeconds, left, right, up, down, space);
        _starScrollX = (_starScrollX + 35f * args.DeltaSeconds) % GoGoShitcurityGame.GameW;
    }

    protected override void UpdateIdleGame(FrameEventArgs args)
    {
        _starScrollX = (_starScrollX + 22f * args.DeltaSeconds) % GoGoShitcurityGame.GameW;
    }

    protected override void UpdateSharedGame(FrameEventArgs args)
    {
        QueueMiniGameDrawHelpers.UpdateParticles(_particles, args.DeltaSeconds);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var gw = GoGoShitcurityGame.GameW;
        var gh = GoGoShitcurityGame.GameH;

        handle.DrawRect(new UIBox2(0, 0, gw, gh), ColorBg);
        DrawStars(handle, gw, gh);

        if (Started)
            DrawEntities(handle);
        else
        {
            DrawPlayer(handle);
            DrawDemoRaid(handle);
        }

        DrawBullets(handle);
        DrawPowerUps(handle);
        QueueMiniGameDrawHelpers.DrawParticles(handle, _particles);
        DrawOverlay(handle, gw, gh);
        QueueMiniGameDrawHelpers.DrawArcadeFrame(handle, gw, gh, _starScrollX / 35f);
    }

    private void DrawStars(DrawingHandleScreen handle, float gw, float gh)
    {
        foreach (var (baseX, y, speed, size) in _stars)
        {
            var x = (baseX - _starScrollX * (speed / 60f) % gw + gw) % gw;
            var alpha = 0.3f + size * 0.25f;
            handle.DrawRect(new UIBox2(x, y, x + size, y + size), new Color(1f, 1f, 1f, alpha));
        }
    }

    private void DrawEntities(DrawingHandleScreen handle)
    {
        DrawPlayer(handle);

        foreach (var e in _game.Enemies)
        {
            var state = _enemySprites[e.Type % _enemySprites.Length];
            var tex = QueueMiniGameDrawHelpers.GetFrameOrNull(state, RsiDirection.West, e.AnimFrame);
            if (tex != null)
            {
                var entrance = Math.Clamp((GoGoShitcurityGame.GameW + GoGoShitcurityGame.EnemySize - e.X) / 72f, 0f, 1f);
                var drawSize = GoGoShitcurityGame.EnemySize * (0.65f + entrance * 0.35f);
                var centerX = e.X + GoGoShitcurityGame.EnemySize / 2f;
                var centerY = e.Y + GoGoShitcurityGame.EnemySize / 2f;
                handle.DrawTextureRect(tex,
                    new UIBox2(centerX - drawSize / 2f, centerY - drawSize / 2f,
                        centerX + drawSize / 2f, centerY + drawSize / 2f));
            }
        }
    }

    private void DrawDemoRaid(DrawingHandleScreen handle)
    {
        for (var i = 0; i < 5; i++)
        {
            var x = GoGoShitcurityGame.GameW - 72f - i * 48f + MathF.Sin(_starScrollX / 28f + i) * 12f;
            var y = 64f + i * 46f + MathF.Cos(_starScrollX / 32f + i) * 10f;
            var state = _enemySprites[i % _enemySprites.Length];
            var tex = QueueMiniGameDrawHelpers.GetFrameOrNull(state, RsiDirection.West, i % 4);
            if (tex == null)
                continue;

            handle.DrawTextureRect(tex, new UIBox2(x, y, x + GoGoShitcurityGame.EnemySize, y + GoGoShitcurityGame.EnemySize));
        }
    }

    private void DrawPlayer(DrawingHandleScreen handle)
    {
        var size = GoGoShitcurityGame.PlayerSize;
        var x = Started ? _game.PlayerX : GoGoShitcurityGame.PlayerStartX;
        var y = Started ? _game.PlayerY : GoGoShitcurityGame.GameH / 2f - size / 2f;
        var half = size / 2f;
        var cx = x + half;
        var cy = y + half;
        var prev = handle.GetTransform();
        // Rotate 90° CW so the sprite faces right (toward enemies)
        handle.SetTransform(Matrix3x2.Multiply(new Matrix3x2(0f, 1f, -1f, 0f, cx, cy), prev));
        handle.DrawTextureRect(_spacepodTexture, new UIBox2(-half, -half, half, half));
        handle.SetTransform(prev);

        if (_game.ShieldCharges <= 0)
            return;

        var shield = new Color(90, 185, 255, 65 + _game.ShieldCharges * 35);
        handle.DrawRect(new UIBox2(x - 5f, y - 5f, x + size + 5f, y + size + 5f), shield, false);
    }

    private void DrawBullets(DrawingHandleScreen handle)
    {
        foreach (var b in _game.Bullets)
        {
            var color = b.IsPlayer ? ColorBullet : ColorEnemyBullet;
            handle.DrawRect(new UIBox2(b.X, b.Y, b.X + 14f, b.Y + 5f), color);
        }
    }

    private void DrawPowerUps(DrawingHandleScreen handle)
    {
        foreach (var powerUp in _game.PowerUps)
        {
            var pulse = MathF.Sin(powerUp.Pulse) * 2f;
            var size = GoGoShitcurityGame.PowerUpSize + pulse;
            var x = powerUp.X + GoGoShitcurityGame.PowerUpSize / 2f - size / 2f;
            var y = powerUp.Y + GoGoShitcurityGame.PowerUpSize / 2f - size / 2f;
            var color = GetPowerUpColor(powerUp.Type);
            handle.DrawRect(new UIBox2(x, y, x + size, y + size), new Color(20, 50, 70, 180));
            handle.DrawRect(new UIBox2(x, y, x + size, y + size), color, false);
            DrawPowerUpGlyph(handle, powerUp.Type, x, y, size, color);
        }
    }

    private static Color GetPowerUpColor(GoGoShitcurityGame.PowerUpKind kind)
    {
        return kind switch
        {
            GoGoShitcurityGame.PowerUpKind.RapidFire => ColorRapidFire,
            GoGoShitcurityGame.PowerUpKind.Shield => ColorShield,
            GoGoShitcurityGame.PowerUpKind.SpreadShot => ColorSpreadShot,
            GoGoShitcurityGame.PowerUpKind.SlowField => ColorSlowField,
            GoGoShitcurityGame.PowerUpKind.Heal => ColorHeal,
            _ => Color.White,
        };
    }

    private static void DrawPowerUpGlyph(DrawingHandleScreen handle, GoGoShitcurityGame.PowerUpKind kind, float x, float y, float size, Color color)
    {
        var midX = x + size / 2f;
        var midY = y + size / 2f;
        switch (kind)
        {
            case GoGoShitcurityGame.PowerUpKind.RapidFire:
                handle.DrawLine(new Vector2(x + 4f, midY - 4f), new Vector2(x + size - 4f, midY - 4f), color);
                handle.DrawLine(new Vector2(x + 4f, midY), new Vector2(x + size - 4f, midY), color);
                handle.DrawLine(new Vector2(x + 4f, midY + 4f), new Vector2(x + size - 4f, midY + 4f), color);
                break;
            case GoGoShitcurityGame.PowerUpKind.Shield:
                handle.DrawRect(new UIBox2(x + 4f, y + 3f, x + size - 4f, y + size - 3f), color, false);
                break;
            case GoGoShitcurityGame.PowerUpKind.SpreadShot:
                handle.DrawLine(new Vector2(x + 4f, midY), new Vector2(x + size - 4f, y + 4f), color);
                handle.DrawLine(new Vector2(x + 4f, midY), new Vector2(x + size - 4f, midY), color);
                handle.DrawLine(new Vector2(x + 4f, midY), new Vector2(x + size - 4f, y + size - 4f), color);
                break;
            case GoGoShitcurityGame.PowerUpKind.SlowField:
                handle.DrawLine(new Vector2(midX, y + 3f), new Vector2(midX, y + size - 3f), color);
                handle.DrawLine(new Vector2(x + 4f, midY), new Vector2(x + size - 4f, midY), color);
                break;
            case GoGoShitcurityGame.PowerUpKind.Heal:
                handle.DrawLine(new Vector2(midX, y + 3f), new Vector2(midX, y + size - 3f), color);
                handle.DrawLine(new Vector2(x + 3f, midY), new Vector2(x + size - 3f, midY), color);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private void DrawOverlay(DrawingHandleScreen handle, float gw, float gh)
    {
        if (!Started)
        {
            QueueMiniGameDrawHelpers.DrawCentered(handle, _fontBig, gw, gh / 2f - 28f,
                Loc.GetString("queue-minigame-gogo-shitcurity-title"), new Color(255, 200, 50));
            QueueMiniGameDrawHelpers.DrawCentered(handle, _font, gw, gh / 2f + 10f,
                Loc.GetString("queue-minigame-start"), Color.White);
            QueueMiniGameDrawHelpers.DrawCentered(handle, _font, gw, gh / 2f + 30f,
                Loc.GetString("queue-minigame-gogo-shitcurity-controls"), new Color(160, 160, 160));
            return;
        }

        if (_game.GameOver)
        {
            handle.DrawRect(new UIBox2(0, 0, gw, gh), new Color(0, 0, 0, 140));
            QueueMiniGameDrawHelpers.DrawCentered(handle, _fontBig, gw, gh / 2f - 20f,
                Loc.GetString("queue-minigame-game-over"), new Color(255, 80, 80));
            QueueMiniGameDrawHelpers.DrawCentered(handle, _font, gw, gh / 2f + 18f,
                Loc.GetString("queue-minigame-score", ("score", _game.Score)), Color.White);
            QueueMiniGameDrawHelpers.DrawCentered(handle, _font, gw, gh / 2f + 40f,
                Loc.GetString("queue-minigame-restart"), new Color(160, 160, 160));
        }
        else if (_game.Victory)
        {
            handle.DrawRect(new UIBox2(0, 0, gw, gh), new Color(0, 0, 0, 140));
            QueueMiniGameDrawHelpers.DrawCentered(handle, _fontBig, gw, gh / 2f - 20f,
                Loc.GetString("queue-minigame-you-win"), new Color(50, 255, 50));
            QueueMiniGameDrawHelpers.DrawCentered(handle, _font, gw, gh / 2f + 18f,
                Loc.GetString("queue-minigame-score", ("score", _game.Score)), Color.White);
            QueueMiniGameDrawHelpers.DrawCentered(handle, _font, gw, gh / 2f + 40f,
                Loc.GetString("queue-minigame-restart"), new Color(160, 160, 160));
        }
    }
}
