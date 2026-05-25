using QType.COMMON.Morphology;
using Xunit;

namespace QType.Tests;

public class PosTests
{
    [Theory]
    // Verbs (citation form ends in -у)
    [InlineData("жазу", Pos.Verb)]
    [InlineData("оқу", Pos.Verb)]
    [InlineData("келу", Pos.Verb)]
    [InlineData("көру", Pos.Verb)]
    [InlineData("істеу", Pos.Verb)]
    // Nouns
    [InlineData("бала", Pos.Noun)]
    [InlineData("кітап", Pos.Noun)]
    [InlineData("мектеп", Pos.Noun)]
    [InlineData("адам", Pos.Noun)]
    // Abstract nouns (-лық/-лік)
    [InlineData("байлық", Pos.Noun)]
    [InlineData("кемелдік", Pos.Noun)]
    // Agent nouns (-шы/-ші)
    [InlineData("оқушы", Pos.Noun)]
    [InlineData("жазушы", Pos.Noun)]
    // Adjectives (-сыз/-сіз)
    [InlineData("үйсіз", Pos.Adjective)]
    [InlineData("ақысыз", Pos.Adjective)]
    // Adverbs (-дай/-дей, -ша/-ше)
    [InlineData("алмадай", Pos.Adverb)]
    [InlineData("қазақша", Pos.Adverb)]
    public void Classify_picks_expected_pos(string word, Pos expected)
    {
        var guess = KazakhPos.Classify(word);
        Assert.Equal(expected, guess.Pos);
    }

    [Fact]
    public void Empty_string_returns_unknown()
    {
        Assert.Equal(Pos.Unknown, KazakhPos.Classify("").Pos);
    }

    [Fact]
    public void Non_cyrillic_returns_foreign()
    {
        Assert.Equal(Pos.Foreign, KazakhPos.Classify("hello").Pos);
    }

    [Fact]
    public void Short_u_word_is_noun_not_verb()
    {
        // "су" (water) ends in -у but is a noun, not a verb. Length=2.
        Assert.NotEqual(Pos.Verb, KazakhPos.Classify("су").Pos);
    }

    [Fact]
    public void Three_letter_verbs_recognized()
    {
        // "оқу" is 3 chars and IS a verb. Was a bug.
        Assert.Equal(Pos.Verb, KazakhPos.Classify("оқу").Pos);
    }
}
