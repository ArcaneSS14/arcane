// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Actions;
using Content.Client.Actions.UI;
using Content.Client.Cooldown;
using Content.Client.Stylesheets;
using Content.Shared.Actions.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Examine;
using Content.Shared.Input;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Client._Arcane.UserInterface.Systems.Actions;
using static Robust.Client.UserInterface.Controls.BoxContainer;
using static Robust.Client.UserInterface.Controls.TextureRect;
using Direction = Robust.Shared.Maths.Direction;

namespace Content.Client.UserInterface.Systems.Actions.Controls;

public sealed class ActionButton : Control, IEntityControl
{
    public const string StyleClassActionHighlightRect = "ActionHighlightRect";

    private IEntityManager _entities;
    private IPlayerManager _player;
    private SpriteSystem? _spriteSys;
    private ActionUIController? _controller;
    private bool _beingHovered;
    private bool _depressed;
    private bool _toggled;

    public BoundKeyFunction? KeyBind
    {
        set
        {
            _keybind = value;
            if (_keybind != null)
            {
                Label.Text = BoundKeyHelper.ShortKeyName(_keybind.Value);
            }
        }
    }

    private BoundKeyFunction? _keybind;

    public readonly TextureRect Button;
    public readonly PanelContainer HighlightRect;
    private readonly TextureRect _bigActionIcon;
    private readonly TextureRect _smallActionIcon;
    public readonly Label Label;
    public readonly CooldownGraphic Cooldown;
    private readonly SpriteView _smallItemSpriteView;
    private readonly SpriteView _bigItemSpriteView;

    private Texture? _buttonBackgroundTexture;

    // Arcane-Start
    private readonly TextureRect _blockedRect;
    private readonly PanelContainer _pinRect;
    private Texture? _blockedTexture;
    private FormattedMessage? _cachedName;
    private FormattedMessage? _cachedDesc;
    // Arcane-End

    public Entity<ActionComponent>? Action { get; private set; }
    public bool Locked { get; set; }
    // Arcane-Start
    public bool Pinned { get; private set; }
    public bool Unavailable { get; private set; }
    // Arcane-End

    public event Action<GUIBoundKeyEventArgs, ActionButton>? ActionPressed;
    public event Action<GUIBoundKeyEventArgs, ActionButton>? ActionUnpressed;
    public event Action<ActionButton>? ActionFocusExited;

