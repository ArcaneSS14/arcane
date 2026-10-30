// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Shared._Arcane.Bloodlust;
using Content.Shared.Chat;
using Content.Shared.Eye;
using Content.Shared.Implants;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Arcane.Bloodlust;

public sealed class BloodlustImplantSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;

    private const int HostVisibility = (int) VisibilityFlags.BloodlustCompanion;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodlustImplantComponent, ImplantImplantedEvent>(OnImplanted);
        SubscribeLocalEvent<BloodlustImplantComponent, EntGotRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<BloodlustImplantComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<BloodlustHostComponent, ProjectileReflectAttemptEvent>(OnProjectileAttempt);

        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<MeleeWeaponComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<MobStateComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnImplanted(Entity<BloodlustImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        TryActivate(ent, args.Implanted);
    }

    private void OnRemoved(Entity<BloodlustImplantComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        // The first removal happens when the implant leaves the implanter, nothing to clean yet.
        if (ent.Comp.Host == null)
            return;

        Deactivate(ent);
    }

    private void OnShutdown(Entity<BloodlustImplantComponent> ent, ref ComponentShutdown args)
    {
        Deactivate(ent);
    }

    private void OnProjectileAttempt(Entity<BloodlustHostComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        TryBlockProjectile(ent, args.ProjUid, ref args);
    }

    private void OnGunShot(Entity<GunComponent> ent, ref GunShotEvent args)
    {
        TryCombatPhrase(args.User);
    }

    private void OnMeleeHit(Entity<MeleeWeaponComponent> ent, MeleeHitEvent args)
    {
        if (args.HitEntities.Count == 0)
            return;

        TryCombatPhrase(args.User);
    }

    private void OnMobStateChanged(Entity<MobStateComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.Origin is not { } killer || killer == ent.Owner)
            return;

        if (!TryGetImplant(killer, out var implant))
            return;

        if (!_random.Prob(implant.Value.Comp.KillPhraseChance))
            return;

        TrySpeak(implant.Value, implant.Value.Comp.KillPhrases);
    }

    private void TryCombatPhrase(EntityUid user)
    {
        if (!TryGetImplant(user, out var implant))
            return;

        if (!_random.Prob(implant.Value.Comp.CombatPhraseChance))
            return;

        TrySpeak(implant.Value, implant.Value.Comp.CombatPhrases);
    }

    private bool TryActivate(Entity<BloodlustImplantComponent> ent, EntityUid host)
    {
        Deactivate(ent);

        ent.Comp.Host = host;
        EnsureComp<BloodlustHostComponent>(host).Implant = ent;

        // Companion is a child of the host and is visible only on the host's visibility layer.
        var companion = Spawn(ent.Comp.CompanionPrototype, Transform(host).Coordinates);
        _transform.SetParent(companion, host);
        _transform.SetLocalPosition(companion, ent.Comp.CompanionOffset);

        var visibility = EnsureComp<VisibilityComponent>(companion);
        _visibility.SetLayer((companion, visibility), (ushort) HostVisibility);

        if (TryComp<EyeComponent>(host, out var eye))
            _eye.SetVisibilityMask(host, eye.VisibilityMask | HostVisibility, eye);

        ent.Comp.Companion = companion;
        ent.Comp.NextIdleCheck = _timing.CurTime + GetIdleDelay(ent.Comp);

        SendPhrase(ent, Loc.GetString(ent.Comp.GreetingPhrase));
        return true;
    }

    private void Deactivate(Entity<BloodlustImplantComponent> ent)
    {
        if (ent.Comp.Companion is { } companion)
            QueueDel(companion);

        if (ent.Comp.Host is { } host && !TerminatingOrDeleted(host))
        {
            RemComp<BloodlustHostComponent>(host);

            if (TryComp<EyeComponent>(host, out var eye))
                _eye.SetVisibilityMask(host, eye.VisibilityMask & ~HostVisibility, eye);
        }

        ent.Comp.Companion = null;
        ent.Comp.Host = null;
    }

    private bool TryBlockProjectile(Entity<BloodlustHostComponent> host, EntityUid projectile, ref ProjectileReflectAttemptEvent args)
    {
        if (!CanBlockProjectile(host, out var implant))
            return false;

        args.Cancelled = true;
        QueueDel(projectile);

        SpawnSpark(host, implant.Value.Comp);
        _audio.PlayPvs(implant.Value.Comp.BlockSound, host);
        return true;
    }

    private bool CanBlockProjectile(Entity<BloodlustHostComponent> host, out Entity<BloodlustImplantComponent>? implant)
    {
        implant = null;

        if (!TryComp<BloodlustImplantComponent>(host.Comp.Implant, out var comp))
            return false;

        if (!_random.Prob(comp.BlockChance))
            return false;

        implant = (host.Comp.Implant, comp);
        return true;
    }

    private void SpawnSpark(EntityUid host, BloodlustImplantComponent implant)
    {
        if (implant.SparkPrototypes.Count == 0)
            return;

        var offset = new Vector2(_random.NextFloat(-0.3f, 0.3f), _random.NextFloat(-0.3f, 0.3f));
        var spark = Spawn(_random.Pick(implant.SparkPrototypes), Transform(host).Coordinates.Offset(offset));
        _transform.SetLocalRotation(spark, _random.NextAngle());
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<BloodlustImplantComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Host is not { } host || curTime < comp.NextIdleCheck)
                continue;

            comp.NextIdleCheck = curTime + GetIdleDelay(comp);

            if (_mobState.IsIncapacitated(host) || !_random.Prob(comp.IdlePhraseChance))
                continue;

            TrySpeak((uid, comp), comp.IdlePhrases);
        }
    }

    private TimeSpan GetIdleDelay(BloodlustImplantComponent comp)
    {
        return TimeSpan.FromSeconds(_random.NextFloat(comp.IdleCheckMin, comp.IdleCheckMax));
    }

    private bool TryGetImplant(EntityUid host, out Entity<BloodlustImplantComponent>? implant)
    {
        implant = null;

        if (!TryComp<BloodlustHostComponent>(host, out var hostComp) ||
            !TryComp<BloodlustImplantComponent>(hostComp.Implant, out var comp))
            return false;

        implant = (hostComp.Implant, comp);
        return true;
    }

    private bool TrySpeak(Entity<BloodlustImplantComponent> ent, ProtoId<LocalizedDatasetPrototype> dataset)
    {
        if (_timing.CurTime < ent.Comp.NextPhrase)
            return false;

        if (!_proto.TryIndex(dataset, out var phrases) || phrases.Values.Count == 0)
            return false;

        SendPhrase(ent, Loc.GetString(_random.Pick(phrases.Values)));
        return true;
    }

    /// <summary>
    ///     Shows the phrase only to the host: popup over the companion and a line in the host's chat.
    /// </summary>
    private void SendPhrase(Entity<BloodlustImplantComponent> ent, string message)
    {
        if (ent.Comp.Host is not { } host || ent.Comp.Companion is not { } companion)
            return;

        ent.Comp.NextPhrase = _timing.CurTime + ent.Comp.PhraseCooldown;

        _popup.PopupEntity(message, companion, host, PopupType.MediumCaution);

        if (!TryComp<ActorComponent>(host, out var actor))
            return;

        var name = FormattedMessage.EscapeText(Name(companion));
        var wrapped = Loc.GetString("bloodlust-chat-wrap",
            ("name", name),
            ("message", FormattedMessage.EscapeText(message)));

        _chat.ChatMessageToOne(ChatChannel.Local, message, wrapped, companion, false, actor.PlayerSession.Channel);
    }
}
