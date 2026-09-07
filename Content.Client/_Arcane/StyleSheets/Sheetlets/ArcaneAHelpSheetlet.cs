using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Arcane.StyleSheets.Sheetlets;

public sealed class ArcaneAHelpSheetlet : Sheetlet<ArcaneStylesheet>
{
    public override StyleRule[] GetRules(ArcaneStylesheet sheet, object config)
    {
        var border = sheet.PrimaryPalette.Base.WithAlpha(0.62f);
        var surface = Panel(sheet.SecondaryPalette.BackgroundLight, border, new Thickness(1));
        var output = Panel(sheet.SecondaryPalette.Element, contentMargin: 8);
        var toolbar = Panel(sheet.SecondaryPalette.Background, border, new Thickness(0, 1, 0, 0), 4);
        var listSurface = Panel(sheet.SecondaryPalette.BackgroundDark, contentMargin: 4);

        return
        [
            E<PanelContainer>().Class(ArcaneStyleClass.AHelpSurface).Panel(surface),
            E<OutputPanel>()
                .Class(ArcaneStyleClass.AHelpOutput)
                .Prop(OutputPanel.StylePropertyStyleBox, output),
            E<PanelContainer>().Class(ArcaneStyleClass.AHelpToolbar).Panel(toolbar),
            E<PanelContainer>().Class(ArcaneStyleClass.AdminListSurface).Panel(listSurface),
        ];
    }

    private static StyleBoxFlat Panel(
        Color background,
        Color? border = null,
        Thickness? borderThickness = null,
        float contentMargin = 0)
    {
        var box = new StyleBoxFlat(background);
        if (border is not null)
        {
            box.BorderColor = border.Value;
            box.BorderThickness = borderThickness ?? new Thickness(1);
        }

        if (contentMargin > 0)
            box.SetContentMarginOverride(StyleBox.Margin.All, contentMargin);

        return box;
    }
}
