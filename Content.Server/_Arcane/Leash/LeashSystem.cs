using Content.Shared._Arcane.Leash;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using System.Numerics;

namespace Content.Server._Arcane.Leash;

public sealed class LeashSystem : SharedLeashSystem
{
    [Dependency] private readonly SharedJointSystem _jointSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LeashComponent, DroppedEvent>(OnLeashDropped);
        SubscribeLocalEvent<LeashComponent, EntGotInsertedIntoContainerMessage>(OnLeashContainerInserted);
        // Телепортация
        SubscribeLocalEvent<LeashComponent, MoveEvent>(OnLeashMove);
        SubscribeLocalEvent<LeashedComponent, MoveEvent>(OnLeashedMove);
    }

    /// <summary>
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LeashComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var leash, out var xform))
        {
            if (leash.AttachedEntity is not { } target)
                continue;

            if (!TryComp<TransformComponent>(target, out var targetXform))
            {
                TryDetachLeash(uid, leash);
                continue;
            }

            if (xform.MapID != targetXform.MapID ||
                Vector2.Distance(_transform.GetWorldPosition(xform), _transform.GetWorldPosition(targetXform)) > 5.0f)
            {
                TryDetachLeash(uid, leash);
                _popupSystem.PopupEntity(Loc.GetString("leash-popup-snap"), target, target);
            }
        }
    }

    /// <summary>
    /// Перепривязка связи к поводку если тот выброшен
    /// </summary>
    private void OnLeashDropped(EntityUid uid, LeashComponent component, ref DroppedEvent args)
    {
        if (component.AttachedEntity == null)
            return;

        ReanchorJoint(uid, component, newAnchor: uid);
    }

    /// <summary>
    /// Перемещение поводка в контейнер или в руку другого существа
    /// </summary>
    private void OnLeashContainerInserted(EntityUid uid, LeashComponent component, ref EntGotInsertedIntoContainerMessage args)
    {
        if (component.AttachedEntity == null)
            return;

        var containerOwner = args.Container.Owner;

        // Если поводок передали
        if (_handsSystem.IsHolding(containerOwner, uid, out _))
        {
            ReanchorJoint(uid, component, newAnchor: containerOwner);
        }
        else
        {
            // Если поводок в контейнере
            TryDetachLeash(uid, component);

            if (args.Container.Owner != default)
            {
                _popupSystem.PopupEntity(Loc.GetString("leash-popup-detached-container"), args.Container.Owner, args.Container.Owner);
            }
        }
    }

    /// <summary>
    /// Проверяет разрыв связи при перемещении поводка
    /// </summary>
    private void OnLeashMove(EntityUid uid, LeashComponent component, ref MoveEvent args)
    {
        if (component.AttachedEntity is not { } target)
            return;

        CheckTeleportOrDistance(uid, component, target);
    }

    /// <summary>
    /// Проверяет разрыв связи при перемещении ошейника
    /// </summary>
    private void OnLeashedMove(EntityUid uid, LeashedComponent component, ref MoveEvent args)
    {
        if (component.Leash is not { } leashUid)
            return;

        if (TryComp<LeashComponent>(leashUid, out var leashComp))
        {
            CheckTeleportOrDistance(leashUid, leashComp, uid);
        }
    }

    /// <summary>
    /// Мгновенная проверка расстояния между объектами при любом их сдвиге
    /// </summary>
    private void CheckTeleportOrDistance(EntityUid leashUid, LeashComponent leash, EntityUid targetUid)
    {
        if (!TryComp<TransformComponent>(leashUid, out var leashXform) ||
            !TryComp<TransformComponent>(targetUid, out var targetXform))
        {
            return;
        }

        // Если при телепортации сменилась карта или расстояние превысило предел — мгновенно рвем поводок
        if (leashXform.MapID != targetXform.MapID ||
            Vector2.Distance(_transform.GetWorldPosition(leashXform), _transform.GetWorldPosition(targetXform)) > 5.0f)
        {
            TryDetachLeash(leashUid, leash);
            _popupSystem.PopupEntity(Loc.GetString("leash-popup-snap"), targetUid, targetUid);
        }
    }

    /// <summary>
    /// Создание ограничения привязаной сущности
    /// </summary>
    private void ReanchorJoint(EntityUid leashUid, LeashComponent leash, EntityUid newAnchor)
    {
        if (leash.AttachedEntity is not { } target)
            return;

        if (leash.JointId != null)
        {
            _jointSystem.RemoveJoint(target, leash.JointId);
            _jointSystem.RemoveJoint(leashUid, leash.JointId);
            _jointSystem.RemoveJoint(newAnchor, leash.JointId);
        }

        if (!HasComp<PhysicsComponent>(newAnchor) || !HasComp<PhysicsComponent>(target))
            return;

        leash.JointId = $"leash_{leashUid}_{Guid.NewGuid()}";

        var joint = _jointSystem.CreateDistanceJoint(newAnchor, target, id: leash.JointId!);
        joint.MaxLength = leash.MaxDistance;
        joint.MinLength = 0f;
    }

    /// <summary>
    /// Система привязки поводка с ошейником. Проверяет соблюдение условий
    /// </summary>
    public override bool TryAttachLeash(EntityUid leashUid, EntityUid userUid, EntityUid targetUid, LeashComponent? leash = null)
    {
        if (!base.TryAttachLeash(leashUid, userUid, targetUid, leash))
            return false;

        if (!Resolve(leashUid, ref leash) || leash.AttachedEntity is not { } targetEntity)
            return false;

        // Серверная физика и синхронизация состояния компонентов
        var joint = _jointSystem.CreateDistanceJoint(userUid, targetEntity, id: leash.JointId!);
        joint.MaxLength = leash.MaxDistance;
        joint.MinLength = 0f;

        Dirty(leashUid, leash);
        if (TryComp<LeashedComponent>(targetEntity, out var leashedComp))
        {
            Dirty(targetEntity, leashedComp);
        }

        return true;
    }

    /// <summary>
    /// Удаление поводка
    /// </summary>
    public override bool TryDetachLeash(EntityUid leashUid, LeashComponent? leash = null, EntityUid? user = null)
    {
        if (!Resolve(leashUid, ref leash))
            return false;

        var target = leash.AttachedEntity;

        // Удаление физического соединения 
        if (leash.JointId != null && target != null)
        {
            _jointSystem.RemoveJoint(target.Value, leash.JointId);
        }

        if (LifeStage(leashUid) < EntityLifeStage.Terminating)
        {
            Dirty(leashUid, leash);
        }

        return base.TryDetachLeash(leashUid, leash, user);
    }
}
