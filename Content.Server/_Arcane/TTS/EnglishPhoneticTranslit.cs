// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Text;

namespace Content.Server._Arcane.TTS;

/// <summary>
/// English-to-Russian phonetic transliteration used by the TTS sanitize pipeline.
/// The Russian TTS engine reads Cyrillic text, so English words are rewritten into
/// Cyrillic that approximates English pronunciation instead of naive letter-by-letter
/// transliteration.
/// </summary>
/// <remarks>
/// The TTS engine reads е/ё/я/ю as plain letters without iotation or softening,
/// so all output uses only hard vowels: [ɛ] is "э", a [ju] glide is written "йу",
/// etc. Priority order inside <see cref="Transliterate"/>:
/// <list type="number">
/// <item>whole-word dictionary (function words + domain vocabulary + rule exceptions)</item>
/// <item>multi-letter patterns, longest match first (digraphs, suffixes, vowel pairs)</item>
/// <item>context rules (magic e, silent e, -ed, -es, c/g/s/y context, letter-by-letter fallback)</item>
/// </list>
/// </remarks>
public static class EnglishPhoneticTranslit
{
    private const int CacheLimit = 8192;
    private static readonly ConcurrentDictionary<string, string> Cache = new();

    /// <summary>
    /// Fixed multi-letter patterns. Matched longest-first at the current scan position.
    /// </summary>
    private static readonly (string Pattern, string Replacement)[] Rules =
    {
        // Suffix-like patterns.
        ("ation", "эйшн"),
        ("ution", "ушн"),
        ("ition", "ишн"),
        ("otion", "оушн"),
        ("ature", "эйчэр"),
        ("ision", "ижн"),
        ("ssion", "шн"),
        ("sion", "жн"),
        ("cial", "шл"),
        ("tial", "шл"),
        ("cian", "шн"),
        ("tious", "шэс"),
        ("cious", "шэс"),
        ("ough", "оу"),
        ("ought", "от"),
        ("augh", "о"),
        ("eigh", "эй"),
        ("igh", "ай"),
        ("dge", "дж"),
        ("nge", "ндж"),
        ("tion", "шн"),
        ("ture", "чэр"),
        ("sure", "жэ"),

        // R-controlled vowels and vowel digraphs.
        ("eer", "ир"),
        ("air", "эр"),
        ("are", "эр"),
        ("ear", "ир"),
        ("our", "эр"),
        ("all", "ол"),
        ("old", "оулд"),
        ("ew", "йу"),
        ("ee", "и"),
        ("ea", "и"),
        ("ei", "эй"),
        ("ey", "эй"),
        ("ai", "эй"),
        ("ay", "эй"),
        ("oa", "оу"),
        ("oe", "оу"),
        ("oo", "у"),
        ("ou", "ау"),
        ("ow", "ау"),
        ("oi", "ой"),
        ("oy", "ой"),
        ("au", "о"),
        ("aw", "о"),
        ("ue", "у"),
        ("ui", "у"),
        ("ar", "ар"),
        ("er", "эр"),
        ("ir", "эр"),
        ("or", "ор"),
        ("ur", "эр"),

        // Consonant digraphs and silent-letter clusters.
        ("ch", "ч"),
        ("sh", "ш"),
        ("ph", "ф"),
        ("wh", "в"),
        ("ck", "к"),
        ("ng", "нг"),
        ("qu", "кв"),
        ("gh", "г"),
        ("th", "т"),
    };

    /// <summary>
    /// Silent-letter clusters that drop their first letter only at the start of a word
    /// (write, know, psychology, gnaw). Never applied mid-word: "ships" keeps its p,
    /// "signal" keeps its g, "darkness" keeps its k.
    /// </summary>
    private static readonly (string Pattern, string Replacement)[] WordInitialRules =
    {
        ("wr", "р"),
        ("kn", "н"),
        ("ps", "с"),
        ("gn", "н"),
    };

    private static readonly HashSet<string> SoftThWords = new()
    {
        "other", "another", "mother", "father", "brother", "further", "furthest",
        "either", "neither", "whether", "weather", "smooth", "breathe", "clothing",
        "leather", "feather", "together", "altogether", "rhythm", "thence",
    };

