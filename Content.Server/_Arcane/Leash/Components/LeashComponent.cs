using Content.Shared.Damage;
using Robust.Shared.GameObjects;

namespace Content.Server._Arcane.Leash.Components;

[RegisterComponent, Access(typeof(Systems.LeashSystem))]
public sealed partial class LeashComponent : Component
{
    [DataField]
    public DamageSpecifier ChokeDamage = new()
    {
        DamageDict =
        {
            ["Asphyxiation"] = 1,
        },
    };

    [DataField]
    public TimeSpan ChokeCooldown = TimeSpan.FromSeconds(0.5);

    [DataField]
    public float ChokeDistance = 3.25f;

    [DataField]
    public float ResistanceThreshold = 0.05f;

    [DataField]
    public float MaxTensionDistance = 1.35f;

    [DataField]
    public float MaximumDistance = 4f;

    [DataField]
    public float PullForce = 32f;

    public EntityUid? AttachedCollar;
    public EntityUid? Holder;
    public TimeSpan NextChokeTime;
}
