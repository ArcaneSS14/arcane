using Content.Client.PDA;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Arcane.StyleSheets.Sheetlets;

public sealed class ArcanePdaSheetlet : Sheetlet<ArcaneStylesheet>
{
    public override StyleRule[] GetRules(ArcaneStylesheet sheet, object config)
    {
        var content = new StyleBoxFlat(sheet.SecondaryPalette.BackgroundDark);
        var normalBorder = sheet.PrimaryPalette.Base.WithAlpha(0.72f);

        return
        [
            E<PanelContainer>().Class("PdaContentBackground").Panel(content).Modulate(Color.White),

            E<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(PdaSettingsButton.StylePropertyBgColor, sheet.ButtonPalette.Element)
                .Prop(PdaSettingsButton.StylePropertyFgColor, sheet.SecondaryPalette.Text)
                .Prop(PdaSettingsButton.StylePropertyBorderColor, normalBorder)
                .Prop(PdaSettingsButton.StylePropertyBorderThickness, 1f),
            E<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(PdaSettingsButton.StylePropertyBgColor, sheet.ButtonPalette.HoveredElement)
                .Prop(PdaSettingsButton.StylePropertyFgColor, sheet.SecondaryPalette.Text)
                .Prop(PdaSettingsButton.StylePropertyBorderColor, sheet.AccentColor)
                .Prop(PdaSettingsButton.StylePropertyBorderThickness, 1f),
            E<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(PdaSettingsButton.StylePropertyBgColor, sheet.ButtonPalette.PressedElement)
                .Prop(PdaSettingsButton.StylePropertyFgColor, sheet.SecondaryPalette.Text)
                .Prop(PdaSettingsButton.StylePropertyBorderColor, sheet.AccentColor)
                .Prop(PdaSettingsButton.StylePropertyBorderThickness, 1f),
            E<PdaSettingsButton>()
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(PdaSettingsButton.StylePropertyBgColor, sheet.ButtonPalette.DisabledElement)
                .Prop(PdaSettingsButton.StylePropertyFgColor, sheet.SecondaryPalette.TextDark)
                .Prop(PdaSettingsButton.StylePropertyBorderColor, normalBorder.WithAlpha(0.3f))
                .Prop(PdaSettingsButton.StylePropertyBorderThickness, 1f),

            E<PdaProgramItem>()
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(PdaProgramItem.StylePropertyBgColor, sheet.ButtonPalette.Element)
                .Prop(PdaProgramItem.StylePropertyBorderColor, normalBorder)
                .Prop(PdaProgramItem.StylePropertyBorderThickness, 1f),
            E<PdaProgramItem>()
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(PdaProgramItem.StylePropertyBgColor, sheet.ButtonPalette.HoveredElement)
                .Prop(PdaProgramItem.StylePropertyBorderColor, sheet.AccentColor)
                .Prop(PdaProgramItem.StylePropertyBorderThickness, 1f),
            E<PdaProgramItem>()
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(PdaProgramItem.StylePropertyBgColor, sheet.ButtonPalette.PressedElement)
                .Prop(PdaProgramItem.StylePropertyBorderColor, sheet.AccentColor)
                .Prop(PdaProgramItem.StylePropertyBorderThickness, 1f),
            E<PdaProgramItem>()
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(PdaProgramItem.StylePropertyBgColor, sheet.ButtonPalette.DisabledElement)
                .Prop(PdaProgramItem.StylePropertyBorderColor, normalBorder.WithAlpha(0.3f))
                .Prop(PdaProgramItem.StylePropertyBorderThickness, 1f),

            E<Label>()
                .Class("PdaContentFooterText")
                .FontColor(sheet.SecondaryPalette.TextDark),
            E<Label>()
                .Class("PdaWindowFooterText")
                .FontColor(sheet.PrimaryPalette.TextDark),
        ];
    }
}
