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

namespace Content.Client._Arcane.JoinQueue.SpaceInvaders;

public sealed class SpaceInvadersControl : QueueMiniGameControl
{
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly SpaceInvadersGame _game = new();
    private readonly (float X, float Y, float Speed, float Size)[] _stars;
    private readonly List<MiniGameParticle> _particles = [];
    private SpriteSystem _sprites = default!;
    private Font _font = default!;
    private Font _fontBig = default!;
    private Texture _spacepodTexture = default!;
    private readonly IRsiStateLike[] _crawlerSprites = new IRsiStateLike[3];
    private IRsiStateLike _coreSprite = default!;
    private float _starScroll;

    public override int Score => _game.Score;
    public override int Lives => _game.Lives;
    public override int Wave => _game.Wave;

    protected override bool IsGameFinished => _game.GameOver || _game.Victory;

    private static readonly Color ColorBg = new(4, 3, 18);
    private static readonly Color ColorGround = new(90, 230, 120);
    private static readonly Color ColorPlayerBullet = new(180, 255, 255);
    private static readonly Color ColorEnemyBullet = new(255, 120, 80);

    public SpaceInvadersControl()
    {
        IoCManager.InjectDependencies(this);

        _sprites = _entMan.System<SpriteSystem>();
        _font = QueueMiniGameAssets.LoadRegularFont(_cache, 12);
        _fontBig = QueueMiniGameAssets.LoadBoldFont(_cache, 18);
        _spacepodTexture = QueueMiniGameAssets.LoadSpacepodTexture(_cache);

        _crawlerSprites[0] = LoadState(QueueMiniGameAssets.SpaceCarpRsi, "alive");
        _crawlerSprites[1] = LoadState(QueueMiniGameAssets.MagicCarpRsi, "alive");
        _crawlerSprites[2] = LoadState(QueueMiniGameAssets.SharkminnowRsi, "alive");
        _coreSprite = LoadState(QueueMiniGameAssets.DragonCarpRsi, "alive");

        _game.OnShoot = RaiseBulletFired;
        _game.OnEnemyKilled = (x, y) =>
        {
            QueueMiniGameDrawHelpers.AddBurst(_particles, x, y, new Color(120, 255, 150));
            RaiseEnemyKilled();
        };
        _game.OnGameOver = () => RaiseGameEnded(false);
        _game.OnVictory = () => RaiseGameEnded(true);

        MinSize = new Vector2(SpaceInvadersGame.GameW, SpaceInvadersGame.GameH);
        RectClipContent = true;

        var rng = new Random(127);
        _stars = new (float, float, float, float)[54];
        for (var i = 0; i < _stars.Length; i++)
        {
            _stars[i] = (
                rng.NextSingle() * SpaceInvadersGame.GameW,
                rng.NextSingle() * SpaceInvadersGame.GameH,
                10f + rng.NextSingle() * 28f,
                i % 4 == 0 ? 2f : 1f);
        }
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
        _starScroll += args.DeltaSeconds;
    }

    protected override void UpdateIdleGame(FrameEventArgs args)
    {
        _starScroll += args.DeltaSeconds * 0.55f;
    }

    protected override void UpdateSharedGame(FrameEventArgs args)
    {
        QueueMiniGameDrawHelpers.UpdateParticles(_particles, args.DeltaSeconds);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var gw = SpaceInvadersGame.GameW;
        var gh = SpaceInvadersGame.GameH;

        handle.DrawRect(new UIBox2(0, 0, gw, gh), ColorBg);
        DrawStars(handle, gw, gh);
        handle.DrawRect(new UIBox2(0, SpaceInvadersGame.GroundY, gw, SpaceInvadersGame.GroundY + 2f), ColorGround);

        if (Started)
        {
            DrawCrawlers(handle);
            DrawCore(handle);
        }
        else
        {
            DrawDemoFormation(handle);
        }

        DrawPlayer(handle);
        DrawBullets(handle);
        QueueMiniGameDrawHelpers.DrawParticles(handle, _particles);
        DrawOverlay(handle, gw, gh);
        QueueMiniGameDrawHelpers.DrawArcadeFrame(handle, gw, gh, _starScroll);
    }

    private void DrawStars(DrawingHandleScreen handle, float gw, float gh)
    {
        foreach (var (baseX, baseY, speed, size) in _stars)
        {
            var y = (baseY + _starScroll * speed) % gh;
            var alpha = 0.35f + size * 0.18f;
            handle.DrawRect(new UIBox2(baseX, y, baseX + size, y + size), new Color(1f, 1f, 1f, alpha));
        }
    }

    private IRsiStateLike LoadState(string rsiPath, string stateName)
    {
        var spec = new SpriteSpecifier.Rsi(new ResPath(rsiPath), stateName);
        return _sprites.RsiStateLike(spec);
    }

