using System.Linq;
using QType.COMMON.Morphology;
using Xunit;

namespace QType.Tests;

public class LemmatizerTests
{
    [Theory]
    [InlineData("балалар", "бала")]
    [InlineData("сөздіктер", "сөздік")]
    [InlineData("мектепте", "мектеп")]
    [InlineData("балаға", "бала")]
    [InlineData("көшеден", "көше")]
    // Note: stripper does NOT reverse consonant alternation. "кітабым" → "кітабы"/"кітаб",
    // not "кітап". The б→п reversal is handled at qwordform generation time, not lookup.
    public void Stripper_yields_the_lemma_as_a_candidate(string form, string expectedStem)
    {
        var candidates = KazakhLemmatizer.Strip(form).ToList();
        // The lemmatizer over-generates; we check that the right stem is among the candidates
        Assert.Contains(expectedStem, candidates);
    }

    [Fact]
    public void Input_form_is_always_first_candidate()
    {
        var candidates = KazakhLemmatizer.Strip("балалар").ToList();
        Assert.Equal("балалар", candidates[0]);
    }

    [Fact]
    public void Empty_input_yields_nothing()
    {
        var candidates = KazakhLemmatizer.Strip("").ToList();
        Assert.Empty(candidates);
    }
}
