using System.Linq;
using Content.Client._Shitmed.Medical.Surgery;
using Content.Client.Stylesheets;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
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

    private const float ChangeCheckInterval = 0.25f;
    private static readonly ResPath StatusRsiPath = new("/Textures/_Shitmed/Interface/Targeting/Status");

    private readonly SurgerySystem _surgery;
    private readonly SharedContainerSystem _container;
    private readonly SpriteSystem _sprite;

    [ViewVariables]
    private SurgeryWindow? _window;
    private SurgeryOperationsPopup? _popup;

    private Dictionary<EntityUid, List<EntProtoId>> _choices = new();
    private readonly Dictionary<EntityUid, List<SurgeryOperationEntry>> _entries = new();
    private readonly Dictionary<(EntityUid Part, EntityUid Surgery), SurgeryProgress> _progress = new();
    private readonly List<SurgeryStepSection> _sections = new();
    private readonly Dictionary<TargetBodyPart, (EntityUid Part, string? MissingSlot)> _dollSlots = new();
    private EntityUid? _part;
    private EntProtoId? _surgeryId;
    private (EntityUid Part, string? MissingSlot, string Title)? _popupSource;
    private bool _canOperate;
    private bool _autoSelected;
    private float _changeCheckTimer;
    private (int Parts, int Steps)? _snapshot;
    private int? _stepsLayout;
    private readonly List<Label> _crumbs = new();
    private readonly List<Label> _sectionHeaders = new();
    private readonly List<SurgeryStepRow> _rows = new();

    public SurgeryBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _surgery = EntMan.System<SurgerySystem>();
        _container = EntMan.System<SharedContainerSystem>();
        _sprite = EntMan.System<SpriteSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SurgeryWindow>();
        _window.Doll.OnPartPressed += OnDollPartPressed;
        _window.OnFrameUpdate += OnFrameUpdate;
        _window.BackButton.OnPressed += _ => OpenPartPopup();

        _popup = new SurgeryOperationsPopup();
        _popup.OnOperationPressed += OnOperationPressed;
        _uiManager.ModalRoot.AddChild(_popup);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || _popup == null)
            return;

        _popup.Close();
        _popup.Orphan();
        _popup = null;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not SurgeryBuiState surgeryState)
            return;

        var choices = new Dictionary<EntityUid, List<EntProtoId>>();
        foreach (var (netPart, surgeries) in surgeryState.Choices)
        {
            if (EntMan.TryGetEntity(netPart, out var part) && EntMan.HasComponent<BodyPartComponent>(part))
                choices[part.Value] = surgeries;
        }

        // The engine re-applies every BUI state that is not reference-equal, so identical choices must not rebuild the window.
        if (ChoicesEqual(choices))
            return;

        _choices = choices;
        if (!_autoSelected && _choices.Count > 0)
        {
            _autoSelected = true;
            AutoSelectPart();
        }

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

    private bool ChoicesEqual(Dictionary<EntityUid, List<EntProtoId>> choices)
    {
        if (choices.Count != _choices.Count)
            return false;

        foreach (var (part, surgeries) in choices)
        {
            if (!_choices.TryGetValue(part, out var current) || !surgeries.SequenceEqual(current))
                return false;
        }

        return true;
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
        var snapshot = Collect();
        var partsChanged = _snapshot?.Parts != snapshot.Parts;
        var stepsChanged = _snapshot?.Steps != snapshot.Steps;
        _snapshot = snapshot;

        if (partsChanged)
        {
            UpdateDoll();
            UpdatePartInfo();
            if (_popup is { Visible: true })
                PopulatePopup();
        }

        if (stepsChanged)
            UpdateSteps();

        UpdateTitle();
    }

    private (int Parts, int Steps) Collect()
    {
        _progress.Clear();
        _entries.Clear();

        if (_part is { } selected && !_choices.ContainsKey(selected))
        {
            _part = null;
            _surgeryId = null;
        }

        var hash = new HashCode();
        hash.Add(_part);
        foreach (var part in _choices.Keys)
        {
            var entries = BuildEntries(part);
            _entries[part] = entries;

            hash.Add(part);
            hash.Add(GetSeverity(part));
            foreach (var (text, _) in GetPartStates(part))
                hash.Add(text);
            foreach (var entry in entries)
                hash.Add(entry);
        }

        CollectSteps();
        var steps = new HashCode();
        steps.Add(_part);
        steps.Add(_canOperate);
        foreach (var section in _sections)
        {
            steps.Add(section.Id);
            steps.Add(section.Done);
            foreach (var step in section.Steps)
                steps.Add(step);
        }

        return (hash.ToHashCode(), steps.ToHashCode());
    }

    private void AutoSelectPart()
    {
        if (_part != null
            || _player.LocalEntity is not { } user
            || !EntMan.TryGetComponent(user, out TargetingComponent? targeting))
            return;

        _progress.Clear();
        foreach (var part in _choices.Keys)
        {
            var comp = EntMan.GetComponent<BodyPartComponent>(part);
            if (SurgeryDollControl.GetDollPart(comp.PartType, comp.Symmetry) != targeting.Target)
                continue;

            _part = part;
            _entries[part] = BuildEntries(part);
            _surgeryId = GetStarted(part).Select(e => (EntProtoId?) e.Id).FirstOrDefault();
            return;
        }
    }

    private void SelectSurgery(EntityUid part, EntProtoId surgeryId)
    {
        _part = part;
        _surgeryId = surgeryId;
        _popup?.Close();
        RefreshUI();
    }

    #region Body parts

    private void UpdateDoll()
    {
        _dollSlots.Clear();
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

            var bleeding = EntMan.TryGetComponent(part, out WoundableComponent? woundable) && woundable.Bleeds > 0;

            _dollSlots[slot] = (part, null);
            dollParts[slot] = new SurgeryDollPart(
                GetStatusTexture(slot, ((int) GetSeverity(part)).ToString()),
                bleeding ? GetStatusTexture(slot, "bleed") : null,
                IsPartOpened(part) || GetStarted(part).Any(),
                false,
                GetPartTooltip(part));
        }

        // Missing parts leave an empty slot on their parent, which is where the attach surgeries live.
        foreach (var part in _choices.Keys)
        {
            foreach (var (slotId, slot) in EntMan.GetComponent<BodyPartComponent>(part).Children)
            {
                if (SurgeryDollControl.GetDollPart(slot.Type, slot.Symmetry) is not { } dollSlot
                    || _dollSlots.ContainsKey(dollSlot)
                    || _container.TryGetContainer(part, SharedBodySystem.GetPartSlotContainerId(slotId), out var container)
                    && container.ContainedEntities.Count > 0)
                    continue;

                _dollSlots[dollSlot] = (part, slotId);
                dollParts[dollSlot] = new SurgeryDollPart(
                    GetStatusTexture(dollSlot, ((int) WoundableSeverity.Severed).ToString()),
                    null,
                    false,
                    true,
                    GetMissingPartTitle(dollSlot));
            }
        }

        _window!.Doll.SetParts(dollParts);
        _window.Doll.Visible = dollParts.Count > 0;
        _window.Doll.Selected = null;
        foreach (var (slot, (part, missing)) in _dollSlots)
        {
            if (part == _part && missing == null)
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
                Pressed = part == _part,
                StyleClasses = { StyleClass.ButtonOpenBoth },
            };
            button.OnPressed += _ =>
            {
                button.Pressed = part == _part;
                OpenPopup(part, null, Name(part), UIBox2.FromDimensions(button.GlobalPosition, button.Size));
            };
            _window.OtherParts.AddChild(button);
        }
    }

    private void UpdatePartInfo()
    {
        var window = _window!;
        window.PartStates.DisposeAllChildren();

        if (_part is not { } part)
        {
            window.PartNameLabel.Text = Loc.GetString("surgery-ui-window-no-part");
            window.PartSeverityLabel.Visible = false;
            return;
        }

        window.PartNameLabel.Text = Name(part);
        window.PartSeverityLabel.Visible = true;

        var severity = GetSeverity(part);
        window.PartSeverityLabel.Text = GetSeverityText(severity);
        window.PartSeverityLabel.SetOnlyStyleClass(GetSeverityStyle(severity));

        foreach (var (text, style) in GetPartStates(part))
        {
            var label = new Label { Text = text, HorizontalAlignment = Control.HAlignment.Center };
            label.AddStyleClass(style);
            window.PartStates.AddChild(label);
        }
    }

    private List<(string Text, string Style)> GetPartStates(EntityUid part)
    {
        var states = new List<(string, string)>();

        if (EntMan.TryGetComponent(part, out WoundableComponent? woundable) && woundable.Bleeds > 0)
            states.Add((Loc.GetString("surgery-ui-part-state-bleeding"), StyleClass.StatusBad));
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

    private bool IsPartOpened(EntityUid part)
    {
        return EntMan.HasComponent<IncisionOpenComponent>(part)
            || EntMan.HasComponent<SkinRetractedComponent>(part)
            || EntMan.HasComponent<BonesOpenComponent>(part);
    }

    private IEnumerable<SurgeryOperationEntry> GetStarted(EntityUid part)
    {
        return _entries.TryGetValue(part, out var entries)
            ? entries.Where(e => e.Group == SurgeryOperationGroup.Started)
            : [];
    }

    private string GetPartTooltip(EntityUid part)
    {
        var lines = new List<string> { Name(part), GetSeverityText(GetSeverity(part)) };
        foreach (var (text, _) in GetPartStates(part))
            lines.Add(text);

        foreach (var entry in GetStarted(part))
        {
            lines.Add(Loc.GetString("surgery-ui-part-started",
                ("surgery", entry.Name),
                ("done", entry.Done),
                ("total", entry.Total)));
        }

        var count = _entries.TryGetValue(part, out var entries)
            ? entries.Count(e => e.Group != SurgeryOperationGroup.Completed)
            : 0;
        lines.Add(Loc.GetString("surgery-ui-part-surgeries", ("count", count)));
        return string.Join('\n', lines);
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
        return _sprite.Frame0(new SpriteSpecifier.Rsi(StatusRsiPath / $"{name}.rsi", $"{name}_{state}"));
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

    #region Operations popup

    private void OnDollPartPressed(TargetBodyPart slot, UIBox2 box)
    {
        if (!_dollSlots.TryGetValue(slot, out var dollSlot))
            return;

        var title = dollSlot.MissingSlot != null ? GetMissingPartTitle(slot) : Name(dollSlot.Part);
        OpenPopup(dollSlot.Part, dollSlot.MissingSlot, title, box);
    }

    private void OpenPopup(EntityUid part, string? missingSlot, string title, UIBox2 anchor)
    {
        if (_popup == null)
            return;

        _popupSource = (part, missingSlot, title);
        PopulatePopup();
        _popup.OpenNear(anchor);
    }

    private void PopulatePopup()
    {
        if (_popup == null || _popupSource is not { } source)
            return;

        if (!_entries.TryGetValue(source.Part, out var entries))
        {
            _popup.Close();
            return;
        }

        if (source.MissingSlot is { } missingSlot)
        {
            entries = entries.FindAll(e => GetSurgery(e.Id) is { } surgery
                && EntMan.TryGetComponent(surgery, out SurgeryPartRemovedConditionComponent? removed)
                && removed.Connection == missingSlot);
        }

        var selected = source.Part == _part ? _surgeryId : null;
        _popup.Populate(source.Title, entries, selected);
    }

    private void OnOperationPressed(EntProtoId surgeryId)
    {
        if (_popupSource is { } source)
            SelectSurgery(source.Part, surgeryId);
    }

    private List<SurgeryOperationEntry> BuildEntries(EntityUid part)
    {
        var entries = new List<(int Priority, SurgeryOperationEntry Entry)>();
        foreach (var surgeryId in _choices[part])
        {
            if (GetSurgery(surgeryId) is not { } surgery
                || GetEntry(part, surgery, surgeryId) is not { } entry)
                continue;

            entries.Add((surgery.Comp.Priority, entry));
        }

        entries.Sort((a, b) =>
        {
            var group = a.Entry.Group.CompareTo(b.Entry.Group);
            if (group != 0)
                return group;

            var priority = a.Priority.CompareTo(b.Priority);
            return priority != 0 ? priority : string.Compare(a.Entry.Name, b.Entry.Name, StringComparison.CurrentCulture);
        });

        return entries.ConvertAll(e => e.Entry);
    }

    private SurgeryOperationEntry? GetEntry(EntityUid part, Entity<SurgeryComponent> surgery, EntProtoId surgeryId)
    {
        var (done, total, next, started) = GetProgress(part, surgery);
        var name = Name(surgery);
        var nextHint = next is { } nextStep ? Loc.GetString("surgery-ui-hint-next", ("step", Name(nextStep))) : null;

        // Closing surgeries are valid on every part, so a complete one only means there is nothing to close.
        if (surgery.Comp.Priority > 0)
        {
            return done < total
                ? new SurgeryOperationEntry(surgeryId, name, SurgeryOperationGroup.Closing, done, total, nextHint)
                : null;
        }

        if (GetBlockingRequirement(part, surgery, surgeryId) is { } blocker)
        {
            var hint = Loc.GetString("surgery-ui-hint-after", ("surgery", Name(blocker)));
            return new SurgeryOperationEntry(surgeryId, name, SurgeryOperationGroup.NeedsPreparation, done, total, hint);
        }

        if (done >= total)
            return new SurgeryOperationEntry(surgeryId, name, SurgeryOperationGroup.Completed, done, total, null);

        if (started)
            return new SurgeryOperationEntry(surgeryId, name, SurgeryOperationGroup.Started, done, total, nextHint);

        var startHint = next is { } firstStep ? Loc.GetString("surgery-ui-hint-start", ("step", Name(firstStep))) : null;
        return new SurgeryOperationEntry(surgeryId, name, SurgeryOperationGroup.Available, done, total, startHint);
    }

    private Entity<SurgeryComponent>? GetBlockingRequirement(EntityUid part, Entity<SurgeryComponent> surgery, EntProtoId surgeryId)
    {
        var chain = BuildChain(part, surgery, surgeryId);
        for (var i = chain.Count - 2; i >= 0; i--)
        {
            var progress = GetProgress(part, chain[i].Surgery);
            if (progress.Done < progress.Total)
                return chain[i].Surgery;
        }

        return null;
    }

    #endregion

    #region Steps

    private void CollectSteps()
    {
        _sections.Clear();
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

        foreach (var (id, chainSurgery) in BuildChain(part, surgery, surgeryId))
        {
            var (done, total, _, _) = GetProgress(part, chainSurgery);
            var complete = done >= total;

            var steps = new List<SurgeryStepData>();
            _sections.Add(new SurgeryStepSection(id, Name(chainSurgery), complete, done, total, steps));
            if (complete)
                continue;

            var number = 0;
            foreach (var stepId in chainSurgery.Comp.Steps)
            {
                if (_surgery.IsStepSkipped(part, stepId) || _surgery.GetSingleton(stepId) is not { } step)
                    continue;

                number++;
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
                    status = SurgeryStepStatus.Complete;
                else if (anyOrder || next?.Surgery.Owner == chainSurgery.Owner && nextStepId == stepId)
                    status = SurgeryStepStatus.Next;

                string? warning = null;
                if (status == SurgeryStepStatus.Next
                    && _canOperate
                    && !_surgery.CanPerformStepWithHeld(user, Owner, part, step, false, out var popup))
                    warning = popup;

                steps.Add(new SurgeryStepData(id,
                    stepId,
                    step,
                    number,
                    Name(step),
                    status,
                    warning,
                    _canOperate && _choices[part].Contains(id),
                    activeStart,
                    activeDuration));
            }
        }
    }

    private void UpdateSteps()
    {
        var window = _window!;
        window.BackButton.Visible = _part != null;
        if (_part is { } selectedPart)
            window.BackButton.Text = Loc.GetString("surgery-ui-back-to-part", ("part", Name(selectedPart)));

        if (_sections.Count == 0 || _part == null)
        {
            window.SurgeryView.Visible = false;
            window.EmptyView.Visible = true;
            return;
        }

        window.SurgeryView.Visible = true;
        window.EmptyView.Visible = false;
        window.SurgeryNameLabel.Text = _sections[^1].Name;
        window.WarningLabel.Visible = !_canOperate;
        window.Chain.Visible = _sections.Count > 1;

        // Controls are reused while the layout stays the same, so a refresh does not reset hover or scroll.
        var layout = new HashCode();
        foreach (var section in _sections)
        {
            layout.Add(section.Id);
            layout.Add(section.Steps.Count);
        }

        if (_stepsLayout != layout.ToHashCode())
        {
            _stepsLayout = layout.ToHashCode();
            BuildStepControls(window);
        }

        var current = _sections.FindIndex(s => !s.Complete);
        var total = 0;
        var done = 0;
        var row = 0;

        for (var i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            total += section.Total;
            done += section.Done;

            if (_sections.Count > 1)
            {
                SetCrumb(_crumbs[i], section, i == current);
                SetSectionHeader(_sectionHeaders[i], section);
            }

            foreach (var step in section.Steps)
            {
                _rows[row++].Set(step, EntMan.GetComponentOrNull<SpriteComponent>(step.StepEnt)?.Icon?.Default);
            }
        }

        window.Progress.MaxValue = Math.Max(total, 1);
        window.Progress.Value = done;
        window.ProgressLabel.Text = total > 0 && done >= total
            ? Loc.GetString("surgery-ui-window-complete")
            : Loc.GetString("surgery-ui-window-progress", ("done", done), ("total", total));
    }

    private void BuildStepControls(SurgeryWindow window)
    {
        window.Chain.DisposeAllChildren();
        window.Steps.DisposeAllChildren();
        _crumbs.Clear();
        _sectionHeaders.Clear();
        _rows.Clear();

        for (var i = 0; i < _sections.Count; i++)
        {
            if (_sections.Count > 1)
            {
                if (i > 0)
                    window.Chain.AddChild(new Label { Text = "→", StyleClasses = { StyleClass.LabelWeak } });

                var crumb = new Label();
                _crumbs.Add(crumb);
                window.Chain.AddChild(crumb);

                var header = new Label { Margin = new Thickness(0, 6, 0, 2) };
                _sectionHeaders.Add(header);
                window.Steps.AddChild(header);
            }

            for (var j = 0; j < _sections[i].Steps.Count; j++)
            {
                var row = new SurgeryStepRow();
                row.OnPressed += _ => OnPerformPressed(row);
                _rows.Add(row);
                window.Steps.AddChild(row);
            }
        }
    }

    private void OnPerformPressed(SurgeryStepRow row)
    {
        if (_part is { } part)
            SendPredictedMessage(new SurgeryStepChosenBuiMsg(EntMan.GetNetEntity(part), row.Data.Surgery, row.Data.Step));
    }

    private void OpenPartPopup()
    {
        if (_window is not { } window || _part is not { } part)
            return;

        var button = window.BackButton;
        OpenPopup(part, null, Name(part), UIBox2.FromDimensions(button.GlobalPosition, button.Size));
    }

    private static void SetCrumb(Label label, SurgeryStepSection section, bool current)
    {
        label.Text = section.Complete ? $"✓ {section.Name}" : section.Name;
        label.SetOnlyStyleClass(section.Complete
            ? StyleClass.StatusGood
            : current
                ? StyleClass.LabelKeyText
                : StyleClass.LabelWeak);
    }

    private static void SetSectionHeader(Label label, SurgeryStepSection section)
    {
        label.Text = section.Complete ? $"✓ {section.Name}" : section.Name;
        label.SetOnlyStyleClass(section.Complete ? StyleClass.StatusGood : StyleClass.LabelSubText);
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

        var progress = new SurgeryProgress();
        foreach (var stepId in surgery.Comp.Steps)
        {
            if (_surgery.IsStepSkipped(part, stepId))
                continue;

            var complete = _surgery.IsStepComplete(Owner, part, stepId, surgery);
            if (progress.Total++ == 0)
                progress.Started = complete;

            if (complete)
                progress.Done++;
            else
                progress.Next ??= _surgery.GetSingleton(stepId);
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

    #endregion

    private void UpdateTitle()
    {
        var partName = _part is { } part ? Name(part) : null;
        var surgeryName = _surgeryId is { } id && GetSurgery(id) is { } surgery ? Name(surgery) : null;

        _window!.Title = (partName, surgeryName) switch
        {
            ({ } p, { } s) => Loc.GetString("surgery-ui-window-title-part-surgery", ("part", p), ("surgery", s)),
            ({ } p, null) => Loc.GetString("surgery-ui-window-title-part", ("part", p)),
            _ => Loc.GetString("surgery-ui-window-title"),
        };
    }

    private Entity<SurgeryComponent>? GetSurgery(EntProtoId id)
    {
        if (_surgery.GetSingleton(id) is not { } surgery
            || !EntMan.TryGetComponent(surgery, out SurgeryComponent? comp))
            return null;

        return (surgery, comp);
    }

    private string Name(EntityUid uid) => EntMan.GetComponent<MetaDataComponent>(uid).EntityName;

    private record struct SurgeryProgress(int Done, int Total, EntityUid? Next, bool Started);
}