    private void DrawPlayer(DrawingHandleScreen handle)
    {
        const float drawSize = 42f;
        var x = _game.PlayerX + SpaceInvadersGame.PlayerW / 2f - drawSize / 2f;
        var y = _game.PlayerY + SpaceInvadersGame.PlayerH - drawSize;
        handle.DrawTextureRect(_spacepodTexture, new UIBox2(x, y, x + drawSize, y + drawSize));
    }

    private void DrawCore(DrawingHandleScreen handle)
    {
        if (_game.CoreHealth <= 0)
            return;

        var x = _game.CoreX;
        var y = _game.CoreY;
        const float size = 56f;
        var tex = QueueMiniGameDrawHelpers.GetFrameOrNull(_coreSprite, RsiDirection.South, 0);
        if (tex != null)
            handle.DrawTextureRect(tex, new UIBox2(x, y, x + size, y + size));
    }

    private void DrawCrawlers(DrawingHandleScreen handle)
    {
        foreach (var crawler in _game.Crawlers)
        {
            var state = _crawlerSprites[crawler.Type % _crawlerSprites.Length];
            var tex = QueueMiniGameDrawHelpers.GetFrameOrNull(state, RsiDirection.South, crawler.LegFrame);
            if (tex == null)
                continue;
            var entrance = Math.Clamp(crawler.SpawnAge / 0.55f, 0.35f, 1f);
            var drawSize = 30f * entrance;
            const float cw = SpaceInvadersGame.CrawlerW;
            const float ch = SpaceInvadersGame.CrawlerH;
            var x = crawler.X + cw / 2f - drawSize / 2f;
            var y = crawler.Y + ch / 2f - drawSize / 2f;
            handle.DrawTextureRect(tex, new UIBox2(x, y, x + drawSize, y + drawSize));
        }
    }

    private void DrawDemoFormation(DrawingHandleScreen handle)
    {
        for (var row = 0; row < 3; row++)
        for (var col = 0; col < 7; col++)
        {
            var state = _crawlerSprites[(row + col) % _crawlerSprites.Length];
            var tex = QueueMiniGameDrawHelpers.GetFrameOrNull(state, RsiDirection.South, (int)(_starScroll * 5f + col) % 2);
            if (tex == null)
                continue;

            const float drawSize = 28f;
            var x = 164f + col * 34f + MathF.Sin(_starScroll * 1.7f + row) * 8f;
            var y = 82f + row * 34f + MathF.Cos(_starScroll * 1.3f + col) * 5f;
            handle.DrawTextureRect(tex, new UIBox2(x, y, x + drawSize, y + drawSize));
        }

        var coreTex = QueueMiniGameDrawHelpers.GetFrameOrNull(_coreSprite, RsiDirection.South, 0);
        if (coreTex != null)
        {
            var x = SpaceInvadersGame.GameW / 2f - 28f + MathF.Sin(_starScroll) * 18f;
            handle.DrawTextureRect(coreTex, new UIBox2(x, 34f, x + 56f, 90f));
        }
    }

    private void DrawBullets(DrawingHandleScreen handle)
    {
        foreach (var bullet in _game.Bullets)
        {
            var color = bullet.IsPlayer ? ColorPlayerBullet : ColorEnemyBullet;
            handle.DrawRect(new UIBox2(bullet.X, bullet.Y, bullet.X + 4f, bullet.Y + 10f), color);
        }
    }

    private void DrawOverlay(DrawingHandleScreen handle, float gw, float gh)
    {
        if (!Started)
        {
            QueueMiniGameDrawHelpers.DrawCentered(handle, _fontBig, gw, gh / 2f - 26f,
                Loc.GetString("queue-minigame-space-invaders-title"), new Color(255, 210, 90));
            QueueMiniGameDrawHelpers.DrawCentered(handle, _font, gw, gh / 2f + 10f,
                Loc.GetString("queue-minigame-start"), Color.White);
            QueueMiniGameDrawHelpers.DrawCentered(handle, _font, gw, gh / 2f + 30f,
                Loc.GetString("queue-minigame-space-invaders-controls"), new Color(170, 170, 190));
            return;
        }

        if (!_game.GameOver && !_game.Victory)
            return;

        handle.DrawRect(new UIBox2(0, 0, gw, gh), new Color(0, 0, 0, 150));
        QueueMiniGameDrawHelpers.DrawCentered(handle, _fontBig, gw, gh / 2f - 20f,
            Loc.GetString(_game.Victory ? "queue-minigame-you-win" : "queue-minigame-game-over"),
            _game.Victory ? new Color(90, 255, 130) : new Color(255, 90, 90));
        QueueMiniGameDrawHelpers.DrawCentered(handle, _font, gw, gh / 2f + 18f,
            Loc.GetString("queue-minigame-score", ("score", _game.Score)), Color.White);
        QueueMiniGameDrawHelpers.DrawCentered(handle, _font, gw, gh / 2f + 40f,
            Loc.GetString("queue-minigame-restart"), new Color(170, 170, 190));
    }
}
