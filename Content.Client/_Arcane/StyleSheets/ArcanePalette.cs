using Content.Client.Stylesheets.Palette;

namespace Content.Client._Arcane.StyleSheets;

public static class ArcanePalette
{
    public static readonly Color NeonOutline = Color.FromHex("#68D9EE");

    public static readonly ColorPalette Primary = new(
        Base: Color.FromHex("#647E87"),
        LightnessShift: 0f,
        ChromaShift: 0f,
        Element: Color.FromHex("#314A55"),
        HoveredElement: Color.FromHex("#3B5964"),
        PressedElement: Color.FromHex("#29414A"),
        DisabledElement: Color.FromHex("#20272B"),
        Background: Color.FromHex("#151A1E"),
        BackgroundLight: Color.FromHex("#20272B"),
        BackgroundDark: Color.FromHex("#0E1215"),
        Text: Color.FromHex("#BCCFD6"),
        TextDark: Color.FromHex("#84979F"));

    public static readonly ColorPalette Secondary = new(
        Base: Color.FromHex("#727A80"),
        LightnessShift: 0f,
        ChromaShift: 0f,
        Element: Color.FromHex("#2A2F34"),
        HoveredElement: Color.FromHex("#343B41"),
        PressedElement: Color.FromHex("#242A2F"),
        DisabledElement: Color.FromHex("#1C1F22"),
        Background: Color.FromHex("#1B1D21"),
        BackgroundLight: Color.FromHex("#25282D"),
        BackgroundDark: Color.FromHex("#121519"),
        Text: Color.FromHex("#D8DEE3"),
        TextDark: Color.FromHex("#959FA6"));

    public static readonly ColorPalette Buttons = new(
        Base: Color.FromHex("#717A80"),
        LightnessShift: 0f,
        ChromaShift: 0f,
        Element: Color.FromHex("#2D3338"),
        HoveredElement: Color.FromHex("#39434A"),
        PressedElement: Color.FromHex("#31515C"),
        DisabledElement: Color.FromHex("#202428"),
        Background: Color.FromHex("#1B1D21"),
        BackgroundLight: Color.FromHex("#25282D"),
        BackgroundDark: Color.FromHex("#121519"),
        Text: Color.FromHex("#D8DEE3"),
        TextDark: Color.FromHex("#959FA6"));

    public static readonly ColorPalette Positive = new(
        Base: Color.FromHex("#729D90"),
        LightnessShift: 0f,
        ChromaShift: 0f,
        Element: Color.FromHex("#345A4F"),
        HoveredElement: Color.FromHex("#3E685B"),
        PressedElement: Color.FromHex("#2B4B42"),
        DisabledElement: Color.FromHex("#24312D"),
        Background: Color.FromHex("#17231F"),
        BackgroundLight: Color.FromHex("#20312B"),
        BackgroundDark: Color.FromHex("#101713"),
        Text: Color.FromHex("#91C1B3"),
        TextDark: Color.FromHex("#6E9F91"));

    public static readonly ColorPalette Negative = new(
        Base: Color.FromHex("#A77B83"),
        LightnessShift: 0f,
        ChromaShift: 0f,
        Element: Color.FromHex("#5B3A42"),
        HoveredElement: Color.FromHex("#704750"),
        PressedElement: Color.FromHex("#4B3037"),
        DisabledElement: Color.FromHex("#30252A"),
        Background: Color.FromHex("#26191D"),
        BackgroundLight: Color.FromHex("#352329"),
        BackgroundDark: Color.FromHex("#190F12"),
        Text: Color.FromHex("#D2A3AB"),
        TextDark: Color.FromHex("#AE7D86"));

    public static readonly ColorPalette Highlight = new(
        Base: Color.FromHex("#648F99"),
        LightnessShift: 0f,
        ChromaShift: 0f,
        Element: Color.FromHex("#3A6370"),
        HoveredElement: Color.FromHex("#477580"),
        PressedElement: Color.FromHex("#31545E"),
        DisabledElement: Color.FromHex("#27383E"),
        Background: Color.FromHex("#172327"),
        BackgroundLight: Color.FromHex("#203137"),
        BackgroundDark: Color.FromHex("#10181B"),
        Text: Color.FromHex("#83B7C1"),
        TextDark: Color.FromHex("#6598A3"));
}
