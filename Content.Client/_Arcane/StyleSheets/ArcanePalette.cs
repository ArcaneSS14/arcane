using Content.Client.Stylesheets.Palette;
using Content.Shared._Arcane.DiscordRoles;

namespace Content.Client._Arcane.StyleSheets;

public static class ArcanePalette
{
    public const string DefaultThemeId = "default";
    public const string GoldThemeId = "gold";
    public const string CosmosThemeId = "cosmos";

    public static readonly Color NeonOutline = Color.FromHex("#66D9EF");

    public static readonly ColorPalette Primary = new(
        Base: Color.FromHex("#5E8797"),
        LightnessShift: 0f,
        ChromaShift: 0f,
        Element: Color.FromHex("#294651"),
        HoveredElement: Color.FromHex("#345966"),
        PressedElement: Color.FromHex("#203D48"),
        DisabledElement: Color.FromHex("#202B30"),
        Background: Color.FromHex("#121A1F"),
        BackgroundLight: Color.FromHex("#1A252B"),
        BackgroundDark: Color.FromHex("#0B1115"),
        Text: Color.FromHex("#C5DCE4"),
        TextDark: Color.FromHex("#88A3AE"));

    public static readonly ColorPalette Secondary = new(
        Base: Color.FromHex("#6B7780"),
        LightnessShift: 0f,
        ChromaShift: 0f,
        Element: Color.FromHex("#263239"),
        HoveredElement: Color.FromHex("#303F47"),
        PressedElement: Color.FromHex("#1F2A30"),
        DisabledElement: Color.FromHex("#1B2226"),
        Background: Color.FromHex("#151C21"),
        BackgroundLight: Color.FromHex("#1D272D"),
        BackgroundDark: Color.FromHex("#0D1317"),
        Text: Color.FromHex("#DCE5E9"),
        TextDark: Color.FromHex("#96A7AF"));

    public static readonly ColorPalette Buttons = new(
        Base: Color.FromHex("#718993"),
        LightnessShift: 0f,
        ChromaShift: 0f,
        Element: Color.FromHex("#29363D"),
        HoveredElement: Color.FromHex("#34464F"),
        PressedElement: Color.FromHex("#21424D"),
        DisabledElement: Color.FromHex("#1C252A"),
        Background: Color.FromHex("#151C21"),
        BackgroundLight: Color.FromHex("#1D272D"),
        BackgroundDark: Color.FromHex("#0D1317"),
        Text: Color.FromHex("#DCE5E9"),
        TextDark: Color.FromHex("#96A7AF"));

    public static readonly ColorPalette Positive = new(
        Base: Color.FromHex("#5FB98A"),
        LightnessShift: 0f,
        ChromaShift: 0f,
        Element: Color.FromHex("#285941"),
        HoveredElement: Color.FromHex("#326C4F"),
        PressedElement: Color.FromHex("#204936"),
        DisabledElement: Color.FromHex("#1D2D26"),
        Background: Color.FromHex("#12251D"),
        BackgroundLight: Color.FromHex("#193329"),
        BackgroundDark: Color.FromHex("#0D1914"),
        Text: Color.FromHex("#A1E1BD"),
        TextDark: Color.FromHex("#71B792"));

    public static readonly ColorPalette Negative = new(
        Base: Color.FromHex("#D1737F"),
        LightnessShift: 0f,
        ChromaShift: 0f,
        Element: Color.FromHex("#63363E"),
        HoveredElement: Color.FromHex("#7A424B"),
        PressedElement: Color.FromHex("#522C33"),
        DisabledElement: Color.FromHex("#342329"),
        Background: Color.FromHex("#27181D"),
        BackgroundLight: Color.FromHex("#372128"),
        BackgroundDark: Color.FromHex("#190E12"),
        Text: Color.FromHex("#F2B7C0"),
        TextDark: Color.FromHex("#C98994"));

    public static readonly ColorPalette Highlight = new(
        Base: Color.FromHex("#5AC5DC"),
        LightnessShift: 0f,
        ChromaShift: 0f,
        Element: Color.FromHex("#285866"),
        HoveredElement: Color.FromHex("#326D7D"),
        PressedElement: Color.FromHex("#214B57"),
        DisabledElement: Color.FromHex("#23363C"),
        Background: Color.FromHex("#10242A"),
        BackgroundLight: Color.FromHex("#17323A"),
        BackgroundDark: Color.FromHex("#0A181C"),
        Text: Color.FromHex("#9ADFED"),
        TextDark: Color.FromHex("#67B7C8"));

    public static readonly ArcaneTheme DefaultTheme = new(
        DefaultThemeId, "ui-lobby-theme-default", null,
        Primary, Secondary, Buttons, Highlight, NeonOutline);

