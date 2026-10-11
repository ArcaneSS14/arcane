using Content.Shared._Arcane.Weapons.Aim;
using Content.Shared.Actions;
using Robust.Shared.GameObjects;

namespace Content.Server._Arcane.Weapons.Aim;

public sealed class ArcaneAimSystem : SharedArcaneAimSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArcaneAimComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<ArcaneAimComponent> ent, ref MapInitEvent args)
    {
    }
}
