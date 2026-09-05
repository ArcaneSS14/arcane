using Content.Client.Stylesheets.Palette;

namespace Content.Client._Arcane.StyleSheets;

public static class ArcanePalette
{
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
}
