using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Goobstation.UIKit.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Arcane.StyleSheets.Sheetlets;

public sealed class ArcaneChatSheetlet : Sheetlet<ArcaneStylesheet>
{
    public override StyleRule[] GetRules(ArcaneStylesheet sheet, object config)
    {
        var shell = Panel(sheet.SecondaryPalette.BackgroundLight.WithAlpha(0.96f),
            sheet.PrimaryPalette.Base.WithAlpha(0.42f));
        var separatedShell = Panel(sheet.SecondaryPalette.BackgroundLight,
            sheet.PrimaryPalette.Base.WithAlpha(0.5f));
        var output = Panel(sheet.SecondaryPalette.Element.WithAlpha(0.96f),
            sheet.PrimaryPalette.Base.WithAlpha(0.38f), 8);
        var input = Panel(sheet.SecondaryPalette.HoveredElement.WithAlpha(0.98f),
            sheet.PrimaryPalette.Base.WithAlpha(0.5f), 2);
        var auxiliary = Panel(sheet.SecondaryPalette.Element,
            sheet.PrimaryPalette.Base.WithAlpha(0.38f));
        var lobbyShell = Panel(sheet.SecondaryPalette.BackgroundLight.WithAlpha(0.35f),
            sheet.PrimaryPalette.Base.WithAlpha(0.42f));
        var lobbyOutput = Panel(sheet.SecondaryPalette.Element.WithAlpha(0.35f),
            sheet.PrimaryPalette.Base.WithAlpha(0.38f), 8);

        return
        [
            E<PanelContainer>().Class(ArcaneStyleClass.ChatSurface).Panel(shell).Modulate(Color.White),
            E<PanelContainer>().Class(ArcaneStyleClass.SeparatedChatSurface).Panel(separatedShell).Modulate(Color.White),
            E<PanelContainer>().Class(ArcaneStyleClass.ChatAuxiliary).Panel(auxiliary).Modulate(Color.White),
            E<CustomOutputPanel>()
                .Class(ArcaneStyleClass.ChatOutput)
                .Prop(CustomOutputPanel.StylePropertyStyleBox, output),
            E<PanelContainer>()
                .Class(ArcaneStyleClass.ChatSurface)
                .Class(ArcaneStyleClass.LobbyChatSurface)
                .Panel(lobbyShell)
                .Modulate(Color.White),
            E<CustomOutputPanel>()
                .Class(ArcaneStyleClass.ChatOutput)
                .Class(ArcaneStyleClass.LobbyChatOutput)
                .Prop(CustomOutputPanel.StylePropertyStyleBox, lobbyOutput),
            E<ChatInputBox>().Class(ChatInputBox.StyleClassChatPanel).Panel(input),
        ];
    }

    private static StyleBoxFlat Panel(Color background, Color border, float contentMargin = 0)
    {
        var box = new StyleBoxFlat(background)
        {
            BorderColor = border,
            BorderThickness = new Thickness(1),
        };

        if (contentMargin > 0)
            box.SetContentMarginOverride(StyleBox.Margin.All, contentMargin);

        return box;
    }
}
