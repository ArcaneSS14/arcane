using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Arcane.StyleSheets.Sheetlets;

public sealed class ArcaneDetailExaminableSheetlet : Sheetlet<ArcaneStylesheet>
{
    public override StyleRule[] GetRules(ArcaneStylesheet sheet, object config)
    {
        var softBorder = sheet.PrimaryPalette.Base.WithAlpha(0.48f);
        var strongBorder = sheet.PrimaryPalette.Base.WithAlpha(0.68f);
        var surface = Panel(sheet.PrimaryPalette.BackgroundLight, softBorder, new Thickness(1));
        var sidebar = Panel(sheet.SecondaryPalette.BackgroundLight, softBorder, new Thickness(0, 0, 1, 0));
        var content = Panel(sheet.SecondaryPalette.Element, contentMargin: 4);
        var preview = Panel(sheet.SecondaryPalette.HoveredElement, strongBorder, new Thickness(1), 4);
        var stripe = Panel(sheet.PrimaryPalette.Element, strongBorder, new Thickness(0, 0, 0, 1));

        return
        [
            E<PanelContainer>().Class(ArcaneStyleClass.DetailSurface).Panel(surface),
            E<PanelContainer>().Class(ArcaneStyleClass.DetailSidebar).Panel(sidebar),
            E<PanelContainer>().Class(ArcaneStyleClass.DetailContent).Panel(content),
            E<PanelContainer>().Class(ArcaneStyleClass.DetailPreview).Panel(preview),
            E<StripeBack>()
                .Class(ArcaneStyleClass.DetailStripe)
                .Prop(StripeBack.StylePropertyBackground, stripe),
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
