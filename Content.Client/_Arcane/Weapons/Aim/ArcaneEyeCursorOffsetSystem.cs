using System.Numerics;
using Content.Shared._Arcane.Weapons.Aim;
using Content.Shared.Camera;
using Content.Shared.Movement.Systems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client._Arcane.Weapons.Aim;

public sealed class ArcaneEyeCursorOffsetSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly SharedContentEyeSystem _eye = default!;

    private readonly Dictionary<EntityUid, Vector2> _currentOffsets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArcaneEyeCursorOffsetComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ArcaneEyeCursorOffsetComponent, GetEyeOffsetEvent>(OnGetEyeOffset);
    }

    private void OnShutdown(Entity<ArcaneEyeCursorOffsetComponent> ent, ref ComponentShutdown args)
    {
        _currentOffsets.Remove(ent.Owner);
        if (TryComp<EyeComponent>(ent.Owner, out var eye))
            _eye.UpdateEyeOffset((ent.Owner, eye));
    }

    private void OnGetEyeOffset(Entity<ArcaneEyeCursorOffsetComponent> ent, ref GetEyeOffsetEvent args)
    {
        if (_player.LocalEntity != ent.Owner)
            return;

        if (_currentOffsets.TryGetValue(ent.Owner, out var offset))
            args.Offset += offset;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalEntity is not { } player)
            return;

        if (!TryComp<ArcaneEyeCursorOffsetComponent>(player, out var comp))
        {
            if (_currentOffsets.Remove(player) && TryComp<EyeComponent>(player, out var staleEye))
                _eye.UpdateEyeOffset((player, staleEye));
            return;
        }

        if (_eyeManager.MainViewport is not Control viewport)
            return;

        var vpSize = (Vector2) viewport.PixelSize;
        if (vpSize.X <= 0 || vpSize.Y <= 0)
            return;

        var mouse = _input.MouseScreenPosition.Position;
        var center = viewport.GlobalPixelPosition + vpSize / 2f;

        var normalized = new Vector2(
            (mouse.X - center.X) / (vpSize.X / 2f),
            (center.Y - mouse.Y) / (vpSize.Y / 2f));

        normalized = Vector2.Clamp(normalized, -Vector2.One, Vector2.One);

        var target = normalized * comp.MaxOffset;
        var current = _currentOffsets.GetValueOrDefault(player, Vector2.Zero);
        var delta = target - current;
        var step = comp.OffsetSpeed * frameTime * 10f;

        if (delta.LengthSquared() <= step * step)
            current = target;
        else
            current += Vector2.Normalize(delta) * step;

        _currentOffsets[player] = current;

        if (TryComp<EyeComponent>(player, out var eye))
            _eye.UpdateEyeOffset((player, eye));
    }
}
