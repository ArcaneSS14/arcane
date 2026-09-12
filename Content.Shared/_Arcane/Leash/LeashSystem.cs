using Content.Shared.Clothing.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using System.Numerics;

namespace Content.Shared._Arcane.Leash;

/// <summary>
/// Общая система управления логикой поводка и ошейника.
/// Отвечает за физическое привязывание сущностей, обработку контекстных действий и синхронизацию.
/// </summary>
public sealed class SharedLeashSystem : EntitySystem
{
    [Dependency] private readonly SharedJointSystem _jointSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LeashComponent, AfterInteractEvent>(OnLeashAfterInteract);
        SubscribeLocalEvent<LeashComponent, UseInHandEvent>(OnLeashUseInHand);
        SubscribeLocalEvent<LeashComponent, GetVerbsEvent<InteractionVerb>>(AddLeashVerbs);
        SubscribeLocalEvent<LeashComponent, DroppedEvent>(OnLeashDropped);
        SubscribeLocalEvent<LeashComponent, EntGotInsertedIntoContainerMessage>(OnLeashContainerInserted);
        SubscribeLocalEvent<CollarComponent, GotUnequippedEvent>(OnCollarUnequipped);
        // Удаление
        SubscribeLocalEvent<LeashComponent, ComponentShutdown>(OnLeashShutdown);
        SubscribeLocalEvent<LeashedComponent, ComponentShutdown>(OnLeashedShutdown);
        // Телепортация
        SubscribeLocalEvent<LeashComponent, MoveEvent>(OnLeashMove);
        SubscribeLocalEvent<LeashedComponent, MoveEvent>(OnLeashedMove);

    }

    /// <summary>
    /// Проверка местоположения сущностей и разрыв связи при превышении дистанции или телепортации на другую карту.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_netManager.IsServer)
            return;

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
    /// Отсоеденяет поводок при активации в руке
    /// </summary>
    private void OnLeashUseInHand(EntityUid uid, LeashComponent component, ref UseInHandEvent args)
    {
        // Прекращаем выполнение, если событие уже обработано или поводок ни к кому не привязан
        if (args.Handled || component.AttachedEntity == null)
            return;

        // Попытка отсоединения поводка с указанием инициатора (User)
        if (TryDetachLeash(uid, component, user: args.User))
        {
            args.Handled = true; // Помечаем событие как успешно завершенное
        }
    }

    /// <summary>
    /// Образует связь между поводком и ошейником при нажатии ЛКМ на цель
    /// </summary>
    private void OnLeashAfterInteract(EntityUid uid, LeashComponent component, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        // Если поводок уже привязан к другой цели, ничего не делаем
        if (component.AttachedEntity != null)
            return;

        if (TryAttachLeash(uid, args.User, target, component))
        {
            args.Handled = true;
        }
    }

    /// <summary>
    /// Добавляет возможность Отвязать поводок в контекстное меню ПКМ взаимодействия
    /// </summary>
    private void AddLeashVerbs(EntityUid uid, LeashComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (component.AttachedEntity != null)
        {
            InteractionVerb verb = new()
            {
                Text = Loc.GetString("leash-verb-detach"),
                Act = () => TryDetachLeash(uid, component, user: args.User)
            };
            args.Verbs.Add(verb);
        }
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
    /// Разрывает связь если снять ошейник
    /// </summary>
    private void OnCollarUnequipped(EntityUid uid, CollarComponent component, GotUnequippedEvent args)
    {
        if (TryComp<LeashedComponent>(args.Equipee, out var leashed) && leashed.Leash != null)
        {
            TryDetachLeash(leashed.Leash.Value);
        }
    }

    /// <summary>
    /// При удалении поводка разрывает связь
    /// </summary>
    private void OnLeashShutdown(EntityUid uid, LeashComponent component, ComponentShutdown args)
    {
        TryDetachLeash(uid, component);
    }

    /// <summary>
    /// При удалении привязаной сущности разрывает связь
    /// </summary>
    private void OnLeashedShutdown(EntityUid uid, LeashedComponent component, ComponentShutdown args)
    {
        if (component.Leash is { } targetLeashUid)
        {
            TryDetachLeash(targetLeashUid, user: null);
        }
    }

    /// <summary>
    /// Проверяет разрыв связи при перемещении поводка
    /// </summary>
    private void OnLeashMove(EntityUid uid, LeashComponent component, ref MoveEvent args)
    {
        if (!_netManager.IsServer || component.AttachedEntity is not { } target)
            return;

        CheckTeleportOrDistance(uid, component, target);
    }

    /// <summary>
    /// Проверяет разрыв связи при перемещении ошейника
    /// </summary>
    private void OnLeashedMove(EntityUid uid, LeashedComponent component, ref MoveEvent args)
    {
        if (!_netManager.IsServer || component.Leash is not { } leashUid)
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
        if (!_netManager.IsServer || leash.AttachedEntity is not { } target)
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
    /// Проверяет надет ли ошейник
    /// </summary>
    public bool TryGetEquippedCollar(EntityUid target, out EntityUid collarUid)
    {
        collarUid = default;

        // Если объект - ошейник
        if (HasComp<CollarComponent>(target))
        {
            collarUid = target;
            return true;
        }

        // Если объект в слоту NECK на сущности
        if (_inventorySystem.TryGetSlotEntity(target, "neck", out var neckItem) && HasComp<CollarComponent>(neckItem))
        {
            collarUid = neckItem.Value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Система привязки поводка с ошейником. Проверяет соблюдение условий
    /// </summary>
    public bool TryAttachLeash(EntityUid leashUid, EntityUid userUid, EntityUid targetUid, LeashComponent? leash = null)
    {
        if (!Resolve(leashUid, ref leash))
            return false;

        // Запрящяет привязать самого себя
        var targetEntity = targetUid;
        if (HasComp<CollarComponent>(targetUid) && _containerSystem.TryGetContainingContainer(targetUid, out var container))
        {
            targetEntity = container.Owner;
        }

        // Не даём привязать себя
        if (userUid == targetEntity)
        {
            _popupSystem.PopupEntity(Loc.GetString("leash-popup-self-attach"), userUid, userUid);
            return false;
        }

        // Не даём привязать если нет ошейника
        if (!HasComp<PhysicsComponent>(userUid) || !HasComp<PhysicsComponent>(targetEntity))
            return false;

        if (!TryGetEquippedCollar(targetEntity, out _))
        {
            _popupSystem.PopupEntity(Loc.GetString("leash-popup-no-collar"), userUid, userUid);
            return false;
        }

        // Не даем привязать если уже есть связь
        if (HasComp<LeashedComponent>(targetEntity))
        {
            _popupSystem.PopupEntity(Loc.GetString("leash-popup-already-leashed"), userUid, userUid);
            return false;
        }

        // Сохраняем ссылки и формируем уникальный ID для физического соединения
        leash.AttachedEntity = targetEntity;
        leash.JointId = $"leash_{leashUid}_{targetEntity}";

        var leashedComp = EnsureComp<LeashedComponent>(targetEntity);
        leashedComp.Leash = leashUid;

        // Серверная физика и синхронизация состояния компонентов
        if (_netManager.IsServer)
        {
            var joint = _jointSystem.CreateDistanceJoint(userUid, targetEntity, id: leash.JointId);
            joint.MaxLength = leash.MaxDistance;
            joint.MinLength = 0f;

            Dirty(leashUid, leash);
            Dirty(targetEntity, leashedComp);
        }

        // Попоут если привязали поводок
        _popupSystem.PopupEntity(Loc.GetString("leash-popup-attached"), userUid, userUid);

        return true;
    }

    /// <summary>
    /// Удаление поводка
    /// </summary>
    public bool TryDetachLeash(EntityUid leashUid, LeashComponent? leash = null, EntityUid? user = null)
    {
        if (!Resolve(leashUid, ref leash))
            return false;

        if (leash.AttachedEntity is not { } target)
            return false;

        // Удаление физического соединения 
        if (_netManager.IsServer)
        {
            if (leash.JointId != null)
            {
                _jointSystem.RemoveJoint(target, leash.JointId);
            }

            if (LifeStage(leashUid) < EntityLifeStage.Terminating)
            {
                Dirty(leashUid, leash);
            }
        }

        leash.JointId = null;
        leash.AttachedEntity = null;

        // Удаление компонента LeashedComponent с привязанной сущности при разрыве связи
        if (LifeStage(target) < EntityLifeStage.Terminating)
        {
            RemCompDeferred<LeashedComponent>(target);
        }

        if (user != null)
            _popupSystem.PopupEntity(Loc.GetString("leash-popup-detached"), user.Value, user.Value);

        return true;
    }
}
