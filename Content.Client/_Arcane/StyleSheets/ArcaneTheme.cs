using Content.Client.Stylesheets.Palette;
using Content.Shared._Arcane.DiscordRoles;

namespace Content.Client._Arcane.StyleSheets;

public sealed record ArcaneTheme(
    string Id,
    string NameLocId,
    DiscordRole? RequiredRole,
    ColorPalette Primary,
    ColorPalette Secondary,
    ColorPalette Buttons,
    ColorPalette Highlight,
    Color Accent
);
