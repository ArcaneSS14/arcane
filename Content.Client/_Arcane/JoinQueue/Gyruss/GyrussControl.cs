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

namespace Content.Client._Arcane.JoinQueue.Gyruss;

public sealed class GyrussControl : QueueMiniGameControl
{
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly GyrussGame _game = new();
    private readonly (float X, float Y, float Size)[] _stars;
    private readonly List<MiniGameParticle> _particles = [];
    private SpriteSystem _sprites = default!;
    private Font _font = default!;
    private Font _fontBig = default!;
    private Texture _spacepodTexture = default!;
    private readonly IRsiStateLike[] _enemySprites = new IRsiStateLike[3];
    private IRsiStateLike _portalSprite = default!;
    private float _portalTime;

    public override int Score => _game.Score;
    public override int Lives => _game.Lives;
    public override int Wave => _game.Wave;

    protected override bool IsGameFinished => _game.GameOver || _game.Victory;

    private static readonly Color ColorBg = new(0, 0, 18);
    private static readonly Color ColorBulletPlayer = new(160, 255, 255);
    private static readonly Color ColorBulletEnemy = new(255, 110, 80);
    private const float Tau = MathF.PI * 2f;

    public GyrussControl()
    {
        IoCManager.InjectDependencies(this);

        _sprites = _entMan.System<SpriteSystem>();
        _font = QueueMiniGameAssets.LoadRegularFont(_cache, 12);
        _fontBig = QueueMiniGameAssets.LoadBoldFont(_cache, 18);
        _spacepodTexture = QueueMiniGameAssets.LoadSpacepodTexture(_cache);

        _enemySprites[0] = LoadState(QueueMiniGameAssets.SpaceCarpRsi, "alive");
        _enemySprites[1] = LoadState(QueueMiniGameAssets.MagicCarpRsi, "alive");
        _enemySprites[2] = LoadState(QueueMiniGameAssets.SharkminnowRsi, "alive");
        _portalSprite = LoadState(QueueMiniGameAssets.PortalRsi, "portal-artifact");

        _game.OnShoot = RaiseBulletFired;
        _game.OnEnemyKilled = (x, y) =>
        {
            QueueMiniGameDrawHelpers.AddBurst(_particles, x, y, new Color(105, 230, 255));
            RaiseEnemyKilled();
        };
        _game.OnGameOver = () => RaiseGameEnded(false);
        _game.OnVictory = () => RaiseGameEnded(true);

        MinSize = new Vector2(GyrussGame.GameW, GyrussGame.GameH);
        RectClipContent = true;

        var rng = new Random(77);
        _stars = new (float, float, float)[70];
        for (var i = 0; i < _stars.Length; i++)
            _stars[i] = (
                rng.NextSingle() * GyrussGame.GameW,
                rng.NextSingle() * GyrussGame.GameH,
                i % 5 == 0 ? 2f : 1f);
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
        _game.Update(args.DeltaSeconds, left, right, space);
    }

    protected override void UpdateSharedGame(FrameEventArgs args)
    {
        _portalTime += args.DeltaSeconds;
        QueueMiniGameDrawHelpers.UpdateParticles(_particles, args.DeltaSeconds);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var gw = GyrussGame.GameW;
        var gh = GyrussGame.GameH;

        handle.DrawRect(new UIBox2(0, 0, gw, gh), ColorBg);
        DrawStars(handle);
        DrawPortal(handle);
        DrawRing(handle, GyrussGame.PlayerRadius, new Color(55, 80, 120, 35));

        if (Started)
        {
            DrawEnemies(handle);
            DrawBullets(handle);
        }
        else
        {
            DrawDemoEnemies(handle);
        }

        DrawPlayer(handle);
        QueueMiniGameDrawHelpers.DrawParticles(handle, _particles);
        DrawOverlay(handle, gw, gh);
        QueueMiniGameDrawHelpers.DrawArcadeFrame(handle, gw, gh, _portalTime);
    }

    private void DrawStars(DrawingHandleScreen handle)
    {
        foreach (var (x, y, size) in _stars)
        {
            var alpha = 0.3f + size * 0.25f;
            handle.DrawRect(new UIBox2(x, y, x + size, y + size), new Color(1f, 1f, 1f, alpha));
        }
    }

    private void DrawPortal(DrawingHandleScreen handle)
    {
        const float size = 64f;
        var frameCount = _portalSprite.AnimationFrameCount;
        var frame = frameCount > 0 ? (int)(_portalTime * 8f) % frameCount : 0;
        var tex = QueueMiniGameDrawHelpers.GetFrameOrNull(_portalSprite, RsiDirection.South, frame);
        if (tex == null)
            return;
        var cx = GyrussGame.CenterX;
        var cy = GyrussGame.CenterY;
        handle.DrawTextureRect(tex, new UIBox2(cx - size / 2f, cy - size / 2f, cx + size / 2f, cy + size / 2f));
    }

