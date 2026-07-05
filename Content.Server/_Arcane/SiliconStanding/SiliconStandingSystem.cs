using Content.Shared._Arcane.SiliconStanding;
using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Server.Audio;

namespace Content.Server._Arcane.SiliconStanding;

/// <summary>
/// Server-only part of the silicon resting system: plays audio and updates movement on resting state changes.
/// Prediction logic (action handling, movement blocking, state toggle) lives in <see cref="SharedSiliconStandingSystem"/>.
/// </summary>
public sealed class SiliconStandingSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconRestingComponent, ComponentStartup>(OnRestingStartup);
        SubscribeLocalEvent<SiliconRestingComponent, ComponentRemove>(OnRestingRemove);
    }

    private void OnRestingStartup(Entity<SiliconRestingComponent> ent, ref ComponentStartup args)
    {
        _actionBlocker.UpdateCanMove(ent);
        PlayRestSound(ent);
    }

    private void OnRestingRemove(Entity<SiliconRestingComponent> ent, ref ComponentRemove args)
    {
        _actionBlocker.UpdateCanMove(ent);
        PlayRestSound(ent);
    }

    private void PlayRestSound(EntityUid uid)
    {
        if (TryComp<FootstepModifierComponent>(uid, out var footsteps))
            _audio.PlayPvs(footsteps.FootstepSoundCollection, uid);
    }
}