    public ActionButton(IEntityManager entities, SpriteSystem? spriteSys = null, ActionUIController? controller = null)
    {
        // TODO why is this constructor so slooooow. The rest of the code is fine

        _entities = entities;
        _player = IoCManager.Resolve<IPlayerManager>();
        _spriteSys = spriteSys;
        _controller = controller;

        MouseFilter = MouseFilterMode.Pass;
        Button = new TextureRect
        {
            Name = "Button",
            TextureScale = new Vector2(2, 2)
        };
        HighlightRect = new PanelContainer
        {
            StyleClasses = { StyleClassActionHighlightRect },
            MinSize = new Vector2(32, 32),
            Visible = false
        };
        _bigActionIcon = new TextureRect
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Stretch = StretchMode.Scale,
            Visible = false
        };
        _smallActionIcon = new TextureRect
        {
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Bottom,
            Stretch = StretchMode.Scale,
            Visible = false
        };
        Label = new Label
        {
            Name = "Label",
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Top,
            Margin = new Thickness(5, 0, 0, 0)
        };
        _bigItemSpriteView = new SpriteView
        {
            Name = "Big Sprite",
            HorizontalExpand = true,
            VerticalExpand = true,
            Scale = new Vector2(2, 2),
            SetSize = new Vector2(64, 64),
            Visible = false,
            OverrideDirection = Direction.South,
        };
        _smallItemSpriteView = new SpriteView
        {
            Name = "Small Sprite",
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Bottom,
            Visible = false,
            OverrideDirection = Direction.South,
        };
        // padding to the left of the small icon
        var paddingBoxItemIcon = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            MinSize = new Vector2(64, 64)
        };
        paddingBoxItemIcon.AddChild(new Control()
        {
            MinSize = new Vector2(32, 32),
        });
        paddingBoxItemIcon.AddChild(new Control
        {
            Children =
            {
                _smallActionIcon,
                _smallItemSpriteView
            }
        });
        // Arcane-Start
        _blockedRect = new TextureRect
        {
            Name = "Blocked",
            TextureScale = new Vector2(2, 2),
            MouseFilter = MouseFilterMode.Ignore,
            Visible = false
        };
        _pinRect = new PanelContainer
        {
            Name = "Pinned",
            MouseFilter = MouseFilterMode.Ignore,
            MinSize = new Vector2(64, 64),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.Transparent,
                BorderColor = Color.FromHex("#8fd3ff"),
                BorderThickness = new Thickness(2)
            },
            Visible = false
        };
        // Arcane-End
        Cooldown = new CooldownGraphic {Visible = false};

        AddChild(Button);
        AddChild(_bigActionIcon);
        AddChild(_bigItemSpriteView);
        AddChild(HighlightRect);
        AddChild(Label);
        AddChild(Cooldown);
        AddChild(paddingBoxItemIcon);
        // Arcane-Start
        AddChild(_blockedRect);
        AddChild(_pinRect);
        // Arcane-End

        Button.Modulate = new Color(255, 255, 255, 150);

        OnThemeUpdated();

        OnKeyBindDown += OnPressed;
        OnKeyBindUp += OnUnpressed;

        TooltipSupplier = SupplyTooltip;
    }

    protected override void OnThemeUpdated()
    {
        base.OnThemeUpdated();
        _buttonBackgroundTexture = Theme.ResolveTexture("SlotBackground");
        // Arcane-Start
        _blockedTexture = Theme.ResolveTextureOrNull("blocked")?.Texture;
        _blockedRect.Texture = _blockedTexture;
        // Arcane-End
        Label.FontColorOverride = Theme.ResolveColorOrSpecified("whiteText");
    }

    private void OnPressed(GUIBoundKeyEventArgs args)
    {
        // Arcane-Start
        if (args.Function == ContentKeyFunctions.MouseMiddle)
        {
            ActionPressed?.Invoke(args, this);
            return;
        }
        // Arcane-End

        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.UIRightClick)
            return;

        if (args.Function == EngineKeyFunctions.UIRightClick && !Pinned) // Arcane-Edit
            Depress(args, true);

        ActionPressed?.Invoke(args, this);
    }

    private void OnUnpressed(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.UIRightClick)
            return;

        if (args.Function == EngineKeyFunctions.UIRightClick && !Pinned) // Arcane-Edit
            Depress(args, false);

        ActionUnpressed?.Invoke(args, this);
    }

    // Arcane-Start
    private Control? CreateTooltip(FormattedMessage name, FormattedMessage? desc)
    {
        var state = (string?) null;

        if (Pinned)
            state = Loc.GetString(Unavailable ? "ui-actionslot-pinned-unavailable" : "ui-actionslot-pinned");
        else if (Unavailable)
            state = Loc.GetString("ui-actionslot-unavailable");

        return new ActionAlertTooltip(name, desc, state);
    }
    // Arcane-End

    private Control? SupplyTooltip(Control sender)
    {
        // Arcane-Start
        // A pinned action keeps its last known tooltip while its entity is gone.
        var action = Action;
        if (action is null || _entities.Deleted(action.Value.Owner))
        {
            return _cachedName is { } cachedName && _cachedDesc is { } cachedDesc
                ? CreateTooltip(cachedName, cachedDesc)
                : null;
        }
        // Arcane-End

        if (!_entities.TryGetComponent(action, out MetaDataComponent? metadata)) // Arcane-Edit
            return null;

        var name = FormattedMessage.FromMarkupPermissive(metadata.EntityName);
        var desc = FormattedMessage.FromMarkupPermissive(metadata.EntityDescription);

        if (_player.LocalEntity is null)
            return null;

        // Arcane-Edit-Start
        var ev = new ExaminedEvent(desc, action.Value, _player.LocalEntity.Value, true, !desc.IsEmpty);
        _entities.EventBus.RaiseLocalEvent(action.Value.Owner, ev);
        // Arcane-Edit-End

        var newDesc = ev.GetTotalMessage();

        // Arcane-Start
        _cachedName = name;
        _cachedDesc = newDesc;
        // Arcane-End

        return CreateTooltip(name, newDesc); // Arcane-Edit
    }

    protected override void ControlFocusExited()
    {
        ActionFocusExited?.Invoke(this);
    }

    private void UpdateItemIcon()
    {
        if (Action?.Comp is not {EntityIcon: { } entity} ||
            !_entities.HasComponent<SpriteComponent>(entity))
        {
            _bigItemSpriteView.Visible = false;
            _bigItemSpriteView.SetEntity(null);
            _smallItemSpriteView.Visible = false;
            _smallItemSpriteView.SetEntity(null);
        }
        else
        {
            switch (Action?.Comp.ItemIconStyle)
            {
                case ItemActionIconStyle.BigItem:
                    _bigItemSpriteView.Visible = true;
                    _bigItemSpriteView.SetEntity(entity);
                    _smallItemSpriteView.Visible = false;
                    _smallItemSpriteView.SetEntity(null);
                    break;
                case ItemActionIconStyle.BigAction:
                    _bigItemSpriteView.Visible = false;
                    _bigItemSpriteView.SetEntity(null);
                    _smallItemSpriteView.Visible = true;
                    _smallItemSpriteView.SetEntity(entity);
                    break;
                case ItemActionIconStyle.NoItem:
                    _bigItemSpriteView.Visible = false;
                    _bigItemSpriteView.SetEntity(null);
                    _smallItemSpriteView.Visible = false;
                    _smallItemSpriteView.SetEntity(null);
                    break;
            }
        }
    }

    private void SetActionIcon(Texture? texture)
    {
        if (Action?.Comp is not {} action || texture == null)
        {
            _bigActionIcon.Texture = null;
            _bigActionIcon.Visible = false;
            _smallActionIcon.Texture = null;
            _smallActionIcon.Visible = false;
        }
        else if (action.EntityIcon != null && action.ItemIconStyle == ItemActionIconStyle.BigItem)
        {
            _smallActionIcon.Texture = texture;
            _smallActionIcon.Modulate = action.IconColor;
            _smallActionIcon.Visible = true;
            _bigActionIcon.Texture = null;
            _bigActionIcon.Visible = false;
        }
        else
        {
            _bigActionIcon.Texture = texture;
            _bigActionIcon.Modulate = action.IconColor;
            _bigActionIcon.Visible = true;
            _smallActionIcon.Texture = null;
            _smallActionIcon.Visible = false;
        }
    }

    public void UpdateIcons()
    {
        // Arcane-Start
        // A pinned action keeps the icons it had while it was available, its entity may be gone by now.
        if (Action is null && Unavailable)
        {
            UpdateBackground();
            return;
        }
        // Arcane-End

        UpdateItemIcon();
        UpdateBackground();

        if (Action is not {} action)
        {
            SetActionIcon(null);
            return;
        }

        _controller ??= UserInterfaceManager.GetUIController<ActionUIController>();
        _spriteSys ??= _entities.System<SpriteSystem>();
        var icon = action.Comp.Icon;
        if (_controller.SelectingTargetFor == action || action.Comp.Toggled)
        {
            if (action.Comp.IconOn is {} iconOn)
                icon = iconOn;

            if (action.Comp.BackgroundOn is {} background)
                _buttonBackgroundTexture = _spriteSys.Frame0(background);
        }
        else
        {
            _buttonBackgroundTexture = Theme.ResolveTexture("SlotBackground");
        }

        SetActionIcon(icon != null ? _spriteSys.Frame0(icon) : null);
    }

    public void UpdateBackground()
    {
        _controller ??= UserInterfaceManager.GetUIController<ActionUIController>();
        if (Action != null ||
            Unavailable || // Arcane
            _controller.IsDragging && GetPositionInParent() == Parent?.ChildCount - 1)
        {
            Button.Texture = _buttonBackgroundTexture;
        }
        else
        {
            Button.Texture = null;
        }
    }

    // Arcane-Start
    private void UpdateBlocked()
    {
        _blockedRect.Visible = Unavailable;
    }

    private void UpdatePin()
    {
        _pinRect.Visible = Pinned;
    }
    // Arcane-End

    public bool TryReplaceWith(EntityUid actionId, ActionsSystem system)
    {
        if (Locked)
            return false;

        UpdateData(actionId, system);
        return true;
    }

    public void UpdateData(EntityUid? actionId, ActionsSystem system)
    {
        Action = system.GetAction(actionId);

        // Arcane-Start
        _controller ??= UserInterfaceManager.GetUIController<ActionUIController>();
        Pinned = actionId != null && _controller.IsActionPinned(actionId.Value);
        Unavailable = actionId != null && _controller.IsActionUnavailable(actionId.Value);
        // Arcane-End

        Label.Visible = Action != null || Unavailable; // Arcane-Edit
        UpdateIcons();
        // Arcane-Start
        UpdateBlocked();
        UpdatePin();
        // Arcane-End
    }

    public void ClearData()
    {
        Action = null;
        // Arcane-Start
        Pinned = false;
        Unavailable = false;
        _cachedName = null;
        _cachedDesc = null;
        // Arcane-End
        Cooldown.Visible = false;
        Cooldown.Progress = 1;
        Label.Visible = false;
        UpdateIcons();
        // Arcane-Start
        UpdateBlocked();
        UpdatePin();
        // Arcane-End
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        UpdateBackground();

        Cooldown.Visible = Action?.Comp.Cooldown != null;
        if (Action?.Comp is not {} action)
            return;

        if (action.Cooldown is {} cooldown)
            Cooldown.FromTime(cooldown.Start, cooldown.End);

        if (_toggled != action.Toggled)
            _toggled = action.Toggled;
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();

        UserInterfaceManager.HoverSound();
        _beingHovered = true;
        DrawModeChanged();
    }

    protected override void MouseExited()
    {
        base.MouseExited();

        _beingHovered = false;
        DrawModeChanged();
    }

    /// <summary>
    /// Press this button down. If it was depressed and now set to not depressed, will
    /// trigger the action.
    /// </summary>
    public void Depress(GUIBoundKeyEventArgs args, bool depress)
    {
        // action can still be toggled if it's allowed to stay selected
        if (Unavailable || Action?.Comp is not {Enabled: true}) // Arcane-Edit
            return;

        _depressed = depress;
        DrawModeChanged();
    }

    public void DrawModeChanged()
    {
        _controller ??= UserInterfaceManager.GetUIController<ActionUIController>();
        HighlightRect.Visible = _beingHovered && (Action != null || _controller.IsDragging);

        // always show the normal empty button style if no action in this slot
        if (Action?.Comp is not {} action)
        {
            SetOnlyStylePseudoClass(ContainerButton.StylePseudoClassNormal);
            return;
        }

        // show a hover only if the action is usable or another action is being dragged on top of this
        if (_beingHovered && (_controller.IsDragging || !Unavailable)) // Arcane-Edit
        {
            SetOnlyStylePseudoClass(ContainerButton.StylePseudoClassHover);
        }

        // it's only depress-able if it's usable, so if we're depressed
        // show the depressed style
        if (_depressed && !_beingHovered)
        {
            HighlightRect.Visible = false;
            SetOnlyStylePseudoClass(ContainerButton.StylePseudoClassPressed);
            return;
        }

        // if it's toggled on, always show the toggled on style (currently same as depressed style)
        if (action.Toggled || _controller.SelectingTargetFor == Action?.Owner)
        {
            // when there's a toggle sprite, we're showing that sprite instead of highlighting this slot
            SetOnlyStylePseudoClass(action.IconOn != null
                ? ContainerButton.StylePseudoClassNormal
                : ContainerButton.StylePseudoClassPressed);
            return;
        }

        if (Unavailable) // Arcane-Edit
        {
            SetOnlyStylePseudoClass(ContainerButton.StylePseudoClassDisabled);
            return;
        }

        SetOnlyStylePseudoClass(ContainerButton.StylePseudoClassNormal);
    }

    EntityUid? IEntityControl.UiEntity => Action;
}
