using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;
using System.Numerics;

namespace Content.Client._Arcane.JoinQueue;

internal static class QueueMiniGameDrawHelpers
{
    private static readonly Color FrameOuter = new(190, 145, 70);
    private static readonly Color FrameInner = new(55, 52, 68);

    public static Texture? GetFrameOrNull(IRsiStateLike state, RsiDirection preferredDirection, int frame)
    {
        var frameCount = state.AnimationFrameCount;
        var safeFrame = frameCount > 0 ? frame % frameCount : 0;
        if (safeFrame < 0)
            safeFrame += frameCount;

        var direction = state.RsiDirections switch
        {
            RsiDirectionType.Dir1 => RsiDirection.South,
            RsiDirectionType.Dir4 when preferredDirection > RsiDirection.West => RsiDirection.South,
            _ => preferredDirection,
        };

        if (TryGetFrame(state, direction, safeFrame, out var texture))
            return texture;

        if (TryGetFrame(state, direction, 0, out texture))
            return texture;

        if (TryGetFrame(state, RsiDirection.South, 0, out texture))
            return texture;

        return null;
    }

    private static bool TryGetFrame(IRsiStateLike state, RsiDirection direction, int frame, out Texture? texture)
    {
        try
        {
            texture = state.GetFrame(direction, frame);
            return true;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            texture = null;
            return false;
        }
    }

    public static void DrawCentered(DrawingHandleScreen handle, Font font, float gameWidth, float topY, string text, Color color)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
            width += font.GetCharMetrics(rune, 1f)?.Advance ?? 0;

        handle.DrawString(font, new Vector2((gameWidth - width) / 2f, topY + font.GetAscent(1f)), text, color);
    }

    public static void DrawArcadeFrame(DrawingHandleScreen handle, float width, float height, float time)
    {
        var pulse = 0.55f + MathF.Sin(time * 4.5f) * 0.18f;
        var glow = new Color(255, 205, 105, 45 + (int)(pulse * 35f));
        handle.DrawRect(new UIBox2(0, 0, width, height), glow, false);
        handle.DrawRect(new UIBox2(1, 1, width - 1, height - 1), FrameOuter, false);
        handle.DrawRect(new UIBox2(4, 4, width - 4, height - 4), FrameInner, false);

    }

    public static void AddBurst(List<MiniGameParticle> particles, float x, float y, Color color)
    {
        for (var i = 0; i < 10; i++)
        {
            var angle = MathF.PI * 2f * i / 10f;
            var speed = 28f + i % 3 * 18f;
            particles.Add(new MiniGameParticle(
                x,
                y,
                MathF.Cos(angle) * speed,
                MathF.Sin(angle) * speed,
                0.36f,
                color));
        }
    }

    public static void UpdateParticles(List<MiniGameParticle> particles, float frameTime)
    {
        for (var i = particles.Count - 1; i >= 0; i--)
        {
            var particle = particles[i];
            particle.X += particle.VX * frameTime;
            particle.Y += particle.VY * frameTime;
            particle.Lifetime -= frameTime;

            if (particle.Lifetime <= 0f)
            {
                particles.RemoveAt(i);
                continue;
            }

            particles[i] = particle;
        }
    }

    public static void DrawParticles(DrawingHandleScreen handle, List<MiniGameParticle> particles)
    {
        foreach (var particle in particles)
        {
            var alpha = Math.Clamp(particle.Lifetime / 0.36f, 0f, 1f);
            var color = particle.Color.WithAlpha(alpha);
            handle.DrawRect(new UIBox2(particle.X - 2f, particle.Y - 2f, particle.X + 2f, particle.Y + 2f), color);
        }
    }
}

internal struct MiniGameParticle(float x, float y, float vx, float vy, float lifetime, Color color)
{
    public float X = x;
    public float Y = y;
    public float VX = vx;
    public float VY = vy;
    public float Lifetime = lifetime;
    public Color Color = color;
}
