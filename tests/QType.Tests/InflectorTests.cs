using System.Linq;
using QType.COMMON.Morphology;
using Xunit;

namespace QType.Tests;

public class InflectorTests
{
    // ── Plural — the most-bug-prone area, exhaustive coverage ──
    [Theory]
    [InlineData("бала", "балалар")]   // vowel ending
    [InlineData("қала", "қалалар")]
    [InlineData("көше", "көшелер")]
    [InlineData("ат",   "аттар")]     // voiceless т
    [InlineData("кітап", "кітаптар")]
    [InlineData("ас",   "астар")]
    [InlineData("сөздік", "сөздіктер")]
    [InlineData("мектеп", "мектептер")]
    [InlineData("ұл",   "ұлдар")]     // sonorant л → -дар (was bug: -лар)
    [InlineData("ел",   "елдер")]
    [InlineData("сөз",  "сөздер")]    // sonorant з → -дер (was bug: -лер)
    [InlineData("көз",  "көздер")]
    [InlineData("адам", "адамдар")]   // nasal м → -дар
    [InlineData("үй",   "үйлер")]     // й → -лер
    [InlineData("тау",  "таулар")]    // у → -лар
    public void Plural_form_is_correct(string lemma, string expected)
    {
        var forms = KazakhInflector.Inflect(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == "pl" && f.Form == expected);
    }

    // ── Genitive ──
    [Theory]
    [InlineData("бала", "баланың")]
    [InlineData("көше", "көшенің")]
    [InlineData("кітап", "кітаптың")]
    [InlineData("сөздік", "сөздіктің")]
    [InlineData("адам", "адамның")]
    public void Genitive_form_is_correct(string lemma, string expected)
    {
        var forms = KazakhInflector.Inflect(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == "gen" && f.Form == expected);
    }

    // ── Dative — consonant harmony is the key tricky case ──
    [Theory]
    [InlineData("бала", "балаға")]
    [InlineData("көше", "көшеге")]
    [InlineData("кітап", "кітапқа")]       // back + voiceless → -қа
    [InlineData("мектеп", "мектепке")]     // front + voiceless → -ке (consonant harmony!)
    [InlineData("сөздік", "сөздікке")]
    public void Dative_form_is_correct(string lemma, string expected)
    {
        var forms = KazakhInflector.Inflect(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == "dat" && f.Form == expected);
    }

    // ── Locative ──
    [Theory]
    [InlineData("бала", "балада")]
    [InlineData("көше", "көшеде")]
    [InlineData("кітап", "кітапта")]
    [InlineData("сөздік", "сөздікте")]
    [InlineData("мектеп", "мектепте")]
    public void Locative_form_is_correct(string lemma, string expected)
    {
        var forms = KazakhInflector.Inflect(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == "loc" && f.Form == expected);
    }

    // ── Ablative ──
    [Theory]
    [InlineData("бала", "баладан")]
    [InlineData("кітап", "кітаптан")]
    [InlineData("адам", "адамнан")]   // nasal → -нан
    public void Ablative_form_is_correct(string lemma, string expected)
    {
        var forms = KazakhInflector.Inflect(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == "abl" && f.Form == expected);
    }

    // ── Possessives ──
    [Theory]
    [InlineData("бала", "poss1sg", "балам")]
    [InlineData("бала", "poss3sg", "баласы")]
    [InlineData("бала", "poss1pl", "баламыз")]
    [InlineData("кітап", "poss1sg", "кітапым")]
    [InlineData("кітап", "poss1sg+alt", "кітабым")]   // п→б alternation
    [InlineData("мектеп", "poss1sg+alt", "мектебім")]
    [InlineData("сөздік", "poss1sg+alt", "сөздігім")] // к→г alternation
    public void Possessive_form_is_correct(string lemma, string tag, string expected)
    {
        var forms = KazakhInflector.Inflect(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == tag && f.Form == expected);
    }

    // ── Plural + case combos ──
    [Theory]
    [InlineData("сөздік", "pl+abl", "сөздіктерден")]
    [InlineData("кітап", "pl+loc", "кітаптарда")]
    [InlineData("бала", "pl+dat", "балаларға")]
    [InlineData("ас", "pl+dat", "астарға")]
    public void Plural_case_combo_is_correct(string lemma, string tag, string expected)
    {
        var forms = KazakhInflector.Inflect(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == tag && f.Form == expected);
    }

    // ── Routing: when pos='verb', should produce verb forms, not nominal ones ──
    [Fact]
    public void Pos_verb_routes_to_verb_conjugation()
    {
        var forms = KazakhInflector.Inflect("жазу", "verb").ToList();
        Assert.Contains(forms, f => f.Tag == "v.past.3" && f.Form == "жазды");
        Assert.DoesNotContain(forms, f => f.Tag == "pl"); // not nominal
    }

    [Fact]
    public void Pos_empty_routes_to_nominal_inflection()
    {
        var forms = KazakhInflector.Inflect("бала", "").ToList();
        Assert.Contains(forms, f => f.Tag == "pl" && f.Form == "балалар");
    }
}
