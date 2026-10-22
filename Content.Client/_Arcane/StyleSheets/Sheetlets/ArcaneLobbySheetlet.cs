using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Arcane.StyleSheets.Sheetlets;

public sealed class ArcaneLobbySheetlet : Sheetlet<ArcaneStylesheet>
{
    public override StyleRule[] GetRules(ArcaneStylesheet sheet, object config)
    {
        var canvas = Panel(sheet.SecondaryPalette.Background);
        var lobbySurface = Panel(sheet.SecondaryPalette.BackgroundLight.WithAlpha(0.76f),
            sheet.PrimaryPalette.Base.WithAlpha(0.62f));
        var lobbyInset = Panel(sheet.SecondaryPalette.Element.WithAlpha(0.86f));
        var elevated = Panel(sheet.SecondaryPalette.HoveredElement,
            sheet.PrimaryPalette.Base.WithAlpha(0.62f));

        return
        [
            E<PanelContainer>().Class(ArcaneStyleClass.CharacterSetupBackground).Panel(canvas),
            E<PanelContainer>().Class(ArcaneStyleClass.DepartmentHeader)
                .Panel(Panel(sheet.PrimaryPalette.Element)),
            E<HLine>().Class(ArcaneStyleClass.LobbyDivider)
                .Panel(Panel(sheet.PrimaryPalette.Base.WithAlpha(0.75f))),
            E<HLine>().Class(ArcaneStyleClass.LobbyDividerMuted)
                .Panel(Panel(sheet.SecondaryPalette.Base.WithAlpha(0.45f))),
            E<PanelContainer>().Class(ArcaneStyleClass.LobbySurface).Panel(lobbySurface).Modulate(Color.White),
            E<PanelContainer>().Class(ArcaneStyleClass.LobbyInset).Panel(lobbyInset).Modulate(Color.White),
            E<PanelContainer>().Class(ArcaneStyleClass.ElevatedPanel).Panel(elevated),
        ];
    }

    private static StyleBoxFlat Panel(Color background, Color? border = null, float contentMargin = 0)
    {
        var box = new StyleBoxFlat(background);
        if (border is not null)
        {
            box.BorderColor = border.Value;
            box.BorderThickness = new Thickness(1);
        }

        if (contentMargin > 0)
            box.SetContentMarginOverride(StyleBox.Margin.All, contentMargin);

        return box;
    }
}
