// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;

namespace Content.Server.Speech.EntitySystems;

public sealed class LizardAccentSystem : EntitySystem
{
    private static readonly Regex RegexLowerS = new("s+");
    private static readonly Regex RegexUpperS = new("S+");
    private static readonly Regex RegexInternalX = new(@"(\w)x");
    private static readonly Regex RegexLowerEndX = new(@"\bx([\-|r|R]|\b)");
    private static readonly Regex RegexUpperEndX = new(@"\bX([\-|r|R]|\b)");
    // Arcane-Start
    private static readonly Regex RegexLowerSRus = new("с+");
    private static readonly Regex RegexUpperSRus = new("С+");
    private static readonly Regex RegexLowerChRus = new("ч+");
    private static readonly Regex RegexUpperChRus = new("Ч+");
    private static readonly Regex RegexLowerShRus = new("ш+");
    private static readonly Regex RegexUpperShRus = new("Ш+");
    private static readonly Regex RegexLowerZRus = new("з+");
    private static readonly Regex RegexUpperZRus = new("З+");
    // Arcane-End

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LizardAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, LizardAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // hissss
        message = RegexLowerS.Replace(message, "sss");
        // hiSSS
        message = RegexUpperS.Replace(message, "SSS");
        // ekssit
        message = RegexInternalX.Replace(message, "$1kss");
        // ecks
        message = RegexLowerEndX.Replace(message, "ecks$1");
        // eckS
        message = RegexUpperEndX.Replace(message, "ECKS$1");
        // Arcane-Start
        message = RegexLowerSRus.Replace(message, "ссс");
        message = RegexUpperSRus.Replace(message, "ССС");
        message = RegexLowerChRus.Replace(message, "щщщ");
        message = RegexUpperChRus.Replace(message, "ЩЩЩ");
        message = RegexLowerShRus.Replace(message, "шшш");
        message = RegexUpperShRus.Replace(message, "ШШШ");
        message = RegexLowerZRus.Replace(message, "ссс");
        message = RegexUpperZRus.Replace(message, "ССС");
        // Arcane-End

        args.Message = message;
    }
}