    private static readonly HashSet<char> VoicelessConsonants = new()
    {
        'p', 'k', 'f', 's', 'h', 'c', 'x',
    };

    private static readonly HashSet<char> Sibilants = new()
    {
        's', 'x', 'z',
    };

    static EnglishPhoneticTranslit()
    {
        Array.Sort(Rules, (a, b) => b.Pattern.Length.CompareTo(a.Pattern.Length));
    }

    /// <summary>
    /// Transliterates a single lowercase English word into Cyrillic.
    /// Results are cached because the same vocabulary repeats constantly in speech.
    /// </summary>
    public static string Transliterate(string word)
    {
        if (Cache.TryGetValue(word, out var cached))
            return cached;

        var result = TransliterateUncached(word);

        if (Cache.Count >= CacheLimit)
            Cache.Clear();

        Cache.TryAdd(word, result);
        return result;
    }

    private static string TransliterateUncached(string word)
    {
        if (WordDictionary.TryGetValue(word, out var known))
            return known;

        var sb = new StringBuilder(word.Length * 2);
        var i = 0;
        while (i < word.Length)
        {
            var ch = word[i];

            // Contractions: "i'm"/"i'll"/"i've"/"i'd" start with "ай", elsewhere the
            // apostrophe is dropped and the rest is spelled out (dictionary covers
            // the common forms with proper phonetics).
            if (ch == '\'')
            {
                i++;
                continue;
            }

            if (i == 0 && ch == 'i' && word.Length > 1 && word[1] == '\'')
            {
                sb.Append("ай");
                i++;
                continue;
            }

            if (!char.IsAsciiLetter(ch))
            {
                sb.Append(ch);
                i++;
                continue;
            }

            // Word-initial silent-letter clusters (wr/kn/ps/gn).
            if (i == 0 && TryMatchWordInitial(word, out var cluster, out var clusterLen))
            {
                sb.Append(cluster);
                i += clusterLen;
                continue;
            }

            // -ed suffix: t/d -> "ид", voiceless -> "т", otherwise "д".
            if (ch == 'e' && i == word.Length - 2 && word[^1] == 'd' && word.Length >= 4)
            {
                var prev = word[i - 1];
                if (prev is 't' or 'd')
                    sb.Append("ид");
                else if (VoicelessConsonants.Contains(prev))
                    sb.Append('т');
                else
                    sb.Append('д');

                i += 2;
                continue;
            }

            // -es suffix: after sibilants -> "из", after voiceless -> "с", otherwise "з".
            if (ch == 'e' && i == word.Length - 2 && word[^1] == 's' && word.Length >= 4)
            {
                var prev = word[i - 1];
                if (Sibilants.Contains(prev) || word.EndsWith("ches") || word.EndsWith("shes"))
                    sb.Append("из");
                else if (VoicelessConsonants.Contains(prev))
                    sb.Append('с');
                else
                    sb.Append('з');

                i += 2;
                continue;
            }

            // Final silent e (not covered by magic-e or suffixes above).
            if (ch == 'e' && i == word.Length - 1 && i > 0 && IsConsonant(word[i - 1]))
            {
                i++;
                continue;
            }

            // "-le" syllable after a consonant (apple -> эпл).
            if (ch == 'l' && i == word.Length - 2 && word[^1] == 'e' && i > 0 && IsConsonant(word[i - 1]))
            {
                sb.Append('л');
                i += 2;
                continue;
            }

            // Magic e: vowel + single consonant + final e -> diphthong (make, time, use).
            if (IsMagicE(word, i))
            {
                sb.Append(ch switch
                {
                    'a' => "эй",
                    'e' => "и",
                    'i' => "ай",
                    'o' => "оу",
                    'u' => IsAfterConsonant(word, i) ? "йу" : "у",
                    _ => ch.ToString(),
                });
                i++;
                continue;
            }

            // Fixed multi-letter patterns (digraphs, suffixes, vowel pairs).
            if (TryMatchPattern(word, i, out var replacement, out var length))
            {
                // "sh" before a front vowel is the softer щ (shield, ship); elsewhere ш (shop, shy).
                if (replacement == "ш" && word[i] == 's'
                    && i + 2 < word.Length && word[i + 2] is 'e' or 'i')
                    replacement = "щ";

                // "th" is voiced ("з") in a set of common words.
                if (replacement == "т" && word[i] == 't' && SoftThWords.Contains(word))
                    replacement = "з";

                sb.Append(replacement);
                i += length;
                continue;
            }

            // Context-sensitive single letters.
            switch (ch)
            {
                case 'c':
                    sb.Append(IsBeforeEiy(word, i) ? 'с' : 'к');
                    i++;
                    continue;
                case 'g':
                    // Default hard g (get, give, forget, gear). Only final -ge is soft
                    // (age, page, large); other soft-g words live in the word dictionary.
                    if (i == word.Length - 2 && word[^1] == 'e')
                        sb.Append("дж");
                    else
                        sb.Append('г');
                    i++;
                    continue;
                case 's':
                    {
                        var betweenVowels = i > 0 && i + 1 < word.Length
                            && IsVowel(word[i - 1]) && IsVowel(word[i + 1]);
                        sb.Append(betweenVowels ? 'з' : 'с');
                        i++;
                        continue;
                    }
                case 'y':
                    if (i == 0)
                        sb.Append('й');
                    else if (i == word.Length - 1)
                        sb.Append(word.Length <= 3 ? "ай" : "и");
                    else
                        sb.Append('и');
                    i++;
                    continue;
                case 'j':
                    sb.Append("дж");
                    i++;
                    continue;
                case 'q':
                    sb.Append('к');
                    i++;
                    continue;
                case 'x':
                    sb.Append("кс");
                    i++;
                    continue;
                case 'b':
                case 'd':
                case 'f':
                case 'h':
                case 'k':
                case 'l':
                case 'm':
                case 'n':
                case 'p':
                case 'r':
                case 't':
                case 'v':
                case 'w':
                case 'z':
                    sb.Append(ch switch
                    {
                        'b' => 'б',
                        'd' => 'д',
                        'f' => 'ф',
                        'h' => 'х',
                        'k' => 'к',
                        'l' => 'л',
                        'm' => 'м',
                        'n' => 'н',
                        'p' => 'п',
                        'r' => 'р',
                        't' => 'т',
                        'v' => 'в',
                        'w' => 'в',
                        _ => 'з',
                    });
                    i++;
                    continue;
                case 'a':
                    sb.Append('э');
                    i++;
                    continue;
                case 'e':
                    sb.Append('э');
                    i++;
                    continue;
                case 'i':
                    sb.Append('и');
                    i++;
                    continue;
                case 'o':
                    sb.Append('о');
                    i++;
                    continue;
                case 'u':
                    sb.Append('а');
                    i++;
                    continue;
                default:
                    sb.Append(ch);
                    i++;
                    continue;
            }
        }

        return sb.ToString();
    }