    private static void DrawRing(DrawingHandleScreen handle, float radius, Color color)
    {
        const int segments = 72;
        var previous = GyrussGame.PolarToPoint(0f, radius);
        for (var i = 1; i <= segments; i++)
        {
            var angle = Tau * i / segments;
            var next = GyrussGame.PolarToPoint(angle, radius);
            handle.DrawLine(new Vector2(previous.X, previous.Y), new Vector2(next.X, next.Y), color);
            previous = next;
        }
    }

    private void DrawPlayer(DrawingHandleScreen handle)
    {
        const float drawSize = 30f;
        const float half = drawSize / 2f;
        var angle = _game.PlayerAngle;
        var center = ToVector(GyrussGame.PolarToPoint(angle, GyrussGame.PlayerRadius));
        var rot = angle - MathF.PI / 2f;
        var cos = MathF.Cos(rot);
        var sin = MathF.Sin(rot);
        var prev = handle.GetTransform();
        handle.SetTransform(Matrix3x2.Multiply(new Matrix3x2(cos, sin, -sin, cos, center.X, center.Y), prev));
        handle.DrawTextureRect(_spacepodTexture, new UIBox2(-half, -half, half, half));
        handle.SetTransform(prev);
    }

    private void DrawEnemies(DrawingHandleScreen handle)
    {
        foreach (var enemy in _game.Enemies)
        {
            var center = ToVector(GyrussGame.PolarToPoint(enemy.Angle, enemy.Radius));
            var scale = 0.45f + enemy.Radius / GyrussGame.PlayerRadius * 0.75f;
            var drawSize = (18f * scale + MathF.Sin(enemy.Pulse) * 1.5f) * 2f;
            var state = _enemySprites[enemy.Kind % _enemySprites.Length];
            var tex = QueueMiniGameDrawHelpers.GetFrameOrNull(state, RsiDirection.South, 0);
            if (tex != null)
                handle.DrawTextureRect(tex,
                    new UIBox2(center.X - drawSize / 2f, center.Y - drawSize / 2f,
                        center.X + drawSize / 2f, center.Y + drawSize / 2f));
        }
    }

    private void DrawDemoEnemies(DrawingHandleScreen handle)
    {
        for (var i = 0; i < 6; i++)
        {
            var angle = _portalTime * 0.65f + MathF.PI * 2f * i / 6f;
            var radius = 56f + MathF.Sin(_portalTime * 1.3f + i) * 10f;
            var center = ToVector(GyrussGame.PolarToPoint(angle, radius));
            var state = _enemySprites[i % _enemySprites.Length];
            var tex = QueueMiniGameDrawHelpers.GetFrameOrNull(state, RsiDirection.South, 0);
            if (tex == null)
                continue;

            const float drawSize = 22f;
            handle.DrawTextureRect(tex,
                new UIBox2(center.X - drawSize / 2f, center.Y - drawSize / 2f,
                    center.X + drawSize / 2f, center.Y + drawSize / 2f));
        }
    }

    private void DrawBullets(DrawingHandleScreen handle)
    {
        foreach (var bullet in _game.Bullets)
        {
            var center = ToVector(GyrussGame.PolarToPoint(bullet.Angle, bullet.Radius));
            var color = bullet.IsPlayer ? ColorBulletPlayer : ColorBulletEnemy;
            var size = bullet.IsPlayer ? 3f : 4f;
            handle.DrawRect(new UIBox2(center.X - size, center.Y - size, center.X + size, center.Y + size), color);
        }
    }

    private void DrawOverlay(DrawingHandleScreen handle, float gw, float gh)
    {
        if (!Started)
        {
            QueueMiniGameDrawHelpers.DrawCentered(handle, _fontBig, gw, gh / 2f - 28f,
                Loc.GetString("queue-minigame-gyruss-title"), Color.Cyan);
            QueueMiniGameDrawHelpers.DrawCentered(handle, _font, gw, gh / 2f + 10f,
                Loc.GetString("queue-minigame-start"), Color.White);
            QueueMiniGameDrawHelpers.DrawCentered(handle, _font, gw, gh / 2f + 30f,
                Loc.GetString("queue-minigame-gyruss-controls"), new Color(170, 170, 190));
            return;
        }

        if (!_game.GameOver && !_game.Victory)
            return;

        handle.DrawRect(new UIBox2(0, 0, gw, gh), new Color(0, 0, 0, 140));
        QueueMiniGameDrawHelpers.DrawCentered(handle, _fontBig, gw, gh / 2f - 20f,
            Loc.GetString(_game.Victory ? "queue-minigame-you-win" : "queue-minigame-game-over"),
            _game.Victory ? new Color(60, 255, 120) : new Color(255, 80, 80));
        QueueMiniGameDrawHelpers.DrawCentered(handle, _font, gw, gh / 2f + 18f,
            Loc.GetString("queue-minigame-score", ("score", _game.Score)), Color.White);
        QueueMiniGameDrawHelpers.DrawCentered(handle, _font, gw, gh / 2f + 40f,
            Loc.GetString("queue-minigame-restart"), new Color(170, 170, 190));
    }

    private static Vector2 ToVector((float X, float Y) point)
    {
        return new Vector2(point.X, point.Y);
    }
}
