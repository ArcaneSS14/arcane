using System.Linq;
using Content.Client._Shitmed.Medical.Surgery;
using Content.Client._Shitmed.UserInterface.Systems.Targeting;
using Content.Client.Stylesheets;
using Content.Shared._Arcane.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Surgery;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Arcane.Medical.Surgery;

[UsedImplicitly]
public sealed class SurgeryBui : BoundUserInterface
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private const float ChangeCheckInterval = 0.25f;
    private static readonly TimeSpan TargetChangeTimeout = TimeSpan.FromSeconds(1);
    private static readonly ResPath StatusRsiPath = new("/Textures/_Shitmed/Interface/Targeting/Status");

    private readonly SurgerySystem _surgery;
    private readonly SurgeryToolSystem _tools;
    private readonly SharedBodySystem _body;
    private readonly SharedContainerSystem _container;
    private readonly TargetingUIController _targeting;

    [ViewVariables]
    private SurgeryWindow? _window;

    private Dictionary<NetEntity, List<EntProtoId>> _serverChoices = new();
    private readonly Dictionary<EntityUid, List<EntProtoId>> _choices = new();
    private readonly Dictionary<EntityUid, List<SurgeryOperationEntry>> _entries = new();
    private readonly Dictionary<(EntityUid Part, EntityUid Surgery), SurgeryProgress> _progress = new();
    private readonly List<SurgeryStepData> _steps = new();
    private readonly Dictionary<TargetBodyPart, (EntityUid Part, string? MissingSlot)> _dollSlots = new();
    // Parts lost with a missing limb, like its hand or foot, stand for that limb's slot.
    private readonly Dictionary<TargetBodyPart, TargetBodyPart> _lostSlots = new();
    private EntityUid? _part;
    // A child slot of the selected part, empty or holding a part still being reattached;
    // it lists only the surgeries that attach a part there.
    private (string Slot, TargetBodyPart DollPart)? _missing;
    private EntProtoId? _surgeryId;
    private bool _canOperate;
    // With previous steps ignored, a step counts as done even when earlier ones are not.
    private bool _anyOrder;
    // The chart follows the surgeon's targeting and plan, so they are applied only when they change.
    private TargetBodyPart? _lastTarget;
    // A doll click retargets through the server, so the old target is ignored until the new one arrives.
    private (TargetBodyPart Target, TimeSpan Until)? _pendingTarget;
    private (EntityUid? Part, EntProtoId? Surgery) _lastPlan;
    private float _changeCheckTimer;
    private (int Parts, int Steps)? _snapshot;
    private int? _operationsLayout;
    private readonly List<SurgeryOperationRow> _operationRows = new();

    public SurgeryBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _surgery = EntMan.System<SurgerySystem>();
        _tools = EntMan.System<SurgeryToolSystem>();
        _targeting = _uiManager.GetUIController<TargetingUIController>();
        _body = EntMan.System<SharedBodySystem>();
        _container = EntMan.System<SharedContainerSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SurgeryWindow>();
        _window.Doll.OnPartPressed += OnDollPartPressed;
        _window.OnFrameUpdate += OnFrameUpdate;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not SurgeryBuiState surgeryState)
            return;

        // The engine re-applies every BUI state that is not reference-equal, so identical choices must not rebuild the window.
        if (SurgeryChoices.Equal(surgeryState.Choices, _serverChoices))
            return;

        _serverChoices = surgeryState.Choices;
        RefreshUI();
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is SurgeryBuiRefreshMessage)
            RefreshUI();
    }

    public void RefreshUI()
    {
        if (_window == null)
            return;

        _snapshot = null;
        RenderChanged();
    }

    private void OnFrameUpdate(FrameEventArgs args)
    {
        _changeCheckTimer += args.DeltaSeconds;
        if (_changeCheckTimer < ChangeCheckInterval)
            return;

        _changeCheckTimer = 0;

        // Damage, bleeding, held tools and do-afters change without a new BUI state, so redraw only when the shown data changed.
        RenderChanged();
    }

    private void RenderChanged()
    {
        if (IsAwaitingStepResult())
            return;

        var snapshot = Collect();
        var partsChanged = _snapshot?.Parts != snapshot.Parts;
        var stepsChanged = _snapshot?.Steps != snapshot.Steps;
        _snapshot = snapshot;

        if (partsChanged)
        {
            UpdateDoll();
            UpdatePartInfo();
        }

        if (partsChanged || stepsChanged)
            UpdateOperations();

        UpdateTitle();
    }

    private (int Parts, int Steps) Collect()
    {
        _progress.Clear();
        _entries.Clear();

        var ignore = new SurgeryIgnorePreviousStepsEvent();
        if (_player.LocalEntity is { } user)
            EntMan.EventBus.RaiseLocalEvent(user, ignore);
        _anyOrder = ignore.Handled;

        // Parts are filtered on every check: the BUI state lags behind a predicted severing, and it arrives
        // before the state of a newly attached part, since the engine applies parents before children.
        _choices.Clear();
        foreach (var (netPart, surgeries) in _serverChoices)
        {
            if (EntMan.TryGetEntity(netPart, out var part) && IsAttachedPart(part.Value))
                _choices[part.Value] = surgeries;
        }

        foreach (var part in _choices.Keys)
            _entries[part] = BuildEntries(part);

        FollowTargeting();
        ValidateSelection();
        FollowPlan();

        var hash = new HashCode();
        hash.Add(_part);
        hash.Add(_missing);
        foreach (var (part, entries) in _entries)
        {
            hash.Add(part);
            hash.Add(GetSeverity(part));
            hash.Add(IsBleeding(part));
            foreach (var (text, _) in GetPartStates(part))
                hash.Add(text);

            foreach (var entry in entries)
                hash.Add(entry);
        }

        CollectSteps();
        var steps = new HashCode();
        steps.Add(_surgeryId);
        steps.Add(_lastPlan);
        steps.Add(_canOperate);
        foreach (var step in _steps)
            steps.Add(step);

        return (hash.ToHashCode(), steps.ToHashCode());
    }

    private void FollowTargeting()
    {
        if (_choices.Count == 0
            || _player.LocalEntity is not { } user
            || !EntMan.TryGetComponent(user, out TargetingComponent? targeting))
            return;

        if (_pendingTarget is { } pending)
        {
            if (targeting.Target != pending.Target && _timing.CurTime < pending.Until)
                return;

            _pendingTarget = null;
            _lastTarget = targeting.Target;
            return;
        }

        if (targeting.Target == _lastTarget)
            return;

        _lastTarget = targeting.Target;
        SelectTarget(targeting.Target);
    }

    private void SelectTarget(TargetBodyPart target)
    {
        (EntityUid Part, (string Slot, TargetBodyPart DollPart)? Missing)? found = null;
        foreach (var part in _choices.Keys)
        {
            var comp = EntMan.GetComponent<BodyPartComponent>(part);
            if (SurgeryDollControl.GetDollPart(comp.PartType, comp.Symmetry) == target)
            {
                found = GetSelection(part, target);
                break;
            }
        }

        if (found == null)
        {
            foreach (var (parent, slot, dollPart) in GetMissingSlots())
            {
                if (dollPart != target && !IsLostWith(dollPart, target))
                    continue;

                found = (parent, (slot, dollPart));
                break;
            }
        }

        if (found is not { } selection || selection.Part == _part && selection.Missing == _missing)
            return;

        _part = selection.Part;
        _missing = selection.Missing;
        _surgeryId = GetOpenSurgery(selection.Part, selection.Missing);
    }

    // A surgery picked with a tool click becomes the open one, like picking it here.
    private void FollowPlan()
    {
        if (_player.LocalEntity is not { } user)
            return;

        var plan = EntMan.TryGetComponent(user, out SurgeryPlanComponent? comp) ? (comp.Part, comp.Surgery) : (null, null);
        if (plan == _lastPlan)
            return;

        _lastPlan = plan;
        if (plan.Part == _part
            && plan.Surgery is { } surgery
            && _part is { } part
            && GetPartEntries(part, _missing).Exists(e => e.Id == surgery))
            _surgeryId = surgery;
    }

    private void ValidateSelection()
    {
        if (_part is { } selected)
        {
            if (!_choices.ContainsKey(selected))
                SelectMissingSlotOf(selected);
            // An attached part keeps its attach surgery on the parent until the wounds are sealed, then the part itself is selected.
            else if (_missing is { } missing
                     && GetPartEntries(selected, missing).Count == 0
                     && GetSlotPart(selected, missing.Slot) is { } attached
                     && _choices.ContainsKey(attached))
            {
                _part = attached;
                _missing = null;
            }
        }

        if (_surgeryId is { } id
            && (_part is not { } part || !GetPartEntries(part, _missing).Exists(e => e.Id == id)))
            _surgeryId = _part is { } current ? GetOpenSurgery(current, _missing) : null;
    }

    // A severed part leaves an empty slot on its parent, which is where its attach surgeries live.
    private void SelectMissingSlotOf(EntityUid part)
    {
        _part = null;
        _missing = null;

        if (!EntMan.TryGetComponent(part, out BodyPartComponent? comp)
            || SurgeryDollControl.GetDollPart(comp.PartType, comp.Symmetry) is not { } dollPart)
            return;

        foreach (var (parent, slot, slotDollPart) in GetMissingSlots())
        {
            if (slotDollPart != dollPart)
                continue;

            _part = parent;
            _missing = (slot, slotDollPart);
            return;
        }
    }

    // A part still being reattached is finished by the attach surgery on its parent, like a tool click on it.
    private (EntityUid Part, (string Slot, TargetBodyPart DollPart)? Missing) GetSelection(EntityUid part, TargetBodyPart dollPart)
    {
        if (EntMan.HasComponent<BodyPartReattachedComponent>(part)
            && _body.GetParentPartAndSlotOrNull(part) is { } parent
            && _choices.ContainsKey(parent.Parent))
            return (parent.Parent, (parent.Slot, dollPart));

        return (part, null);
    }

    private void SelectPart(EntityUid part, (string Slot, TargetBodyPart DollPart)? missing)
    {
        if (_part == part && _missing == missing)
            return;

        _part = part;
        _missing = missing;
        _surgeryId = GetOpenSurgery(part, missing);
        RefreshUI();
    }

    // The open surgery is the plan, which decides what an ambiguous tool click does.
    private void ToggleSurgery(EntProtoId surgeryId)
    {
        if (_part is not { } part)
            return;

        // Picking the planned surgery again drops the plan; any other surgery, even one already open, becomes it.
        var planned = _lastPlan == (part, surgeryId);
        _surgeryId = planned ? null : (EntProtoId?) surgeryId;
        _lastPlan = planned ? (null, null) : (part, surgeryId);
        SendPredictedMessage(new SurgeryPlanBuiMsg(EntMan.GetNetEntity(part), _surgeryId));
        RefreshUI();
    }

    private EntProtoId? GetOpenSurgery(EntityUid part, (string Slot, TargetBodyPart DollPart)? missing)
    {
        var entries = GetPartEntries(part, missing);
        if (_player.LocalEntity is { } user
            && EntMan.TryGetComponent(user, out SurgeryPlanComponent? plan)
            && plan.Part == part
            && plan.Surgery is { } planned
            && entries.Exists(e => e.Id == planned))
            return planned;

        return GetStarted(entries);
    }

    private static EntProtoId? GetStarted(IEnumerable<SurgeryOperationEntry> entries)
    {
        return entries.FirstOrDefault(e => e.Status == SurgeryOperationStatus.Started)?.Id;
    }

    #region Body parts

    private void OnDollPartPressed(TargetBodyPart slot)
    {
        if (_lostSlots.TryGetValue(slot, out var limb))
            slot = limb;

        if (!_dollSlots.TryGetValue(slot, out var dollSlot))
            return;

        SelectPart(dollSlot.Part, dollSlot.MissingSlot is { } missingSlot ? (missingSlot, slot) : null);
        if (_player.LocalEntity is { } user && EntMan.HasComponent<TargetingComponent>(user))
        {
            _pendingTarget = (slot, _timing.CurTime + TargetChangeTimeout);
            _targeting.CycleTarget(slot);
        }
    }

    private void UpdateDoll()
    {
        _dollSlots.Clear();
        _lostSlots.Clear();
        var dollParts = new Dictionary<TargetBodyPart, SurgeryDollPart>();
        var otherParts = new List<EntityUid>();

        foreach (var part in _choices.Keys)
        {
            var comp = EntMan.GetComponent<BodyPartComponent>(part);
            if (SurgeryDollControl.GetDollPart(comp.PartType, comp.Symmetry) is not { } slot
                || _dollSlots.ContainsKey(slot))
            {
                otherParts.Add(part);
                continue;
            }

            var selection = GetSelection(part, slot);
            _dollSlots[slot] = (selection.Part, selection.Missing?.Slot);
            dollParts[slot] = new SurgeryDollPart(
                GetStatusTexture(slot, ((int) GetSeverity(part)).ToString()),
                IsBleeding(part) ? GetStatusTexture(slot, "bleed") : null,
                IsPartOpened(part) || GetStarted(_entries[part]) != null,
                false,
                GetPartTooltip(part));
        }

        // Missing parts leave an empty slot on their parent, which is where the attach surgeries live.
        foreach (var (part, slotId, dollSlot) in GetMissingSlots())
        {
            if (_dollSlots.ContainsKey(dollSlot))
                continue;

            _dollSlots[dollSlot] = (part, slotId);
            dollParts[dollSlot] = new SurgeryDollPart(
                GetStatusTexture(dollSlot, ((int) WoundableSeverity.Severed).ToString()),
                null,
                false,
                true,
                GetMissingPartTitle(dollSlot));
            AddLostParts(dollSlot, dollSlot, dollParts);
        }

        _window!.Doll.SetParts(dollParts);
        _window.Doll.Visible = dollParts.Count > 0;
        _window.Doll.Selected = _missing?.DollPart;
        foreach (var (slot, (part, missing)) in _dollSlots)
        {
            if (_missing == null && part == _part && missing == null)
                _window.Doll.Selected = slot;
        }

        otherParts.Sort((a, b) => GetPartScore(a).CompareTo(GetPartScore(b)));
        _window.OtherParts.DisposeAllChildren();
        _window.OtherParts.Visible = otherParts.Count > 0;
        foreach (var part in otherParts)
        {
            var button = new Button
            {
                Text = Name(part),
                ToolTip = GetPartTooltip(part),
                ToggleMode = true,
                Pressed = part == _part && _missing == null,
                StyleClasses = { StyleClass.ButtonOpenBoth },
            };
            button.OnPressed += _ => SelectPart(part, null);
            _window.OtherParts.AddChild(button);
        }
    }

    private static bool IsLostWith(TargetBodyPart limb, TargetBodyPart part)
    {
        foreach (var child in SurgeryDollControl.GetChildDollParts(limb))
        {
            if (child == part || IsLostWith(child, part))
                return true;
        }

        return false;
    }

    private void AddLostParts(TargetBodyPart limb, TargetBodyPart parent, Dictionary<TargetBodyPart, SurgeryDollPart> dollParts)
    {
        foreach (var child in SurgeryDollControl.GetChildDollParts(parent))
        {
            if (dollParts.ContainsKey(child))
                continue;

            _lostSlots[child] = limb;
            dollParts[child] = new SurgeryDollPart(
                GetStatusTexture(child, ((int) WoundableSeverity.Severed).ToString()),
                null,
                false,
                true,
                GetMissingPartTitle(child));
            AddLostParts(limb, child, dollParts);
        }
    }

    private IEnumerable<(EntityUid Parent, string Slot, TargetBodyPart DollPart)> GetMissingSlots()
    {
        foreach (var part in _choices.Keys)
        {
            foreach (var (slotId, slot) in EntMan.GetComponent<BodyPartComponent>(part).Children)
            {
                if (SurgeryDollControl.GetDollPart(slot.Type, slot.Symmetry) is not { } dollPart
                    || GetSlotPart(part, slotId) != null)
                    continue;

                yield return (part, slotId, dollPart);
            }
        }
    }

    private EntityUid? GetSlotPart(EntityUid parent, string slot)
    {
        return _container.TryGetContainer(parent, SharedBodySystem.GetPartSlotContainerId(slot), out var container)
            && container.ContainedEntities.Count > 0
                ? container.ContainedEntities[0]
                : null;
    }

    // The selected part, or the one being reattached into the selected slot.
    private EntityUid? GetShownPart()
    {
        if (_part is not { } part)
            return null;

        return _missing is { } missing ? GetSlotPart(part, missing.Slot) : part;
    }

    private bool IsAttachedPart(EntityUid part)
    {
        return EntMan.TryGetComponent(part, out BodyPartComponent? comp) && comp.Body == Owner;
    }

    private void UpdatePartInfo()
    {
        var window = _window!;
        window.PartStates.DisposeAllChildren();

        var shown = GetShownPart();
        window.PartInfo.Visible = shown != null;
        window.PartStatesDivider.Visible = shown != null;
        window.PartStatesView.Visible = shown != null;
        window.PartNameLabel.Text = GetPartTitle() ?? Loc.GetString("surgery-ui-window-no-part");

        if (shown is not { } part)
            return;

        var severity = GetSeverity(part);
        window.PartSeverityLabel.Text = GetSeverityText(severity);
        window.PartSeverityLabel.SetOnlyStyleClass(GetSeverityStyle(severity));

        var bleeding = IsBleeding(part);
        window.PartBleedingLabel.Text = Loc.GetString(bleeding ? "surgery-ui-part-bleeding-yes" : "surgery-ui-part-bleeding-no");
        window.PartBleedingLabel.SetOnlyStyleClass(bleeding ? StyleClass.StatusBad : StyleClass.StatusGood);

        var states = GetPartStates(part);
        if (states.Count == 0)
            states.Add((Loc.GetString("surgery-ui-part-states-none"), StyleClass.LabelWeak));

        foreach (var (text, style) in states)
        {
            var label = new Label { Text = Loc.GetString("surgery-ui-part-state-entry", ("state", text)) };
            label.AddStyleClass(style);
            window.PartStates.AddChild(label);
        }
    }

    private List<(string Text, string Style)> GetPartStates(EntityUid part)
    {
        var states = new List<(string, string)>();

        if (EntMan.HasComponent<IncisionOpenComponent>(part))
            states.Add((Loc.GetString("surgery-ui-part-state-incision"), StyleClass.StatusWarning));
        if (EntMan.HasComponent<SkinRetractedComponent>(part))
            states.Add((Loc.GetString("surgery-ui-part-state-skin-retracted"), StyleClass.StatusWarning));
        if (EntMan.HasComponent<BleedersClampedComponent>(part))
            states.Add((Loc.GetString("surgery-ui-part-state-clamped"), StyleClass.StatusOkay));
        if (EntMan.HasComponent<BonesSawedComponent>(part))
            states.Add((Loc.GetString("surgery-ui-part-state-bones-sawed"), StyleClass.StatusWarning));
        if (EntMan.HasComponent<BonesOpenComponent>(part))
            states.Add((Loc.GetString("surgery-ui-part-state-bones-open"), StyleClass.StatusWarning));

        return states;
    }

    private bool IsBleeding(EntityUid part)
    {
        return EntMan.TryGetComponent(part, out WoundableComponent? woundable) && woundable.Bleeds > 0;
    }

    private bool IsPartOpened(EntityUid part)
    {
        return EntMan.HasComponent<IncisionOpenComponent>(part)
            || EntMan.HasComponent<SkinRetractedComponent>(part)
            || EntMan.HasComponent<BonesOpenComponent>(part);
    }

    private string GetPartTooltip(EntityUid part)
    {
        var lines = new List<string> { Name(part), GetSeverityText(GetSeverity(part)) };
        if (IsBleeding(part))
            lines.Add(Loc.GetString("surgery-ui-part-state-bleeding"));

        foreach (var (text, _) in GetPartStates(part))
            lines.Add(text);

        var entries = _entries[part];
        foreach (var entry in entries.Where(e => e.Status == SurgeryOperationStatus.Started))
        {
            lines.Add(Loc.GetString("surgery-ui-part-started",
                ("surgery", entry.Name),
                ("done", entry.Done),
                ("total", entry.Total)));
        }

        var count = entries.Count(e => e.Status != SurgeryOperationStatus.Completed);
        lines.Add(Loc.GetString("surgery-ui-part-surgeries", ("count", count)));
        return string.Join('\n', lines);
    }

    private string? GetPartTitle()
    {
        if (GetShownPart() is { } part)
            return Name(part);

        return _missing is { } missing ? GetMissingPartTitle(missing.DollPart) : null;
    }

    private WoundableSeverity GetSeverity(EntityUid part)
    {
        return EntMan.TryGetComponent(part, out WoundableComponent? woundable)
            ? woundable.WoundableSeverity
            : WoundableSeverity.Healthy;
    }

    private Texture GetStatusTexture(TargetBodyPart slot, string state)
    {
        var name = slot.ToString().ToLowerInvariant();
        // Loaded outside the RSI atlas, since the doll outline shader samples past the frame edge into neighbouring sprites.
        return _resourceCache.GetResource<TextureResource>(StatusRsiPath / $"{name}.rsi" / $"{name}_{state}.png").Texture;
    }

    private static string GetMissingPartTitle(TargetBodyPart slot)
    {
        var part = Loc.GetString($"surgery-ui-part-{slot.ToString().ToLowerInvariant()}");
        return Loc.GetString("surgery-ui-part-missing", ("part", part));
    }

    private static string GetSeverityText(WoundableSeverity severity)
    {
        return Loc.GetString($"surgery-ui-severity-{severity.ToString().ToLowerInvariant()}");
    }

    private static string GetSeverityStyle(WoundableSeverity severity)
    {
        return severity switch
        {
            WoundableSeverity.Healthy => StyleClass.StatusGood,
            WoundableSeverity.Minor => StyleClass.StatusOkay,
            WoundableSeverity.Moderate => StyleClass.StatusWarning,
            WoundableSeverity.Severe => StyleClass.StatusBad,
            _ => StyleClass.StatusCritical,
        };
    }

    private int GetPartScore(EntityUid part)
    {
        return EntMan.GetComponent<BodyPartComponent>(part).PartType switch
        {
            BodyPartType.Head => 1,
            BodyPartType.Chest => 2,
            BodyPartType.Groin => 3,
            BodyPartType.Arm => 4,
            BodyPartType.Hand => 5,
            BodyPartType.Leg => 6,
            BodyPartType.Foot => 7,
            BodyPartType.Tail => 8,
            _ => 9,
        };
    }

    #endregion

    #region Operations

    private void UpdateOperations()
    {
        var window = _window!;
        window.WarningLabel.Visible = _part != null && !_canOperate;

        var entries = _part is { } part ? GetPartEntries(part, _missing) : [];
        window.OperationsView.Visible = entries.Count > 0;
        window.EmptyView.Visible = entries.Count == 0;
        window.EmptyHintLabel.Visible = _part == null;
        window.EmptyLabel.Text = Loc.GetString(_part == null ? "surgery-ui-window-empty" : "surgery-ui-operations-empty");

        // Rows keep their order as the operation progresses and are reused, so a refresh does not reset hover or scroll.
        var layout = new HashCode();
        foreach (var entry in entries)
            layout.Add(entry.Id);

        if (_operationsLayout != layout.ToHashCode())
        {
            _operationsLayout = layout.ToHashCode();
            BuildOperationControls(window, entries);
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var steps = entry.Id == _surgeryId ? _steps : [];
            _operationRows[i].Set(entry, steps, _lastPlan == (_part, entry.Id), GetIcon);
        }
    }

    private void BuildOperationControls(SurgeryWindow window, List<SurgeryOperationEntry> entries)
    {
        window.Operations.DisposeAllChildren();
        _operationRows.Clear();

        foreach (var entry in entries)
        {
            var id = entry.Id;
            var row = new SurgeryOperationRow();
            row.OnHeaderPressed += () => ToggleSurgery(id);
            _operationRows.Add(row);
            window.Operations.AddChild(row);
        }
    }

    private List<SurgeryOperationEntry> GetPartEntries(EntityUid part, (string Slot, TargetBodyPart DollPart)? missing)
    {
        if (!_entries.TryGetValue(part, out var entries))
            return [];

        if (missing is not { } slot)
            return entries;

        return entries.FindAll(e => GetSurgery(e.Id) is { } surgery
            && EntMan.TryGetComponent(surgery, out SurgeryPartRemovedConditionComponent? removed)
            && removed.Connection == slot.Slot);
    }

    private List<SurgeryOperationEntry> BuildEntries(EntityUid part)
    {
        // Surgeries that only prepare for others, like opening an incision, are shown as steps of those others.
        var required = new HashSet<EntProtoId>();
        foreach (var surgeryId in _choices[part])
        {
            var requirementId = GetSurgery(surgeryId)?.Comp.Requirement;
            while (requirementId is { } id && required.Add(id))
                requirementId = GetSurgery(id)?.Comp.Requirement;
        }

        var entries = new List<(int Priority, SurgeryOperationEntry Entry)>();
        foreach (var surgeryId in _choices[part])
        {
            if (required.Contains(surgeryId)
                || GetSurgery(surgeryId) is not { } surgery
                || GetEntry(part, surgery, surgeryId) is not { } entry)
                continue;

            entries.Add((surgery.Comp.Priority, entry));
        }

        entries.Sort((a, b) =>
        {
            var priority = a.Priority.CompareTo(b.Priority);
            return priority != 0 ? priority : string.Compare(a.Entry.Name, b.Entry.Name, StringComparison.CurrentCulture);
        });

        return entries.ConvertAll(e => e.Entry);
    }

    private SurgeryOperationEntry? GetEntry(EntityUid part, Entity<SurgeryComponent> surgery, EntProtoId surgeryId)
    {
        var own = GetProgress(part, surgery);
        var (done, total) = (0, 0);
        var inOrder = true;
        foreach (var (_, chainSurgery) in BuildChain(part, surgery, surgeryId))
        {
            var progress = GetProgress(part, chainSurgery);
            total += progress.Total;
            if (inOrder || _anyOrder)
                done += progress.Done;

            inOrder &= progress.Done >= progress.Total;
        }

        // The surgery's own steps come last in its chain, so it is started once one of them counts.
        var started = _anyOrder ? own.Done > 0 : done > total - own.Total;

        SurgeryOperationStatus status;
        // Closing surgeries are valid on every part, so a complete one only means there is nothing to close.
        if (surgery.Comp.Priority > 0)
        {
            if (own.Done >= own.Total)
                return null;

            status = SurgeryOperationStatus.Closing;
        }
        else if (done >= total)
            status = SurgeryOperationStatus.Completed;
        else if (started)
            status = SurgeryOperationStatus.Started;
        else
            status = SurgeryOperationStatus.Available;

        return new SurgeryOperationEntry(surgeryId, Name(surgery), status, done, total);
    }

    #endregion

    #region Steps

    private void CollectSteps()
    {
        _steps.Clear();
        _canOperate = EntMan.TryGetComponent(Owner, out SurgeryTargetComponent? target) && target.CanOperate;

        if (_part is not { } part
            || _surgeryId is not { } surgeryId
            || GetSurgery(surgeryId) is not { } surgery
            || _player.LocalEntity is not { } user)
            return;

        var next = _surgery.GetNextStep(Owner, part, surgery, user);
        // A negative index means the user may perform any incomplete step in any order.
        var anyOrder = next is { Step: < 0 };
        EntProtoId? nextStepId = next is { Step: >= 0 } found ? found.Surgery.Comp.Steps[found.Step] : (EntProtoId?) null;
        var active = GetActiveStep(user, part);
        var reached = false;

        foreach (var (id, chainSurgery) in BuildChain(part, surgery, surgeryId))
        {
            var stage = id == surgeryId ? null : Name(chainSurgery);
            foreach (var stepId in chainSurgery.Comp.Steps)
            {
                if (_surgery.IsStepSkipped(part, stepId) || _surgery.GetSingleton(stepId) is not { } step)
                    continue;

                var status = SurgeryStepStatus.Locked;
                var activeStart = TimeSpan.Zero;
                var activeDuration = TimeSpan.Zero;
                if (active is { } activeStep && activeStep.Surgery == id && activeStep.Step == stepId)
                {
                    status = SurgeryStepStatus.Active;
                    activeStart = activeStep.Start;
                    activeDuration = activeStep.Duration;
                }
                else if (_surgery.IsStepComplete(Owner, part, stepId, chainSurgery))
                    status = reached ? SurgeryStepStatus.Satisfied : SurgeryStepStatus.Complete;
                else if (anyOrder || next?.Surgery.Owner == chainSurgery.Owner && nextStepId == stepId)
                    status = SurgeryStepStatus.Next;

                if (!anyOrder && status is SurgeryStepStatus.Active or SurgeryStepStatus.Next)
                    reached = true;

                string? warning = null;
                if (status == SurgeryStepStatus.Next
                    && _canOperate
                    && !_surgery.CanPerformStepWithHeld(user, Owner, part, step, false, out var popup))
                    warning = popup;

                _steps.Add(new SurgeryStepData(step,
                    Name(step),
                    stage,
                    status,
                    _tools.GetToolName(step),
                    warning,
                    activeStart,
                    activeDuration));
            }
        }
    }

    // The chain ends with the surgery itself; requirements the part skips entirely are left out.
    private List<(EntProtoId Id, Entity<SurgeryComponent> Surgery)> BuildChain(EntityUid part, Entity<SurgeryComponent> surgery, EntProtoId surgeryId)
    {
        var chain = new List<(EntProtoId, Entity<SurgeryComponent>)> { (surgeryId, surgery) };
        var visited = new HashSet<EntProtoId> { surgeryId };

        var requirementId = surgery.Comp.Requirement;
        while (requirementId is { } id && visited.Add(id) && GetSurgery(id) is { } requirement)
        {
            if (!_surgery.IsSurgerySkipped(part, requirement.AsNullable()))
                chain.Insert(0, (id, requirement));

            requirementId = requirement.Comp.Requirement;
        }

        return chain;
    }

    private SurgeryProgress GetProgress(EntityUid part, Entity<SurgeryComponent> surgery)
    {
        if (_progress.TryGetValue((part, surgery.Owner), out var cached))
            return cached;

        var progress = new SurgeryProgress(0, 0);
        var inOrder = true;
        foreach (var stepId in surgery.Comp.Steps)
        {
            if (_surgery.IsStepSkipped(part, stepId))
                continue;

            var complete = _surgery.IsStepComplete(Owner, part, stepId, surgery);
            progress.Total++;

            // A later step may already pass its check, like taking an item out of an empty cavity, but is not done yet.
            if (complete && (inOrder || _anyOrder))
                progress.Done++;

            inOrder &= complete;
        }

        return _progress[(part, surgery.Owner)] = progress;
    }

    private (EntProtoId Surgery, EntProtoId Step, TimeSpan Start, TimeSpan Duration)? GetActiveStep(EntityUid user, EntityUid part)
    {
        if (!EntMan.HasComponent<ActiveDoAfterComponent>(user)
            || !EntMan.TryGetComponent(user, out DoAfterComponent? doAfters))
            return null;

        foreach (var doAfter in doAfters.DoAfters.Values)
        {
            if (doAfter.Cancelled || doAfter.Completed)
                continue;

            if (doAfter.Args.Event is SurgeryDoAfterEvent surgeryEvent
                && doAfter.Args.EventTarget == Owner
                && doAfter.Args.Target == part)
                return (surgeryEvent.Surgery, surgeryEvent.Step, doAfter.StartTime, doAfter.Args.Delay);
        }

        return null;
    }

    // The client applies a finished step only on its first prediction and drops it on rollback until the server confirms it,
    // so redrawing in between would flash the step back to incomplete.
    private bool IsAwaitingStepResult()
    {
        if (_player.LocalEntity is not { } user
            || !EntMan.TryGetComponent(user, out DoAfterComponent? doAfters))
            return false;

        foreach (var doAfter in doAfters.DoAfters.Values)
        {
            if (doAfter.Completed
                && !doAfter.Cancelled
                && doAfter.Args.Event is SurgeryDoAfterEvent surgeryEvent
                && doAfter.Args.EventTarget == Owner
                && doAfter.Args.Target is { } part
                && GetSurgery(surgeryEvent.Surgery) is { } surgery
                && !_surgery.IsStepComplete(Owner, part, surgeryEvent.Step, surgery))
                return true;
        }

        return false;
    }

    #endregion

    private void UpdateTitle()
    {
        var partName = GetPartTitle();
        var surgeryName = _surgeryId is { } id && GetSurgery(id) is { } surgery ? Name(surgery) : null;

        _window!.Title = (partName, surgeryName) switch
        {
            ({ } p, { } s) => Loc.GetString("surgery-ui-window-title-part-surgery", ("part", p), ("surgery", s)),
            ({ } p, _) => Loc.GetString("surgery-ui-window-title-part", ("part", p)),
            _ => Loc.GetString("surgery-ui-window-title"),
        };
    }

    private Texture? GetIcon(EntityUid step)
    {
        return EntMan.GetComponentOrNull<SpriteComponent>(step)?.Icon?.Default;
    }

    private Entity<SurgeryComponent>? GetSurgery(EntProtoId id)
    {
        if (_surgery.GetSingleton(id) is not { } surgery
            || !EntMan.TryGetComponent(surgery, out SurgeryComponent? comp))
            return null;

        return (surgery, comp);
    }

    private string Name(EntityUid uid) => EntMan.GetComponent<MetaDataComponent>(uid).EntityName;

    private record struct SurgeryProgress(int Done, int Total);
}