    private static bool TryMatchPattern(string word, int index, out string replacement, out int length)
    {
        foreach (var (pattern, value) in Rules)
        {
            if (index + pattern.Length <= word.Length
                && word.AsSpan(index, pattern.Length).Equals(pattern, StringComparison.Ordinal))
            {
                replacement = value;
                length = pattern.Length;
                return true;
            }
        }

        replacement = string.Empty;
        length = 0;
        return false;
    }

    private static bool TryMatchWordInitial(string word, out string replacement, out int length)
    {
        foreach (var (pattern, value) in WordInitialRules)
        {
            if (pattern.Length <= word.Length
                && word.AsSpan(0, pattern.Length).Equals(pattern, StringComparison.Ordinal))
            {
                replacement = value;
                length = pattern.Length;
                return true;
            }
        }

        replacement = string.Empty;
        length = 0;
        return false;
    }

    /// <summary>
    /// True when the vowel at <paramref name="index"/> is followed by a single consonant
    /// and a final e (optionally followed by a plural/ed s or d).
    /// </summary>
    private static bool IsMagicE(string word, int index)
    {
        if (!IsVowel(word[index]))
            return false;

        if (index + 2 >= word.Length || word[index + 2] != 'e')
            return false;

        if (!IsConsonant(word[index + 1]))
            return false;

        var after = index + 3;
        if (after == word.Length)
            return true;

        if (after + 1 != word.Length)
            return false;

        // Trailing s/d is a plural/ed suffix only when the consonant is not a
        // sibilant: "makes" is magic e + s, but "boxes" is box + -es.
        if (Sibilants.Contains(word[index + 1]))
            return false;

        return word[after] == 's' || word[after] == 'd';
    }

