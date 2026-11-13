using Content.Goobstation.Common.Traits.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.IdentityManagement;
using Content.Shared.Standing;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
// Arcane-Start
using Content.Shared._Shitmed.Targeting;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Robust.Shared.Network;
using Content.Shared.Weapons.Melee;
using Content.Shared.Damage.Systems;
// Arcane-End

namespace Content.Goobstation.Shared.Traits.Systems;

public sealed partial class SocialAnxietySystem : EntitySystem
{
    [Dependency] private StandingStateSystem _standingSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedStunSystem _stunSystem = default!;
    // Arcane-Start
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private DamageableSystem _damage = default!;
    // Arcane-End

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SocialAnxietyComponent, InteractionSuccessEvent>(OnHug);
    }
    private void OnHug(EntityUid uid, SocialAnxietyComponent component, ref InteractionSuccessEvent args)
    {
        _standingSystem.Down(uid);
        _stunSystem.TryUpdateStunDuration(uid, TimeSpan.FromSeconds(component.DownedTime));
        var mobName = Identity.Name(uid, EntityManager);
        _popupSystem.PopupEntity(Loc.GetString("social-anxiety-hugged", ("user", mobName)), uid, PopupType.MediumCaution);
        ApplyHugDamage(uid, component, args.User); // Arcane
    }
    // Arcane-Start
    private void ApplyHugDamage(EntityUid uid, SocialAnxietyComponent component, EntityUid hugger)
    {   
        if (!_net.IsServer || uid == hugger)
            return;

        var now = _timing.CurTime;
        var repeatedHug = component.HugWindowUntil.TryGetValue(hugger, out var until) && now < until;

        if (!repeatedHug)
            component.HugWindowUntil[hugger] = now + TimeSpan.FromSeconds(30);

        if (repeatedHug && !_random.Prob(0.20f))
            return;

        var damage = _melee.GetDamage(uid, uid) * (repeatedHug ? 3 : 2);
        var part = repeatedHug ? TargetBodyPart.Head : TargetBodyPart.LeftArm;

        _damage.TryChangeDamage(hugger, damage, origin: uid, targetPart: part,  canMiss: false);
    }
    // Arcane-End
}
