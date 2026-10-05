using System.Numerics;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Part;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._Arcane.Medical.Surgery;

public readonly record struct SurgeryDollPart(Texture Texture, Texture? Bleeding, bool Opened, bool Missing, string Tooltip);

/// <summary>
/// Clickable body doll drawn from the Shitmed part status sprites.
/// </summary>
public sealed class SurgeryDollControl : Control
{
    private const int SpriteSize = 32;
    private const float Scale = 6f;

    private static readonly Color DimmedColor = new(0.55f, 0.55f, 0.6f);
    private static readonly Color MissingColor = Color.White.WithAlpha(0.3f);
    private static readonly Color OpenedColor = Color.FromHex("#E0A93F");
    private static readonly Color OpenedBorderColor = Color.FromHex("#1B1B1E");

    // Part bounds inside the 32x32 status sprites; arms and hands come last so they win the overlap with the torso.
    private static readonly (TargetBodyPart Part, UIBox2i Box)[] Bounds =
    [
        (TargetBodyPart.Chest, new UIBox2i(11, 10, 20, 17)),
        (TargetBodyPart.Groin, new UIBox2i(11, 17, 20, 22)),
        (TargetBodyPart.Head, new UIBox2i(12, 3, 19, 10)),
        (TargetBodyPart.RightLeg, new UIBox2i(11, 20, 15, 26)),
        (TargetBodyPart.LeftLeg, new UIBox2i(16, 20, 20, 26)),
        (TargetBodyPart.RightFoot, new UIBox2i(9, 26, 15, 29)),
        (TargetBodyPart.LeftFoot, new UIBox2i(16, 26, 22, 29)),
        (TargetBodyPart.RightArm, new UIBox2i(7, 10, 12, 16)),
        (TargetBodyPart.LeftArm, new UIBox2i(19, 10, 24, 16)),
        (TargetBodyPart.RightHand, new UIBox2i(7, 16, 11, 20)),
        (TargetBodyPart.LeftHand, new UIBox2i(20, 16, 24, 20)),
    ];

    private readonly Dictionary<TargetBodyPart, SurgeryDollPart> _parts = new();
    private readonly Dictionary<TargetBodyPart, Control> _hitAreas = new();
    private TargetBodyPart? _hovered;

    public TargetBodyPart? Selected { get; set; }

    public event Action<TargetBodyPart, UIBox2>? OnPartPressed;

    public SurgeryDollControl()
    {
        SetSize = new Vector2(SpriteSize * Scale);

        foreach (var (part, box) in Bounds)
        {
            var area = new Control
            {
                MouseFilter = MouseFilterMode.Stop,
                DefaultCursorShape = CursorShape.Hand,
                HorizontalAlignment = HAlignment.Left,
                VerticalAlignment = VAlignment.Top,
                Margin = new Thickness(box.Left * Scale, box.Top * Scale, 0, 0),
                SetSize = new Vector2(box.Width, box.Height) * Scale,
                Visible = false,
            };

            area.OnMouseEntered += _ => _hovered = part;
            area.OnMouseExited += _ =>
            {
                if (_hovered == part)
                    _hovered = null;
            };
            area.OnKeyBindDown += args =>
            {
                if (args.Function != EngineKeyFunctions.UIClick)
                    return;

                OnPartPressed?.Invoke(part, UIBox2.FromDimensions(area.GlobalPosition, area.Size));
                args.Handle();
            };

            _hitAreas[part] = area;
            AddChild(area);
        }
    }

    public static TargetBodyPart? GetDollPart(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return (type, symmetry) switch
        {
            (BodyPartType.Head, _) => TargetBodyPart.Head,
            (BodyPartType.Chest, _) => TargetBodyPart.Chest,
            (BodyPartType.Groin, _) => TargetBodyPart.Groin,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => TargetBodyPart.LeftArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => TargetBodyPart.RightArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => TargetBodyPart.LeftHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => TargetBodyPart.RightHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => TargetBodyPart.LeftLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => TargetBodyPart.RightLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => TargetBodyPart.LeftFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => TargetBodyPart.RightFoot,
            _ => null,
        };
    }

    public void SetParts(IReadOnlyDictionary<TargetBodyPart, SurgeryDollPart> parts)
    {
        _parts.Clear();
        foreach (var (part, data) in parts)
            _parts[part] = data;

        foreach (var (part, area) in _hitAreas)
        {
            area.Visible = _parts.TryGetValue(part, out var data);
            area.ToolTip = area.Visible ? data.Tooltip : null;
        }

        if (_hovered is { } hovered && !_parts.ContainsKey(hovered))
            _hovered = null;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var scale = Scale * UIScale;
        var rect = UIBox2.FromDimensions(Vector2.Zero, new Vector2(SpriteSize * scale));

        foreach (var (part, _) in Bounds)
        {
            if (!_parts.TryGetValue(part, out var data))
                continue;

            var color = data.Missing
                ? MissingColor
                : Selected != null && Selected != part && _hovered != part
                    ? DimmedColor
                    : Color.White;

            handle.DrawTextureRect(data.Texture, rect, color);
            if (data.Bleeding is { } bleeding)
                handle.DrawTextureRect(bleeding, rect, color);
        }

        foreach (var (part, box) in Bounds)
        {
            if (!_parts.TryGetValue(part, out var data) || !data.Opened)
                continue;

            var origin = new Vector2(box.Right - 1.5f, box.Top - 0.5f) * scale;
            var border = 0.4f * scale;
            handle.DrawRect(UIBox2.FromDimensions(origin, new Vector2(2f * scale)), OpenedBorderColor);
            handle.DrawRect(UIBox2.FromDimensions(origin + new Vector2(border), new Vector2(2f * scale - border * 2)), OpenedColor);
        }
    }
}