    private static bool IsBeforeEiy(string word, int index)
    {
        return index + 1 < word.Length && word[index + 1] is 'e' or 'i' or 'y';
    }

    private static bool IsAfterConsonant(string word, int index)
    {
        return index > 0 && IsConsonant(word[index - 1]);
    }

    private static bool IsVowel(char ch)
    {
        return ch is 'a' or 'e' or 'i' or 'o' or 'u' or 'y';
    }

    private static bool IsConsonant(char ch)
    {
        return char.IsAsciiLetter(ch) && !IsVowel(ch);
    }

    /// <summary>
    /// Whole-word pronunciations. Keys must be lowercase.
    /// Function words and common exceptions to the rule tables come first,
    /// then Space Station 14 vocabulary that appears often on radio.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> WordDictionary =
        new Dictionary<string, string>
        {
            // Function words.
            {"the", "зэ"},
            {"a", "э"},
            {"an", "эн"},
            {"to", "ту"},
            {"of", "ов"},
            {"and", "энд"},
            {"or", "ор"},
            {"but", "бат"},
            {"in", "ин"},
            {"on", "он"},
            {"at", "эт"},
            {"is", "из"},
            {"are", "ар"},
            {"was", "воз"},
            {"were", "вэр"},
            {"be", "би"},
            {"been", "бин"},
            {"being", "биинг"},
            {"it", "ит"},
            {"its", "итс"},
            {"this", "зис"},
            {"that", "зэт"},
            {"these", "зийз"},
            {"those", "зоуз"},
            {"there", "зэр"},
            {"here", "хир"},
            {"they", "зэй"},
            {"them", "зэм"},
            {"their", "зэр"},
            {"theirs", "зэрз"},
            {"he", "хи"},
            {"him", "хим"},
            {"his", "хиз"},
            {"she", "ши"},
            {"her", "хэр"},
            {"we", "уи"},
            {"our", "ауэр"},
            {"us", "ас"},
            {"you", "йу"},
            {"your", "йор"},

            // Contractions (apostrophe stays in the sanitized word).
            {"i'm", "айм"},
            {"i'll", "айл"},
            {"i've", "айв"},
            {"i'd", "айд"},
            {"you're", "йур"},
            {"you'll", "юл"},
            {"you've", "юв"},
            {"you'd", "юд"},
            {"he's", "хиз"},
            {"he'll", "хил"},
            {"he'd", "хид"},
            {"she's", "шиз"},
            {"she'll", "шил"},
            {"she'd", "шид"},
            {"it's", "итс"},
            {"it'll", "итл"},
            {"we're", "уир"},
            {"we'll", "уил"},
            {"we've", "уив"},
            {"we'd", "уид"},
            {"they're", "зэйр"},
            {"they'll", "зэйл"},
            {"they've", "зэйв"},
            {"they'd", "зэйд"},
            {"that's", "зэтс"},
            {"that'll", "зэтл"},
            {"there's", "зэрс"},
            {"here's", "хирс"},
            {"what's", "уотс"},
            {"who's", "хуз"},
            {"where's", "уэрз"},
            {"when's", "уэнз"},
            {"why's", "вайз"},
            {"isn't", "изнт"},
            {"aren't", "арнт"},
            {"wasn't", "вознт"},
            {"weren't", "вэрнт"},
            {"don't", "доунт"},
            {"doesn't", "дазнт"},
            {"didn't", "диднт"},
            {"can't", "кэнт"},
            {"won't", "воунт"},
            {"wouldn't", "вуднт"},
            {"shouldn't", "шуднт"},
            {"couldn't", "куднт"},
            {"mustn't", "мастнт"},
            {"haven't", "хэвнт"},
            {"hasn't", "хэзнт"},
            {"hadn't", "хэднт"},
            {"let's", "лэтс"},
            {"i", "ай"},
            {"me", "ми"},
            {"my", "май"},
            {"who", "ху"},
            {"whose", "хуз"},
            {"what", "уот"},
            {"where", "уэр"},
            {"when", "уэн"},
            {"why", "вай"},
            {"how", "хау"},
            {"which", "уич"},
            {"while", "уайл"},
            {"because", "бикоз"},
            {"if", "иф"},
            {"then", "зэн"},
            {"than", "зэн"},
            {"as", "эз"},
            {"with", "виз"},
            {"without", "визаут"},
            {"for", "фор"},
            {"from", "фром"},
            {"by", "бай"},
            {"about", "эбаут"},
            {"into", "инту"},
            {"onto", "онту"},
            {"up", "ап"},
            {"down", "даун"},
            {"out", "аут"},
            {"off", "оф"},
            {"over", "оувэр"},
            {"under", "андэр"},
            {"again", "эгэн"},
            {"also", "олсоу"},
            {"not", "нот"},
            {"no", "ноу"},
            {"yes", "йэс"},
            {"do", "ду"},
            {"does", "даз"},
            {"did", "дид"},
            {"done", "дан"},
            {"have", "хэв"},
            {"has", "хэз"},
            {"had", "хэд"},
            {"can", "кан"},
            {"cannot", "канот"},
            {"cant", "кант"},
            {"will", "уил"},
            {"would", "вуд"},
            {"shall", "шэл"},
            {"should", "шуд"},
            {"must", "маст"},
            {"may", "мэй"},
            {"might", "майт"},
            {"let", "лэт"},
            {"get", "гэт"},
            {"go", "гоу"},
            {"going", "гоуинг"},
            {"gone", "гон"},
            {"come", "кам"},
            {"came", "кэйм"},
            {"back", "бэк"},
            {"good", "гуд"},
            {"well", "уэл"},
            {"very", "вэри"},
            {"much", "мач"},
            {"more", "моу"},
            {"most", "моуст"},
            {"all", "ол"},
            {"any", "эни"},
            {"some", "сам"},
            {"one", "уан"},
            {"only", "оунли"},
            {"just", "джаст"},
            {"now", "нау"},
            {"new", "нйу"},
            {"like", "лайк"},
            {"know", "ноу"},
            {"knew", "нйу"},
            {"see", "си"},
            {"saw", "со"},
            {"look", "лук"},
            {"take", "тэйк"},
            {"made", "мэйд"},
            {"make", "мэйк"},
            {"time", "тайм"},
            {"way", "уэй"},
            {"day", "дэй"},
            {"man", "мэн"},
            {"men", "мэн"},
            {"woman", "вумэн"},
            {"people", "пипл"},
            {"other", "азэр"},
            {"another", "эназэр"},
            {"same", "сэйм"},
            {"too", "ту"},
            {"so", "соу"},
            {"right", "райт"},
            {"left", "лэфт"},
            {"need", "нид"},
            {"want", "уонт"},
            {"even", "ивэн"},
            {"still", "стил"},
            {"yet", "йэт"},
            {"ever", "эвэр"},
            {"never", "нэвэр"},
            {"please", "плиз"},
            {"thanks", "сэнкс"},
            {"thank", "сэнк"},
            {"sorry", "сори"},
            {"hello", "хэлоу"},
            {"hi", "хай"},
            {"bye", "бай"},
            {"goodbye", "гудбай"},
            {"welcome", "уэлкам"},
            {"congratulations", "кэнгрэтиулэйшнз"},
            {"congrats", "кэнгрэтс"},
            {"cheers", "чирз"},
            {"cheerio", "чирйо"},
            {"cheerioes", "чирйоуз"},
            {"cheerios", "чирйоуз"},
            {"cheerio's", "чирйоуз"},
            {"cheerioes'", "чирйоуз"},
            {"cheerios'", "чирйоуз"},
            {"help", "хэлп"},
            {"minute", "минит"},
            {"minutes", "минитс"},
            {"okay", "окэй"},
            {"ok", "окэй"},
            {"maybe", "мэйби"},
            {"sure", "щур"},
            {"fine", "файн"},
            {"great", "грэйт"},
            {"give", "гив"},
            {"forget", "фогэт"},
            {"forgotten", "фоготэн"},
            {"begin", "бигин"},
            {"girl", "гёрл"},
            {"gift", "гифт"},
            {"giant", "джайэнт"},
            {"gem", "джэм"},
            {"general", "джэнэрэл"},
            {"gentle", "джэнтл"},
            {"germ", "джерм"},
            {"gym", "джим"},
            {"magic", "мэджик"},
            {"logic", "лоджик"},
            {"challenge", "чэлэндж"},
            {"large", "лардж"},
            {"charge", "чардж"},
            {"live", "лив"},
            {"love", "лав"},
            {"red", "рэд"},
            {"bed", "бэд"},
            {"old", "оулд"},
            {"cold", "коулд"},
            {"gold", "гоулд"},
            {"hold", "хоулд"},
            {"told", "тоулд"},
            {"sold", "соулд"},
            {"bold", "боулд"},
            {"find", "файнд"},
            {"kind", "кайнд"},
            {"mind", "майнд"},
            {"child", "чайлд"},
            {"children", "чилдрэн"},
            {"laugh", "лаф"},
            {"water", "уотэр"},
            {"father", "фазэр"},
            {"mother", "мазэр"},
            {"brother", "бразэр"},
            {"together", "тугэзэр"},
            {"friend", "фрэнд"},
            {"fight", "файт"},
            {"night", "найт"},
            {"light", "лайт"},
            {"white", "уайт"},
            {"eye", "ай"},
            {"eyes", "айз"},
            {"away", "эуэй"},
            {"answer", "энсэ"},
            {"though", "тоу"},
            {"through", "сру"},
            {"thought", "сот"},
            {"rough", "раф"},
            {"tough", "таф"},
            {"enough", "энэф"},
            {"cough", "коф"},
            {"bought", "бот"},
            {"brought", "брот"},
            {"fought", "фот"},
            {"ought", "от"},
            {"women", "вимэн"},
            {"play", "плэй"},
            {"played", "плэйд"},
            {"playing", "плэйинг"},
            {"two", "ту"},
            {"three", "сри"},
            {"four", "фор"},
            {"five", "файв"},
            {"six", "сикс"},
            {"seven", "сэвэн"},
            {"eight", "эйт"},
            {"nine", "найн"},
            {"ten", "тэн"},
            {"hundred", "хандрид"},
            {"thousand", "cаузэнд"},
            {"million", "милион"},
            {"everyone", "эвриуан"},
            {"everything", "эврифинг"},
            {"everybody", "эврибади"},
            {"anyone", "эниуан"},
            {"anybody", "энибади"},
            {"someone", "сомуан"},
            {"somebody", "сомбади"},
            {"nobody", "нобади"},
            {"everywhere", "эвэривэр"},

            // Space Station 14 vocabulary.
            {"security", "сэкйурити"},
            {"captain", "кэптин"},
            {"cargo", "карго"},
            {"nuclear", "нйуклэр"},
            {"nuke", "нйук"},
            {"nukies", "нйукис"},
            {"nukie", "нйуки"},
            {"shuttle", "шатл"},
            {"clown", "клаун"},
            {"assistant", "эсистэнт"},
            {"engineer", "инжинир"},
            {"medical", "мэдикал"},
            {"science", "сайэнс"},
            {"traitor", "трэйтор"},
            {"syndicate", "синдикэт"},
            {"plasma", "плазма"},
            {"monkey", "манки"},
            {"slime", "слайм"},
            {"station", "стэйшн"},
            {"yellow", "йэлоу"},
            {"used", "йузт"},
            {"maintenance", "мэйнтэнэнс"},
            {"armory", "армори"},
            {"brig", "бриг"},
            {"chapel", "чэпл"},
            {"bar", "бар"},
            {"kitchen", "китчин"},
            {"atmos", "этмос"},
            {"robotics", "роботикс"},
            {"chemistry", "кэмистри"},
            {"genetics", "джэнэтикс"},
            {"warden", "уордн"},
            {"detective", "дитэктив"},
            {"chaplain", "чэплин"},
            {"salvage", "салвэдж"},
            {"hop", "хоп"},
            {"hos", "хос"},
            {"ce", "си и"},
            {"qm", "ку эм"},
            {"medbay", "мэдбэй"},
            {"virology", "вайролоджи"},
            {"toxins", "токсинс"},
            {"research", "рисэрч"},
            {"cyborg", "сайборг"},
            {"borg", "борг"},
            {"silicon", "сайликон"},
            {"pai", "пэйи"},
            {"escape", "искэйп"},
            {"arrivals", "эрайвалз"},
            {"emergency", "имэрджэнси"},
            {"operative", "опэрэтив"},
            {"revolutionary", "рэволйушнэри"},
            {"heretic", "хэритик"},
            {"wizard", "уизард"},
            {"mime", "майм"},
            {"botanist", "ботэнист"},
            {"chef", "шэф"},
            {"bartender", "бартэндэр"},
            {"lawyer", "лоиэ"},
            {"curator", "кйурэтор"},
            {"quartermaster", "куортэрмэстэр"},
            {"miner", "майнэр"},
            {"shaft", "шафт"},
            {"server", "сэрвэр"},
            {"pet", "пэт"},
            {"spider", "спайдэр"},
            {"dragon", "драгон"},
            {"queen", "квин"},
            {"human", "хйумэн"},
            {"lizard", "лизард"},
            {"moth", "мос"},
            {"corgi", "корги"},
            {"space", "спэйс"},
            {"suit", "сйут"},
            {"hardsuit", "хардсйут"},
            {"voidsuit", "войдсйут"},
            {"mask", "маск"},
            {"helmet", "хэлмит"},
            {"armor", "армор"},
            {"vest", "вэст"},
            {"baton", "бэтон"},
            {"disabler", "дизэйблэр"},
            {"laser", "лэйзэр"},
            {"gun", "ган"},
            {"rifle", "райфл"},
            {"pistol", "пистол"},
            {"energy", "энэрджи"},
            {"sword", "сворд"},
            {"grenade", "грэнэйд"},
            {"bomb", "бомб"},
            {"reactor", "риактор"},
            {"singularity", "сингйулэрити"},
            {"tesla", "тэсла"},
            {"radiation", "рэйдиэйшн"},
            {"supermatter", "супэрмэтэр"},
            {"solar", "соулар"},
            {"turret", "тэрэт"},
            {"camera", "камэрэ"},
            {"upload", "аплоуд"},
            {"law", "ло"},
            {"laws", "лоуз"},
            {"storm", "сторм"},
            {"meteor", "митиор"},
            {"breach", "брич"},
            {"hull", "хал"},
            {"vent", "вэнт"},
            {"scrubber", "скрабэр"},
            {"pipe", "пайп"},
            {"air", "эр"},
            {"oxygen", "оксигэн"},
            {"tritium", "тритиум"},
            {"fire", "файэр"},
            {"dead", "дэд"},
            {"crit", "крит"},
            {"health", "хэлс"},
            {"heal", "хил"},
            {"surgery", "сэрджэри"},
            {"organ", "орган"},
            {"limb", "лим"},
            {"blood", "блад"},
            {"pill", "пил"},
            {"syringe", "сириндж"},
            {"patch", "пэтч"},
            {"bandage", "бэндэдж"},
            {"defib", "дэфиб"},
            {"revive", "ривайв"},
            {"corpse", "корпс"},
            {"body", "боди"},
            {"ghost", "гоуст"},
            {"antag", "энтэг"},
            {"round", "раунд"},
            {"ops", "опс"},
            {"war", "уор"},
            {"blob", "блоб"},
            {"cult", "калт"},
            {"changeling", "чэйнджлинг"},
        };
}
