using System.Linq;
using QType.COMMON.Morphology;
using Xunit;

namespace QType.Tests;

public class VerbTests
{
    // ── Imperative (2sg = bare stem) ──
    [Theory]
    [InlineData("жазу", "жаз")]
    [InlineData("келу", "кел")]
    [InlineData("бару", "бар")]
    [InlineData("көру", "көр")]
    public void Imperative_2sg_is_bare_stem(string lemma, string expected)
    {
        var forms = KazakhVerb.Conjugate(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == "v.imp.2sg" && f.Form == expected);
    }

    // ── Past simple (3rd person) ──
    [Theory]
    [InlineData("жазу", "жазды")]
    [InlineData("келу", "келді")]
    [InlineData("бару", "барды")]
    [InlineData("көру", "көрді")]
    [InlineData("білу", "білді")]
    [InlineData("айту", "айтты")]   // voiceless т → -ты
    public void Past_3sg_is_correct(string lemma, string expected)
    {
        var forms = KazakhVerb.Conjugate(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == "v.past.3" && f.Form == expected);
    }

    // ── Past 1pl: -қ vs -к (consonant harmony) ──
    [Theory]
    [InlineData("жазу", "жаздық")]
    [InlineData("бару", "бардық")]
    [InlineData("келу", "келдік")]   // front → -к, not -қ
    [InlineData("көру", "көрдік")]
    public void Past_1pl_harmonizes(string lemma, string expected)
    {
        var forms = KazakhVerb.Conjugate(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == "v.past.1pl" && f.Form == expected);
    }

    // ── Present-future (3rd person) ──
    [Theory]
    [InlineData("жазу", "жазады")]
    [InlineData("бару", "барады")]
    [InlineData("келу", "келеді")]
    [InlineData("білу", "біледі")]
    public void Pres_3sg_is_correct(string lemma, string expected)
    {
        var forms = KazakhVerb.Conjugate(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == "v.pres.3" && f.Form == expected);
    }

    // ── Future-intent: -мақ/-бақ/-пақ sandhi ──
    [Theory]
    [InlineData("жазу", "жазбақ")]    // з → -бақ
    [InlineData("келу", "келмек")]    // л → -мек (sonorant)
    [InlineData("бару", "бармақ")]    // р → -мақ (sonorant)
    [InlineData("көру", "көрмек")]    // р + front → -мек
    [InlineData("білу", "білмек")]    // л + front → -мек
    [InlineData("айту", "айтпақ")]    // т → -пақ (voiceless)
    public void Future_intent_sandhi_correct(string lemma, string expected)
    {
        var forms = KazakhVerb.Conjugate(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == "v.fut.3" && f.Form == expected);
    }

    // ── Negative imperative: -ма/-ба/-па sandhi ──
    [Theory]
    [InlineData("жазу", "жазба")]
    [InlineData("бару", "барма")]   // р → -ма (was bug: -ба)
    [InlineData("көру", "көрме")]
    [InlineData("білу", "білме")]
    [InlineData("айту", "айтпа")]
    public void Negative_imperative_sandhi_correct(string lemma, string expected)
    {
        var forms = KazakhVerb.Conjugate(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == "v.neg.imp.2sg" && f.Form == expected);
    }

    // ── Participle -ған/-ген/-қан/-кен ──
    [Theory]
    [InlineData("жазу", "жазған")]
    [InlineData("келу", "келген")]
    [InlineData("айту", "айтқан")]    // voiceless → -қан
    public void Participle_is_correct(string lemma, string expected)
    {
        var forms = KazakhVerb.Conjugate(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == "v.part" && f.Form == expected);
    }

    // ── Perfect (participle + copula) ──
    [Theory]
    [InlineData("білу", "білгенмін")]
    [InlineData("бару", "барғанмын")]
    [InlineData("келу", "келгенсің")]
    public void Perfect_form_is_correct(string lemma, string expected)
    {
        var forms = KazakhVerb.Conjugate(lemma).ToList();
        Assert.Contains(forms, f => f.Form == expected);
    }

    // ── Converbs ──
    [Theory]
    [InlineData("жазу", "жазып")]
    [InlineData("келу", "келіп")]
    public void Converb_ip_is_correct(string lemma, string expected)
    {
        var forms = KazakhVerb.Conjugate(lemma).ToList();
        Assert.Contains(forms, f => f.Tag == "v.cvb.ip" && f.Form == expected);
    }
}
