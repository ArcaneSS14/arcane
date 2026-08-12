// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;

namespace Content.Server.Speech.EntitySystems;

public sealed class MothAccentSystem : EntitySystem
{
    private static readonly Regex RegexLowerBuzz = new Regex("z{1,3}");
    private static readonly Regex RegexUpperBuzz = new Regex("Z{1,3}");
    // Arcane-Start
    private static readonly Regex RegexLowerBuzzRus = new Regex("з+");
    private static readonly Regex RegexUpperBuzzRus = new Regex("З+");
    private static readonly Regex RegexLowerZhRus = new Regex("ж+");
    private static readonly Regex RegexUpperZhRus = new Regex("Ж+");
    //Arcane-End

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MothAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, MothAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // buzzz
        message = RegexLowerBuzz.Replace(message, "zzz");
        // buZZZ
        message = RegexUpperBuzz.Replace(message, "ZZZ");
        // Arcane-Start
        message = RegexLowerBuzzRus.Replace(message, "ззз");
        message = RegexUpperBuzzRus.Replace(message, "ЗЗЗ");
        message = RegexLowerZhRus.Replace(message, "жжж");
        message = RegexUpperZhRus.Replace(message, "ЖЖЖ");
        // Arcane-End

        args.Message = message;
    }
}
