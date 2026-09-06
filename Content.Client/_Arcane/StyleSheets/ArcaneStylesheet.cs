using System.Linq;
using Content.Client._Arcane.StyleSheets.Sheetlets;
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client.Stylesheets.Palette;
using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client._Arcane.StyleSheets;

public sealed class ArcaneStylesheet : CommonStylesheet, IButtonConfig
{
    public static readonly ResPath TextureRoot = new("/Textures/_Arcane/Interface");

    private const int PrimaryFontSize = 13;
    private const int FontSizeStep = 2;

    private readonly List<(string?, int)> _commonFontSizes = new()
    {
        (null, PrimaryFontSize),
        (StyleClass.FontSmall, PrimaryFontSize - FontSizeStep),
        (StyleClass.FontLarge, PrimaryFontSize + FontSizeStep),
    };

    public override string StylesheetName { get; }
    public override NotoFontFamilyStack BaseFont { get; }

    public override Dictionary<Type, ResPath[]> Roots => new()
    {
        { typeof(TextureResource), [TextureRoot, NanotrasenStylesheet.TextureRoot] },
    };

    public override ColorPalette PrimaryPalette => ArcanePalette.Primary;
    public override ColorPalette SecondaryPalette => ArcanePalette.Secondary;
    public override ColorPalette PositivePalette => ArcanePalette.Positive;
    public override ColorPalette NegativePalette => ArcanePalette.Negative;
    public override ColorPalette HighlightPalette => ArcanePalette.Highlight;

    ColorPalette IButtonConfig.ButtonPalette => ArcanePalette.Buttons;

    public ArcaneStylesheet(object config, StylesheetManager manager, string stylesheetName) : base(config)
    {
        StylesheetName = stylesheetName;
        BaseFont = new NotoFontFamilyStack(ResCache);

        var rules = new[]
        {
            GetRulesForFont(null, BaseFont, _commonFontSizes),
            new StyleRule[]
            {
                Element().Prop(Label.StylePropertyFont, BaseFont.GetFont(PrimaryFontSize)),
                Element<Label>().Prop(Label.StylePropertyFontColor, SecondaryPalette.Text),
            },
            GetAllSheetletRules<PalettedStylesheet, CommonSheetletAttribute>(manager),
            GetAllSheetletRules<ArcaneStylesheet, CommonSheetletAttribute>(manager),
            new ArcaneBaseControlsSheetlet().GetRules(this, config),
            new ArcaneLobbySheetlet().GetRules(this, config),
            new ArcaneChatSheetlet().GetRules(this, config),
            new ArcaneAHelpSheetlet().GetRules(this, config),
            new ArcanePdaSheetlet().GetRules(this, config),
            new ArcaneDetailExaminableSheetlet().GetRules(this, config),
        };

        Stylesheet = new Stylesheet(rules.SelectMany(ruleSet => ruleSet).ToArray());
    }
}
