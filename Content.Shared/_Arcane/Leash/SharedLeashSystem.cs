using Content.Shared.Clothing.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Arcane.Leash;

/// <summary>
/// Общая система управления логикой поводка и ошейника.
/// Отвечает за физическое привязывание сущностей, обработку контекстных действий и синхронизацию.
/// </summary>
public abstract class SharedLeashSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LeashComponent, AfterInteractEvent>(OnLeashAfterInteract);
        SubscribeLocalEvent<LeashComponent, UseInHandEvent>(OnLeashUseInHand);
        SubscribeLocalEvent<LeashComponent, GetVerbsEvent<InteractionVerb>>(AddLeashVerbs);
        SubscribeLocalEvent<CollarComponent, GotUnequippedEvent>(OnCollarUnequipped);
        // Удаление
        SubscribeLocalEvent<LeashComponent, ComponentShutdown>(OnLeashShutdown);
        SubscribeLocalEvent<LeashedComponent, ComponentShutdown>(OnLeashedShutdown);
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
    public virtual bool TryAttachLeash(EntityUid leashUid, EntityUid userUid, EntityUid targetUid, LeashComponent? leash = null)
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
            _popupSystem.PopupClient(Loc.GetString("leash-popup-self-attach"), userUid, userUid);
            return false;
        }

        // Не даём привязать если нет ошейника
        if (!HasComp<PhysicsComponent>(userUid) || !HasComp<PhysicsComponent>(targetEntity))
            return false;

        if (!TryGetEquippedCollar(targetEntity, out _))
        {
            _popupSystem.PopupClient(Loc.GetString("leash-popup-no-collar"), userUid, userUid);
            return false;
        }

        // Не даем привязать если уже есть связь
        if (HasComp<LeashedComponent>(targetEntity))
        {
            _popupSystem.PopupClient(Loc.GetString("leash-popup-already-leashed"), userUid, userUid);
            return false;
        }

        // Сохраняем ссылки и формируем уникальный ID для физического соединения
        leash.AttachedEntity = targetEntity;
        leash.JointId = $"leash_{leashUid}_{targetEntity}";

        var leashedComp = EnsureComp<LeashedComponent>(targetEntity);
        leashedComp.Leash = leashUid;

        // Попоут если привязали поводок
        _popupSystem.PopupClient(Loc.GetString("leash-popup-attached"), userUid, userUid);

        return true;
    }

    /// <summary>
    /// Удаление поводка
    /// </summary>
    public virtual bool TryDetachLeash(EntityUid leashUid, LeashComponent? leash = null, EntityUid? user = null)
    {
        if (!Resolve(leashUid, ref leash))
            return false;

        if (leash.AttachedEntity is not { } target)
            return false;

        leash.JointId = null;
        leash.AttachedEntity = null;

        // Удаление компонента LeashedComponent с привязанной сущности при разрыве связи
        if (LifeStage(target) < EntityLifeStage.Terminating)
        {
            RemCompDeferred<LeashedComponent>(target);
        }

        if (user != null)
            _popupSystem.PopupClient(Loc.GetString("leash-popup-detached"), user.Value, user.Value);

        return true;
    }
}
