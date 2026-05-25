using QType.COMMON.Morphology;
using Xunit;

namespace QType.Tests;

public class PhonologyTests
{
    [Theory]
    // Back-vowel words
    [InlineData("бала", VowelHarmony.Back)]
    [InlineData("қала", VowelHarmony.Back)]
    [InlineData("оқу", VowelHarmony.Back)]
    [InlineData("сұлу", VowelHarmony.Back)]
    [InlineData("ас", VowelHarmony.Back)]
    [InlineData("кітап", VowelHarmony.Back)]
    [InlineData("жазу", VowelHarmony.Back)]
    // Front-vowel words
    [InlineData("көше", VowelHarmony.Front)]
    [InlineData("мектеп", VowelHarmony.Front)]
    [InlineData("сөздік", VowelHarmony.Front)]
    [InlineData("білу", VowelHarmony.Front)]
    [InlineData("көру", VowelHarmony.Front)]
    [InlineData("үй", VowelHarmony.Front)]
    [InlineData("әке", VowelHarmony.Front)]
    public void GetHarmony_returns_expected(string word, VowelHarmony expected)
    {
        Assert.Equal(expected, KazakhPhonology.GetHarmony(word));
    }

    [Theory]
    [InlineData("бала", FinalClass.Vowel)]
    [InlineData("оқу", FinalClass.Vowel)]
    [InlineData("кітап", FinalClass.Voiceless)]
    [InlineData("мектеп", FinalClass.Voiceless)]
    [InlineData("ас", FinalClass.Voiceless)]
    [InlineData("сөздік", FinalClass.Voiceless)]
    [InlineData("ұл", FinalClass.Liquid)]
    [InlineData("ел", FinalClass.Liquid)]
    [InlineData("адам", FinalClass.Nasal)]
    [InlineData("сөз", FinalClass.Liquid)]
    [InlineData("көз", FinalClass.Liquid)]
    public void GetFinalClass_returns_expected(string word, FinalClass expected)
    {
        Assert.Equal(expected, KazakhPhonology.GetFinalClass(word));
    }

    [Theory]
    [InlineData("ға", VowelHarmony.Back, "ға")]
    [InlineData("ға", VowelHarmony.Front, "ге")]
    [InlineData("қа", VowelHarmony.Front, "ке")]      // ← consonant harmony
    [InlineData("ғы", VowelHarmony.Front, "гі")]      // ← consonant harmony
    [InlineData("ның", VowelHarmony.Front, "нің")]
    [InlineData("лар", VowelHarmony.Front, "лер")]
    [InlineData("мыз", VowelHarmony.Front, "міз")]
    public void Harmonize_swaps_vowels_and_consonants(string back, VowelHarmony h, string expected)
    {
        Assert.Equal(expected, KazakhPhonology.Harmonize(back, h));
    }
}
