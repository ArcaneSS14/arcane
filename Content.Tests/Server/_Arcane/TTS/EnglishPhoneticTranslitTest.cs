// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Arcane.TTS;
using NUnit.Framework;

namespace Content.Tests.Server._Arcane.TTS;

[TestFixture, TestOf(typeof(EnglishPhoneticTranslit))]
[Parallelizable(ParallelScope.All)]
public static class EnglishPhoneticTranslitTest
{
    [TestCase("the", "зэ")]
    [TestCase("this", "зис")]
    [TestCase("they", "зэй")]
    [TestCase("with", "виз")]
    [TestCase("other", "азэр")]
    [TestCase("have", "хэв")]
    [TestCase("know", "ноу")]
    [TestCase("through", "сру")]
    [TestCase("thought", "сот")]
    [TestCase("hello", "хэлоу")]
    public static void FunctionWordsUseDictionary(string word, string expected)
    {
        Assert.That(EnglishPhoneticTranslit.Transliterate(word), Is.EqualTo(expected));
    }

    [TestCase("security", "сэкйурити")]
    [TestCase("captain", "кэптин")]
    [TestCase("cargo", "карго")]
    [TestCase("nuclear", "нйуклэр")]
    [TestCase("station", "стэйшн")]
    [TestCase("emergency", "имэроджэнси")]
    [TestCase("traitor", "трэйтор")]
    [TestCase("plasma", "плазма")]
    public static void Ss14VocabularyUsesDictionary(string word, string expected)
    {
        Assert.That(EnglishPhoneticTranslit.Transliterate(word), Is.EqualTo(expected));
    }

    [TestCase("make", "мэйк")]
    [TestCase("time", "тайм")]
    [TestCase("name", "нэйм")]
    [TestCase("use", "йуз")]
    [TestCase("makes", "мэйкс")]
    [TestCase("table", "тэбл")]
    [TestCase("orange", "орэндж")]
    [TestCase("quick", "квик")]
    [TestCase("box", "бокс")]
    [TestCase("boxes", "боксиз")]
    [TestCase("city", "сити")]
    [TestCase("cat", "кэт")]
    [TestCase("gem", "джэм")]
    [TestCase("system", "систэм")]
    [TestCase("city", "сити")]
    [TestCase("meeting", "митинг")]
    [TestCase("knock", "нок")]
    [TestCase("write", "райт")]
    [TestCase("think", "тинк")]
    [TestCase("wanted", "вэнтид")]
    [TestCase("sky", "скай")]
    [TestCase("yellow", "йэлоу")]
    public static void RulesProduceCyrillicPronunciation(string word, string expected)
    {
        Assert.That(EnglishPhoneticTranslit.Transliterate(word), Is.EqualTo(expected));
    }

    [Test]
    public static void ResultIsPureCyrillicWithoutIotation()
    {
        var words = new[]
        {
            "security", "hello", "orange", "quick", "system", "table", "boxes", "wanted",
            "station", "nuclear", "emergency", "genetics", "singularity", "yellow",
        };

        foreach (var word in words)
        {
            var result = EnglishPhoneticTranslit.Transliterate(word);
            foreach (var c in result)
            {
                // The TTS engine reads е/ё/я/ю as plain letters without iotation or
                // softening, so transliteration output must never contain them.
                Assert.That(
                    c is (>= 'а' and <= 'я') or ' ' or (>= '0' and <= '9'),
                    $"Word '{word}' produced soft/iotated char '{c}' in '{result}'");
                Assert.That(
                    c is not ('е' or 'ё' or 'я' or 'ю'),
                    $"Word '{word}' produced iotated char '{c}' in '{result}'");
            }
        }
    }

    [Test]
    public static void CachedResultIsStable()
    {
        var first = EnglishPhoneticTranslit.Transliterate("security");
        var second = EnglishPhoneticTranslit.Transliterate("security");
        Assert.That(second, Is.EqualTo(first));
    }
}
