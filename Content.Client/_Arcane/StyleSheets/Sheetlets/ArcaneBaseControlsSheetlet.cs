using System.Numerics;
using Content.Client.ContextMenu.UI;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Palette;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.Verbs.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Arcane.StyleSheets.Sheetlets;

public sealed class ArcaneBaseControlsSheetlet : Sheetlet<ArcaneStylesheet>
{
    private static readonly (string StyleClass, Color Color)[] DepartmentButtonColors =
    [
        ("ButtonColorCentralCommandDepartment", Color.FromHex("#57B85E")),
        ("ButtonColorCommandDepartment", Color.FromHex("#4F8FD6")),
        ("ButtonColorSecurityDepartment", Color.FromHex("#D65353")),
        ("ButtonColorMedicalDepartment", Color.FromHex("#60B7E4")),
        ("ButtonColorEngineeringDepartment", Color.FromHex("#D6B93F")),
        ("ButtonColorCargoDepartment", Color.FromHex("#D99038")),
        ("ButtonColorScienceDepartment", Color.FromHex("#B15AB5")),
        ("ButtonColorSiliconDepartment", Color.FromHex("#39B9AD")),
        ("ButtonColorCivilianDepartment", Color.FromHex("#79B58F")),
        ("ButtonColorJusticeDepartment", Color.FromHex("#D85A7B")),
        ("ButtonColorLegalDepartment", Color.FromHex("#EB6B8D")),
        ("ButtonColorSpecificDepartment", Color.FromHex("#9BA3A8")),
        ("ButtonColorAntagonistDepartment", Color.FromHex("#B85A63")),
    ];