    public static readonly ArcaneTheme GoldTheme = new(
        GoldThemeId, "ui-lobby-theme-gold", DiscordRole.SponsorTier2,
        Primary with
        {
            Base = Color.FromHex("#D7AA54"),
            Element = Color.FromHex("#554124"),
            HoveredElement = Color.FromHex("#725630"),
            PressedElement = Color.FromHex("#46351E"),
            DisabledElement = Color.FromHex("#30291F"),
            Background = Color.FromHex("#1D1913"),
            BackgroundLight = Color.FromHex("#292217"),
            BackgroundDark = Color.FromHex("#14110D"),
            Text = Color.FromHex("#F3E0B5"),
            TextDark = Color.FromHex("#BCA276"),
        },
        Secondary with
        {
            Base = Color.FromHex("#A88A5B"),
            Element = Color.FromHex("#352B1D"),
            HoveredElement = Color.FromHex("#483822"),
            PressedElement = Color.FromHex("#292217"),
            DisabledElement = Color.FromHex("#252019"),
            Background = Color.FromHex("#1B1813"),
            BackgroundLight = Color.FromHex("#272119"),
            BackgroundDark = Color.FromHex("#13110E"),
            Text = Color.FromHex("#EADFC9"),
            TextDark = Color.FromHex("#AF9D7E"),
        },
        Buttons with
        {
            Base = Color.FromHex("#C9A260"),
            Element = Color.FromHex("#483820"),
            HoveredElement = Color.FromHex("#604A28"),
            PressedElement = Color.FromHex("#382C1C"),
            DisabledElement = Color.FromHex("#29251E"),
            Background = Color.FromHex("#1B1813"),
            BackgroundLight = Color.FromHex("#272119"),
            BackgroundDark = Color.FromHex("#13110E"),
            Text = Color.FromHex("#F3E4C5"),
            TextDark = Color.FromHex("#BFA984"),
        },
        Highlight with
        {
            Base = Color.FromHex("#E0B861"),
            Element = Color.FromHex("#695029"),
            HoveredElement = Color.FromHex("#806136"),
            PressedElement = Color.FromHex("#513E24"),
            DisabledElement = Color.FromHex("#302A1E"),
            Background = Color.FromHex("#221B12"),
            BackgroundLight = Color.FromHex("#322719"),
            BackgroundDark = Color.FromHex("#19130D"),
            Text = Color.FromHex("#F7D98A"),
            TextDark = Color.FromHex("#C49D50"),
        },
        Color.FromHex("#E8BE69"));

    public static readonly ArcaneTheme CosmosTheme = new(
        CosmosThemeId, "ui-lobby-theme-cosmos", DiscordRole.SponsorTier2,
        Primary with
        {
            Base = Color.FromHex("#786AB0"),
            Element = Color.FromHex("#292140"),
            HoveredElement = Color.FromHex("#392B58"),
            PressedElement = Color.FromHex("#211B34"),
            DisabledElement = Color.FromHex("#1F1D2B"),
            Background = Color.FromHex("#0F0E19"),
            BackgroundLight = Color.FromHex("#191528"),
            BackgroundDark = Color.FromHex("#090913"),
            Text = Color.FromHex("#E5DFF7"),
            TextDark = Color.FromHex("#A69BC9"),
        },
        Secondary with
        {
            Base = Color.FromHex("#655D89"),
            Element = Color.FromHex("#211D31"),
            HoveredElement = Color.FromHex("#2D263F"),
            PressedElement = Color.FromHex("#1A1828"),
            DisabledElement = Color.FromHex("#1A1824"),
            Background = Color.FromHex("#11101B"),
            BackgroundLight = Color.FromHex("#1A1727"),
            BackgroundDark = Color.FromHex("#0B0A14"),
            Text = Color.FromHex("#E7E2F3"),
            TextDark = Color.FromHex("#A39ABA"),
        },
        Buttons with
        {
            Base = Color.FromHex("#8070B5"),
            Element = Color.FromHex("#2D2443"),
            HoveredElement = Color.FromHex("#40315A"),
            PressedElement = Color.FromHex("#251E37"),
            DisabledElement = Color.FromHex("#211F2E"),
            Background = Color.FromHex("#11101B"),
            BackgroundLight = Color.FromHex("#1A1727"),
            BackgroundDark = Color.FromHex("#0B0A14"),
            Text = Color.FromHex("#EDE7FA"),
            TextDark = Color.FromHex("#B0A5CF"),
        },
        Highlight with
        {
            Base = Color.FromHex("#A18CDA"),
            Element = Color.FromHex("#433263"),
            HoveredElement = Color.FromHex("#574178"),
            PressedElement = Color.FromHex("#36294F"),
            DisabledElement = Color.FromHex("#252238"),
            Background = Color.FromHex("#161124"),
            BackgroundLight = Color.FromHex("#231A34"),
            BackgroundDark = Color.FromHex("#0E0B19"),
            Text = Color.FromHex("#D9C8FA"),
            TextDark = Color.FromHex("#AD99D5"),
        },
        Color.FromHex("#AC97DD"));

    public static readonly IReadOnlyList<ArcaneTheme> Themes = [DefaultTheme, GoldTheme, CosmosTheme];
}
