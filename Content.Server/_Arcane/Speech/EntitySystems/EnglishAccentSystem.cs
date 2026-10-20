using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Content.Server._Arcane.Speech.Components;
using Content.Server.Speech.Prototypes;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Arcane.Speech.EntitySystems;

public sealed class EnglishAccentSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const string Break = @"[.,!?…;:()«»""—–-]";
    private const string Vowels = "аеёиоуыэюя";

    private static readonly Regex RegexWord = new(@"\p{IsCyrillic}+");

    private static readonly Dictionary<char, string> Translit = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "yo",
        ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m",
        ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
        ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "shch", ['ъ'] = "",
        ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
    };

    private static readonly Dictionary<char, string> Latin = new()
    {
        ['р'] = "r", ['х'] = "h", ['ж'] = "zh", ['ш'] = "sh", ['ч'] = "ch",
    };

    private static readonly Dictionary<char, char> HardL = new()
    {
        ['я'] = 'а', ['ю'] = 'у', ['ё'] = 'о',
    };

    private readonly Dictionary<ProtoId<ReplacementAccentPrototype>, (Regex, string)[]> _words = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnglishAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnAccent(Entity<EnglishAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(ent, args.Message);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ReplacementAccentPrototype>())
            _words.Clear();
    }

    public string Accentuate(Entity<EnglishAccentComponent> ent, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        var msg = message;
        foreach (var (regex, replacement) in GetWords(ent.Comp.Accent))
        {
            msg = regex.Replace(msg, m => CopyCase(m.Value, replacement));
        }

        msg = RegexWord.Replace(msg, m => AccentuateWord(ent.Comp, m.Value));
        msg = AddPrefix(ent.Comp, msg);
        msg = AddSuffix(ent.Comp, msg);
        return msg;
    }

    // заменяет "да" на "yes" но в фразах "да нет конечно" замены не будет.
    private (Regex, string)[] GetWords(ProtoId<ReplacementAccentPrototype> id)
    {
        if (_words.TryGetValue(id, out var words))
            return words;

        words = _proto.TryIndex(id, out var proto) && proto.WordReplacements is { } replacements
            ? replacements.Select(kv => (
                    new Regex($@"(?<=(?:^|{Break})\s*){Regex.Escape(Loc.GetString(kv.Key))}(?=\s*(?:{Break}|$))", RegexOptions.IgnoreCase),
                    Loc.GetString(kv.Value)))
                .ToArray()
            : [];

        _words[id] = words;
        return words;
    }

    private string AccentuateWord(EnglishAccentComponent comp, string word)
    {
        var upperWord = word.Length > 1 && !word.Any(char.IsLower);

        if (_random.Prob(comp.TransliterateChance))
            return Transliterate(word, upperWord);

        var sb = new StringBuilder(word.Length + 2);
        var cooldown = 0;

        for (var i = 0; i < word.Length; i++)
        {
            var c = word[i];
            var lower = char.ToLowerInvariant(c);
            var upper = char.IsUpper(c);

            if (lower == 'ы' && _random.Prob(comp.YeryChance))
            {
                sb.Append(upper ? 'И' : 'и');
                continue;
            }

            if (lower is 'ь' or 'ъ' && i > 0 && _random.Prob(comp.SignChance))
                continue;

            if (lower == 'щ' && _random.Prob(comp.ShchChance))
            {
                sb.Append(upper ? 'Ш' : 'ш');
                continue;
            }

            if (lower == 'ц' && _random.Prob(comp.TseChance))
            {
                sb.Append(upper ? 'Т' : 'т');
                sb.Append(upperWord ? 'С' : 'с');
                continue;
            }

            if (lower == 'л' && i + 1 < word.Length
                && HardL.TryGetValue(char.ToLowerInvariant(word[i + 1]), out var hard)
                && _random.Prob(comp.HardLChance))
            {
                sb.Append(c);
                sb.Append(char.IsUpper(word[i + 1]) ? char.ToUpperInvariant(hard) : hard);
                i++;
                continue;
            }

            if (cooldown > 0)
            {
                cooldown--;
            }
            else if (Latin.TryGetValue(lower, out var latin) && _random.Prob(comp.LatinChance))
            {
                sb.Append(ApplyCase(latin, upper, upperWord));
                cooldown = comp.LatinCooldown;
                continue;
            }

            sb.Append(c);
        }

        if (sb.Length > 2 && _random.Prob(comp.StretchChance))
            Stretch(sb);

        return sb.ToString();
    }

    private static string Transliterate(string word, bool upperWord)
    {
        var sb = new StringBuilder(word.Length + 2);
        foreach (var c in word)
        {
            sb.Append(Translit.TryGetValue(char.ToLowerInvariant(c), out var latin)
                ? ApplyCase(latin, char.IsUpper(c), upperWord)
                : c);
        }

        return sb.ToString();
    }

    private void Stretch(StringBuilder word)
    {
        var vowels = new List<int>();
        for (var i = 0; i < word.Length; i++)
        {
            if (Vowels.Contains(char.ToLowerInvariant(word[i])))
                vowels.Add(i);
        }

        if (vowels.Count == 0)
            return;

        var index = _random.Pick(vowels);
        word.Insert(index, word[index]);
    }

    private static string ApplyCase(string latin, bool upper, bool upperWord)
    {
        if (!upper || latin.Length == 0)
            return latin;

        return upperWord ? latin.ToUpperInvariant() : char.ToUpperInvariant(latin[0]) + latin[1..];
    }

    private static string CopyCase(string source, string replacement)
    {
        if (replacement.Length == 0)
            return replacement;

        if (source.Length > 1 && !source.Any(char.IsLower))
            return replacement.ToUpperInvariant();

        return char.IsUpper(source[0]) ? char.ToUpperInvariant(replacement[0]) + replacement[1..] : replacement;
    }

    private string AddPrefix(EnglishAccentComponent comp, string message)
    {
        if (comp.Prefixes.Count == 0 || !_random.Prob(comp.PrefixChance))
            return message;

        var prefix = Loc.GetString(_random.Pick(comp.Prefixes));

        if (IsShouting(message))
            prefix = prefix.ToUpperInvariant();
        else if (message.Length > 1 && char.IsUpper(message[0]) && !char.IsUpper(message[1]))
            message = char.ToLowerInvariant(message[0]) + message[1..];

        return $"{prefix} {message}";
    }

    private string AddSuffix(EnglishAccentComponent comp, string message)
    {
        if (comp.Suffixes.Count == 0 || !_random.Prob(comp.SuffixChance))
            return message;

        var end = message.Length;
        while (end > 0 && (char.IsPunctuation(message[end - 1]) || char.IsWhiteSpace(message[end - 1])))
            end--;

        if (end == 0)
            return message;

        var suffix = Loc.GetString(_random.Pick(comp.Suffixes));
        if (IsShouting(message))
            suffix = suffix.ToUpperInvariant();

        return message[..end] + suffix + message[end..];
    }

    private static bool IsShouting(string message)
    {
        return message.Any(char.IsLetter) && !message.Any(char.IsLower);
    }
}
