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
    public override StyleRule[] GetRules(ArcaneStylesheet sheet, object config)
    {
        var buttonBorder = sheet.PrimaryPalette.Base.WithAlpha(0.42f);
        var button = StrictBox(ArcanePalette.Buttons.Element, buttonBorder);
        var buttonSmall = StrictBox(ArcanePalette.Buttons.Element, buttonBorder, 1, 8, 2);
        var modulatedPanel = StrictBox(Color.White, buttonBorder, 1, 0, 0);
        var panelLight = StrictBox(sheet.SecondaryPalette.BackgroundLight, buttonBorder, 1, 0, 0);
        var panelDark = StrictBox(sheet.SecondaryPalette.Background, buttonBorder, 1, 0, 0);
        var panelDarker = StrictBox(sheet.SecondaryPalette.BackgroundDark, buttonBorder, 1, 0, 0);
        var lineEdit = StrictBox(sheet.SecondaryPalette.Element, buttonBorder, 1, 7, 3);
        var sliderBackground = StrictBox(sheet.SecondaryPalette.BackgroundDark, buttonBorder, 1, 0, 6);
        var sliderFill = StrictBox(sheet.HighlightPalette.Element, sheet.HighlightPalette.Base.WithAlpha(0.65f), 1, 0, 6);
        var sliderOutline = StrictBox(Color.Transparent, sheet.PrimaryPalette.TextDark.WithAlpha(0.68f), 1, 0, 6);
        var sliderGrabber = StrictBox(sheet.HighlightPalette.TextDark, ArcanePalette.NeonOutline, 1, 4, 8);
        var tabActive = StrictBox(sheet.PrimaryPalette.Element, sheet.PrimaryPalette.Base.WithAlpha(0.7f), 1, 7, 3);
        var tabInactive = StrictBox(sheet.SecondaryPalette.Element, buttonBorder, 1, 7, 3);
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
        var contextMenuPanel = StrictBox(sheet.SecondaryPalette.Background.WithAlpha(0.98f), buttonBorder, 1, 0, 0);
        var windowHeader = StrictBox(sheet.PrimaryPalette.BackgroundLight, buttonBorder, 1, 8, 3);
        var alertHeader = StrictBox(sheet.NegativePalette.BackgroundLight, sheet.NegativePalette.Base.WithAlpha(0.6f), 1, 8, 3);
        var itemListBackground = StrictBox(sheet.SecondaryPalette.BackgroundDark, buttonBorder, 1, 0, 0);
        var itemListItem = StrictBox(sheet.SecondaryPalette.Background, buttonBorder, 1, 5, 3);
        var itemListSelected = StrictBox(sheet.PrimaryPalette.Element, ArcanePalette.NeonOutline, 1, 5, 3);
        var itemListDisabled = StrictBox(sheet.SecondaryPalette.DisabledElement, buttonBorder.WithAlpha(0.25f), 1, 5, 3);

        var rules = new List<StyleRule>
        {
            Button().Box(button),
            Button().Class(StyleClass.ButtonOpenLeft).Box(button),
            Button().Class(StyleClass.ButtonOpenRight).Box(button),
            Button().Class(StyleClass.ButtonOpenBoth).Box(button),
            Button().Class(StyleClass.ButtonSquare).Box(button),
            Button().Class(StyleClass.ButtonSmall).Box(buttonSmall),
            E<MenuButton>().Box(button),
            E<MenuButton>().Class(StyleClass.ButtonOpenLeft).Box(button),
            E<MenuButton>().Class(StyleClass.ButtonOpenRight).Box(button),
            E<MenuButton>().Class(StyleClass.ButtonOpenBoth).Box(button),
            E<MenuButton>().Class(StyleClass.ButtonSquare).Box(button),

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
            E().Class(StyleClass.BackgroundPanelOpenLeft).Panel(modulatedPanel),
            E().Class(StyleClass.BackgroundPanelOpenRight).Panel(modulatedPanel),
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

        rules.AddRange(StrictButtonStateRules(Button, ArcanePalette.Buttons, ArcanePalette.NeonOutline));
        rules.AddRange(StrictButtonStateRules(() => E<MenuButton>(), ArcanePalette.Buttons, ArcanePalette.NeonOutline));
        rules.AddRange(StrictButtonStateRules(() => Button().Class(StyleClass.Positive),
            sheet.PositivePalette, ArcanePalette.NeonOutline));
        rules.AddRange(StrictButtonStateRules(() => Button().Class(StyleClass.Negative),
            sheet.NegativePalette, ArcanePalette.NeonOutline));
        rules.AddRange(StrictButtonStateRules(() => Button().Class(StyleClass.ButtonSmall),
            ArcanePalette.Buttons, ArcanePalette.NeonOutline, 8, 2));
        rules.AddRange(StrictButtonStateRules(
            () => E<ContextMenuElement>().Class(ContextMenuElement.StyleClassContextMenuButton),
            ArcanePalette.Buttons,
            ArcanePalette.NeonOutline,
            0,
            0));
        rules.AddRange(StrictButtonStateRules(
            () => E<ContextMenuElement>().Class(ConfirmationMenuElement.StyleClassConfirmationContextMenuButton),
            sheet.NegativePalette,
            ArcanePalette.NeonOutline,
            0,
            0));

        var confirmNormal = StrictBox(sheet.NegativePalette.Element,
            sheet.NegativePalette.Base.WithAlpha(0.55f));
        var confirmHovered = StrictBox(sheet.NegativePalette.Element, ArcanePalette.NeonOutline, 2);
        var confirmPressed = StrictBox(sheet.NegativePalette.PressedElement, ArcanePalette.NeonOutline, 2);
        var confirmDisabled = StrictBox(sheet.NegativePalette.DisabledElement,
            sheet.NegativePalette.Base.WithAlpha(0.25f));
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

    private static StyleRule[] StrictButtonStateRules(
        Func<MutableSelectorElement> selector,
        ColorPalette palette,
        Color neonBorder,
        float horizontalContentMargin = 14,
        float verticalContentMargin = 4)
    {
        var normalBorder = palette.Base.WithAlpha(0.48f);
        var normal = StrictBox(palette.Element, normalBorder, 1, horizontalContentMargin, verticalContentMargin);
        var hovered = StrictBox(palette.Element, neonBorder, 2, horizontalContentMargin, verticalContentMargin);
        var pressed = StrictBox(palette.PressedElement, neonBorder, 2, horizontalContentMargin, verticalContentMargin);
        var disabled = StrictBox(palette.DisabledElement, normalBorder.WithAlpha(0.3f), 1,
            horizontalContentMargin, verticalContentMargin);

        return
        [
            selector().PseudoNormal().Box(normal).Modulate(Color.White),
            selector().PseudoHovered().Box(hovered).Modulate(Color.White),
            selector().PseudoPressed().Box(pressed).Modulate(Color.White),
            selector().PseudoDisabled().Box(disabled).Modulate(Color.White),
        ];
    }

    private static StyleBoxFlat StrictBox(
        Color background,
        Color border,
        float borderThickness = 1,
        float horizontalContentMargin = 14,
        float verticalContentMargin = 4)
    {
        var box = new StyleBoxFlat(background)
        {
            BorderColor = border,
            BorderThickness = new Thickness(borderThickness),
        };

        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, horizontalContentMargin);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, verticalContentMargin);
        return box;
    }

}