    public override StyleRule[] GetRules(ArcaneStylesheet sheet, object config)
    {
        var buttonTexture = sheet.GetTextureOr(new("button.svg.96dpi.png"), NanotrasenStylesheet.TextureRoot);
        var buttonBorderTexture = sheet.GetTextureOr(new("button_bordered.svg.96dpi.png"),
            ArcaneStylesheet.TextureRoot);
        var buttonBorder = sheet.PrimaryPalette.Base.WithAlpha(0.62f);
        var modulatedPanel = StrictBox(Color.White, buttonBorder, 1, 0, 0);
        var modulatedPanelOpenLeft = StrictBox(Color.White, buttonBorder,
            new Thickness(0, 1, 1, 1), 0, 0);
        var modulatedPanelOpenRight = StrictBox(Color.White, buttonBorder,
            new Thickness(1, 1, 0, 1), 0, 0);
        var panelLight = StrictBox(sheet.SecondaryPalette.BackgroundLight, buttonBorder, 1, 0, 0);
        var panelDark = StrictBox(sheet.SecondaryPalette.Background, buttonBorder, 1, 0, 0);
        var panelDarker = StrictBox(sheet.SecondaryPalette.BackgroundDark, buttonBorder, 1, 0, 0);
        var lineEdit = StrictBox(sheet.SecondaryPalette.Element, buttonBorder, 1, 8, 4);
        var sliderBackground = StrictBox(sheet.SecondaryPalette.BackgroundDark, buttonBorder, 1, 0, 6);
        var sliderFill = StrictBox(sheet.HighlightPalette.Element, sheet.HighlightPalette.Base.WithAlpha(0.65f), 1, 0, 6);
        var sliderOutline = StrictBox(Color.Transparent, sheet.PrimaryPalette.TextDark.WithAlpha(0.68f), 1, 0, 6);
        var sliderGrabber = StrictBox(sheet.HighlightPalette.TextDark, ArcanePalette.NeonOutline, 1, 4, 8);
        var tabActive = StrictBox(sheet.PrimaryPalette.Element, sheet.PrimaryPalette.Base.WithAlpha(0.7f), 1, 8, 4);
        var tabInactive = StrictBox(sheet.SecondaryPalette.Element, buttonBorder, 1, 8, 4);
        var progressBackground = StrictBox(sheet.SecondaryPalette.BackgroundDark, buttonBorder, 1, 0, 14);
        var progressForeground = StrictBox(sheet.HighlightPalette.Element, sheet.HighlightPalette.Base.WithAlpha(0.7f), 1, 0, 14);
        var scrollbar = StrictBox(sheet.PrimaryPalette.Element.WithAlpha(0.6f), buttonBorder, 1, 0, 0);
        var scrollbarHovered = StrictBox(sheet.HighlightPalette.Element.WithAlpha(0.85f), ArcanePalette.NeonOutline, 1, 0, 0);
        var scrollbarPressed = StrictBox(sheet.HighlightPalette.TextDark.WithAlpha(0.95f), ArcanePalette.NeonOutline, 1, 0, 0);
        scrollbar.SetContentMarginOverride(StyleBox.Margin.Left | StyleBox.Margin.Top, 10);
        scrollbarHovered.SetContentMarginOverride(StyleBox.Margin.Left | StyleBox.Margin.Top, 10);
        scrollbarPressed.SetContentMarginOverride(StyleBox.Margin.Left | StyleBox.Margin.Top, 10);
        var tooltip = StrictBox(sheet.SecondaryPalette.BackgroundDark.WithAlpha(0.96f), buttonBorder, 1, 8, 5);
        var windowPanel = StrictBox(sheet.SecondaryPalette.Background.WithAlpha(0.98f), buttonBorder, 1, 8, 6);
        var contextMenuPanel = StrictBox(sheet.SecondaryPalette.BackgroundDark.WithAlpha(0.99f),
            sheet.SecondaryPalette.Base.WithAlpha(0.68f), 1, 0, 0);
        var contextMenuPalette = ArcanePalette.Buttons with
        {
            Element = sheet.SecondaryPalette.Background,
            HoveredElement = sheet.PrimaryPalette.Element,
            PressedElement = sheet.PrimaryPalette.PressedElement,
            DisabledElement = sheet.SecondaryPalette.BackgroundDark,
        };
        var windowHeader = StrictBox(sheet.PrimaryPalette.BackgroundLight, buttonBorder, 1, 8, 3);
        var alertHeader = StrictBox(sheet.NegativePalette.BackgroundLight, sheet.NegativePalette.Base.WithAlpha(0.6f), 1, 8, 3);
        var itemListBackground = StrictBox(sheet.SecondaryPalette.BackgroundDark, buttonBorder, 1, 0, 0);
        var itemListItem = StrictBox(sheet.SecondaryPalette.Background, buttonBorder, 1, 4, 4);
        var itemListSelected = StrictBox(sheet.PrimaryPalette.Element, ArcanePalette.NeonOutline, 1, 4, 4);
        var itemListDisabled = StrictBox(sheet.SecondaryPalette.DisabledElement, buttonBorder.WithAlpha(0.25f), 1, 4, 4);

        var rules = new List<StyleRule>
        {
            E<LineEdit>()
                .Prop(LineEdit.StylePropertyStyleBox, lineEdit)
                .Prop("font-color", sheet.SecondaryPalette.Text)
                .Prop(LineEdit.StylePropertyCursorColor, sheet.HighlightPalette.Text)
                .Prop(LineEdit.StylePropertySelectionColor, sheet.HighlightPalette.Element.WithAlpha(0.72f)),
            E<LineEdit>()
                .Class(LineEdit.StyleClassLineEditNotEditable)
                .Prop("font-color", sheet.SecondaryPalette.TextDark),
            E<LineEdit>()
                .Pseudo(LineEdit.StylePseudoClassPlaceholder)
                .Prop("font-color", sheet.SecondaryPalette.TextDark),
            E<TextEdit>()
                .Prop("font-color", sheet.SecondaryPalette.Text)
                .Prop(TextEdit.StylePropertyCursorColor, sheet.HighlightPalette.Text)
                .Prop(TextEdit.StylePropertySelectionColor, sheet.HighlightPalette.Element.WithAlpha(0.72f)),
            E<TextEdit>()
                .Pseudo(TextEdit.StylePseudoClassPlaceholder)
                .Prop("font-color", sheet.SecondaryPalette.TextDark),
            E<Slider>()
                .Prop(Slider.StylePropertyBackground, sliderBackground)
                .Prop(Slider.StylePropertyFill, sliderFill)
                .Prop(Slider.StylePropertyForeground, sliderOutline)
                .Prop(Slider.StylePropertyGrabber, sliderGrabber),
            E<PanelContainer>().Class(StyleClass.PanelLight).Panel(panelLight),
            E<PanelContainer>().Class(StyleClass.PanelDark).Panel(panelDark),
            E<PanelContainer>().Class("BackgroundDark").Panel(panelDarker),
            E().Class(StyleClass.BackgroundPanel).Panel(modulatedPanel),
            E().Class(StyleClass.BackgroundPanelOpenLeft).Panel(modulatedPanelOpenLeft),
            E().Class(StyleClass.BackgroundPanelOpenRight).Panel(modulatedPanelOpenRight),
            E<PanelContainer>().Class(OptionButton.StyleClassOptionsBackground).Panel(panelDarker),
            E<ItemList>()
                .Prop(ItemList.StylePropertyBackground, itemListBackground)
                .Prop(ItemList.StylePropertyItemBackground, itemListItem)
                .Prop(ItemList.StylePropertySelectedItemBackground, itemListSelected)
                .Prop(ItemList.StylePropertyDisabledItemBackground, itemListDisabled),
            E<TextureRect>()
                .Class(CheckBox.StyleClassCheckBox)
                .Modulate(sheet.SecondaryPalette.TextDark),
            E<TextureRect>()
                .Class(CheckBox.StyleClassCheckBox)
                .Class(CheckBox.StyleClassCheckBoxChecked)
                .Modulate(sheet.HighlightPalette.Text),
            E<TextureRect>()
                .Class(OptionButton.StyleClassOptionTriangle)
                .Modulate(sheet.HighlightPalette.TextDark),

            E().Class(DefaultWindow.StyleClassWindowPanel).Panel(windowPanel),
            E().Class(StyleClass.BorderedWindowPanel).Panel(windowPanel),
            E().Class(DefaultWindow.StyleClassWindowHeader).Panel(windowHeader),
            E().Class(StyleClass.AlertWindowHeader).Panel(alertHeader),
            E<PanelContainer>().Class(ContextMenuPopup.StyleClassContextMenuPopup).Panel(contextMenuPanel),
            E<NanoHeading>().ParentOf(E<PanelContainer>()).Panel(windowHeader),
            E<Label>()
                .Class(DefaultWindow.StyleClassWindowTitle)
                .Font(sheet.BaseFont.GetFont(14, FontKind.Bold))
                .FontColor(sheet.HighlightPalette.Text),
            E<Label>()
                .Class("FancyWindowTitle")
                .Font(sheet.BaseFont.GetFont(14, FontKind.Bold))
                .FontColor(sheet.HighlightPalette.Text),
            E<PanelContainer>().Class(StyleClass.TooltipPanel).Panel(tooltip).Modulate(Color.White),
            E<Tooltip>().Prop(Tooltip.StylePropertyPanel, tooltip),
            E<TextureButton>()
                .Class(DefaultWindow.StyleClassWindowCloseButton)
                .PseudoNormal()
                .Modulate(sheet.SecondaryPalette.TextDark),
            E<TextureButton>()
                .Class(DefaultWindow.StyleClassWindowCloseButton)
                .PseudoHovered()
                .Modulate(sheet.NegativePalette.Text),
            E<TextureButton>()
                .Class(DefaultWindow.StyleClassWindowCloseButton)
                .PseudoPressed()
                .Modulate(sheet.NegativePalette.PressedElement),
            E<TextureButton>()
                .Class(DefaultWindow.StyleClassWindowCloseButton)
                .PseudoDisabled()
                .Modulate(sheet.SecondaryPalette.DisabledElement),

            E<TabContainer>()
                .Prop(TabContainer.StylePropertyPanelStyleBox, panelDark)
                .Prop(TabContainer.StylePropertyTabStyleBox, tabActive)
                .Prop(TabContainer.StylePropertyTabStyleBoxInactive, tabInactive)
                .Prop(TabContainer.stylePropertyTabFontColor, sheet.SecondaryPalette.Text)
                .Prop(TabContainer.StylePropertyTabFontColorInactive, sheet.SecondaryPalette.TextDark),
            E<ProgressBar>()
                .Prop(ProgressBar.StylePropertyBackground, progressBackground)
                .Prop(ProgressBar.StylePropertyForeground, progressForeground),
            E<VScrollBar>().Prop(ScrollBar.StylePropertyGrabber, scrollbar),
            E<VScrollBar>().PseudoHovered().Prop(ScrollBar.StylePropertyGrabber, scrollbarHovered),
            E<VScrollBar>().PseudoPressed().Prop(ScrollBar.StylePropertyGrabber, scrollbarPressed),
            E<HScrollBar>().Prop(ScrollBar.StylePropertyGrabber, scrollbar),
            E<HScrollBar>().PseudoHovered().Prop(ScrollBar.StylePropertyGrabber, scrollbarHovered),
            E<HScrollBar>().PseudoPressed().Prop(ScrollBar.StylePropertyGrabber, scrollbarPressed),

            E<Label>().Class(StyleClass.LabelSubText).FontColor(sheet.SecondaryPalette.TextDark),
            E<Label>().Class(StyleClass.LabelWeak).FontColor(sheet.SecondaryPalette.TextDark),
            E<Label>().Class(StyleClass.LabelHeading).FontColor(sheet.HighlightPalette.Text),
            E<Label>().Class(StyleClass.LabelHeadingBigger).FontColor(sheet.HighlightPalette.Text),
            E<Label>().Class(StyleClass.LabelKeyText).FontColor(sheet.HighlightPalette.Text),
        };

        rules.AddRange(TexturedButtonStateRules(buttonTexture, buttonBorderTexture, Button,
            ArcanePalette.Buttons, ArcanePalette.NeonOutline));
        rules.AddRange(TexturedButtonStateRules(
            buttonTexture,
            buttonBorderTexture,
            () => E<MenuButton>(),
            ArcanePalette.Buttons,
            ArcanePalette.NeonOutline,
            8,
            4));
        rules.AddRange(TexturedButtonStateRules(buttonTexture, buttonBorderTexture,
            () => Button().Class(StyleClass.Positive), sheet.PositivePalette, sheet.PositivePalette.Base));
        rules.AddRange(TexturedButtonStateRules(buttonTexture, buttonBorderTexture,
            () => Button().Class(StyleClass.Negative), sheet.NegativePalette, sheet.NegativePalette.Base));
        rules.AddRange(TexturedButtonStateRules(buttonTexture, buttonBorderTexture,
            () => Button().Class(StyleClass.ButtonSmall), ArcanePalette.Buttons, ArcanePalette.NeonOutline, 8, 2));

        AddComposedButtonRules(rules, buttonTexture, buttonBorderTexture, Button, ArcanePalette.Buttons,
            ArcanePalette.NeonOutline);
        AddComposedButtonRules(rules, buttonTexture, buttonBorderTexture,
            () => Button().Class(StyleClass.Positive), sheet.PositivePalette, sheet.PositivePalette.Base);
        AddComposedButtonRules(rules, buttonTexture, buttonBorderTexture,
            () => Button().Class(StyleClass.Negative), sheet.NegativePalette, sheet.NegativePalette.Base);
        AddComposedButtonRules(rules, buttonTexture, buttonBorderTexture, () => E<MenuButton>(),
            ArcanePalette.Buttons, ArcanePalette.NeonOutline, 8, 4);

        foreach (var (styleClass, color) in DepartmentButtonColors)
            rules.AddRange(DepartmentButtonStateRules(buttonTexture, buttonBorderTexture, styleClass, color));

        rules.AddRange(TexturedButtonStateRules(
            buttonTexture,
            buttonBorderTexture,
            () => E<ContextMenuElement>().Class(ContextMenuElement.StyleClassContextMenuButton),
            contextMenuPalette,
            contextMenuPalette.Base,
            0,
            0,
            ButtonGeometry.Borderless));
        rules.AddRange(TexturedButtonStateRules(
            buttonTexture,
            buttonBorderTexture,
            () => E<ContextMenuElement>().Class(ConfirmationMenuElement.StyleClassConfirmationContextMenuButton),
            sheet.NegativePalette,
            sheet.NegativePalette.Base,
            0,
            0,
            ButtonGeometry.Borderless));

        var confirmNormal = TexturedButtonBox(buttonTexture, sheet.NegativePalette.Element);
        var confirmHovered = TexturedButtonBox(buttonBorderTexture, sheet.NegativePalette.Base);
        var confirmPressed = TexturedButtonBox(buttonBorderTexture, sheet.NegativePalette.Base.WithAlpha(0.82f));
        var confirmDisabled = TexturedButtonBox(buttonTexture, sheet.NegativePalette.DisabledElement);
        rules.AddRange(
        [
            E<ConfirmButton>().Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassNormal)
                .Box(confirmNormal).Modulate(Color.White),
            E<ConfirmButton>().Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassHover)
                .Box(confirmHovered).Modulate(Color.White),
            E<ConfirmButton>().Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassPressed)
                .Box(confirmPressed).Modulate(Color.White),
            E<ConfirmButton>().Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassDisabled)
                .Box(confirmDisabled).Modulate(Color.White),
        ]);

        return rules.ToArray();
    }

    private static MutableSelectorElement Button()
    {
        return E<ContainerButton>().Class(ContainerButton.StyleClassButton);
    }

    private static StyleRule[] TexturedButtonStateRules(
        Texture texture,
        Texture borderTexture,
        Func<MutableSelectorElement> selector,
        ColorPalette palette,
        Color outlineColor,
        float horizontalContentMargin = 14,
        float verticalContentMargin = 4,
        ButtonGeometry geometry = ButtonGeometry.Standard)
    {
        var normal = TexturedButtonBox(texture, palette.Element, geometry,
            horizontalContentMargin, verticalContentMargin);
        StyleBox hovered = geometry is ButtonGeometry.OpenLeft or ButtonGeometry.OpenRight
            ? StrictBox(palette.Element, outlineColor, 1, horizontalContentMargin, verticalContentMargin)
            : TexturedButtonBox(
                borderTexture,
                outlineColor,
                geometry,
                horizontalContentMargin,
                verticalContentMargin);
        StyleBox pressed = geometry is ButtonGeometry.OpenLeft or ButtonGeometry.OpenRight
            ? StrictBox(palette.PressedElement, outlineColor, 1, horizontalContentMargin, verticalContentMargin)
            : TexturedButtonBox(
                borderTexture,
                outlineColor.WithAlpha(0.82f),
                geometry,
                horizontalContentMargin,
                verticalContentMargin);
        var disabled = TexturedButtonBox(texture, palette.DisabledElement, geometry,
            horizontalContentMargin, verticalContentMargin);

        return
        [
            selector().PseudoNormal().Box(normal).Modulate(Color.White),
            selector().PseudoHovered().Box(hovered).Modulate(Color.White),
            selector().PseudoPressed().Box(pressed).Modulate(Color.White),
            selector().PseudoDisabled().Box(disabled).Modulate(Color.White),
        ];
    }

    private static void AddComposedButtonRules(
        List<StyleRule> rules,
        Texture texture,
        Texture borderTexture,
        Func<MutableSelectorElement> selector,
        ColorPalette palette,
        Color outlineColor,
        float horizontalContentMargin = 8,
        float verticalContentMargin = 4)
    {
        rules.AddRange(TexturedButtonStateRules(
            texture,
            borderTexture,
            () => selector().Class(StyleClass.ButtonOpenLeft),
            palette,
            outlineColor,
            horizontalContentMargin,
            verticalContentMargin,
            ButtonGeometry.OpenLeft));
        rules.AddRange(TexturedButtonStateRules(
            texture,
            borderTexture,
            () => selector().Class(StyleClass.ButtonOpenRight),
            palette,
            outlineColor,
            horizontalContentMargin,
            verticalContentMargin,
            ButtonGeometry.OpenRight));
        rules.AddRange(TexturedButtonStateRules(
            texture,
            borderTexture,
            () => selector().Class(StyleClass.ButtonOpenBoth),
            palette,
            outlineColor,
            horizontalContentMargin,
            verticalContentMargin,
            ButtonGeometry.OpenBoth));
        rules.AddRange(TexturedButtonStateRules(
            texture,
            borderTexture,
            () => selector().Class(StyleClass.ButtonSquare),
            palette,
            outlineColor,
            horizontalContentMargin,
            verticalContentMargin,
            ButtonGeometry.OpenBoth));
    }

    private static StyleRule[] DepartmentButtonStateRules(
        Texture texture,
        Texture borderTexture,
        string styleClass,
        Color color)
    {
        var normal = TexturedButtonBox(texture, color.WithAlpha(0.52f));
        var hovered = TexturedButtonBox(borderTexture, color);
        var pressed = TexturedButtonBox(borderTexture, color.WithAlpha(0.82f));
        var disabled = TexturedButtonBox(texture, color.WithAlpha(0.24f));

        return
        [
            Button().Class(styleClass).PseudoNormal().Box(normal).Modulate(Color.White),
            Button().Class(styleClass).PseudoHovered().Box(hovered).Modulate(Color.White),
            Button().Class(styleClass).PseudoPressed().Box(pressed).Modulate(Color.White),
            Button().Class(styleClass).PseudoDisabled().Box(disabled).Modulate(Color.White),
        ];
    }

    private static StyleBoxFlat StrictBox(
        Color background,
        Color border,
        float borderThickness = 1,
        float horizontalContentMargin = 14,
        float verticalContentMargin = 4)
    {
        return StrictBox(background, border, new Thickness(borderThickness), horizontalContentMargin,
            verticalContentMargin);
    }

    private static StyleBoxFlat StrictBox(
        Color background,
        Color border,
        Thickness borderThickness,
        float horizontalContentMargin,
        float verticalContentMargin)
    {
        var box = new StyleBoxFlat(background)
        {
            BorderColor = border,
            BorderThickness = borderThickness,
        };

        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, horizontalContentMargin);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, verticalContentMargin);
        return box;
    }

    private static StyleBoxTexture TexturedButtonBox(
        Texture texture,
        Color color,
        ButtonGeometry geometry = ButtonGeometry.Standard,
        float horizontalContentMargin = 14,
        float verticalContentMargin = 4)
    {
        var box = new StyleBoxTexture
        {
            Texture = texture,
            Modulate = color,
        };

        box.SetPatchMargin(StyleBox.Margin.All, 10);

        switch (geometry)
        {
            case ButtonGeometry.OpenLeft:
                box.Texture = new AtlasTexture(texture,
                    UIBox2.FromDimensions(new Vector2(10, 0), new Vector2(14, 24)));
                box.SetPatchMargin(StyleBox.Margin.Left, 0);
                break;
            case ButtonGeometry.OpenRight:
                box.Texture = new AtlasTexture(texture,
                    UIBox2.FromDimensions(Vector2.Zero, new Vector2(14, 24)));
                box.SetPatchMargin(StyleBox.Margin.Right, 0);
                break;
            case ButtonGeometry.OpenBoth:
                box.Texture = new AtlasTexture(texture,
                    UIBox2.FromDimensions(new Vector2(10, 0), new Vector2(3, 24)));
                box.SetPatchMargin(StyleBox.Margin.Horizontal, 0);
                break;
            case ButtonGeometry.Borderless:
                box.Texture = Texture.White;
                box.SetPatchMargin(StyleBox.Margin.All, 0);
                break;
        }

        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, horizontalContentMargin);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, verticalContentMargin);
        return box;
    }

    private enum ButtonGeometry : byte
    {
        Standard,
        OpenLeft,
        OpenRight,
        OpenBoth,
        Borderless,
    }

}
