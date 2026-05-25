namespace QType.COMMON.Morphology;

/// Coarse part-of-speech tag. Kept small because (a) we only use it to gate
/// inflection rules and (b) the dictionary corpus doesn't carry enough signal
/// to reliably distinguish finer categories.
public enum Pos
{
    Unknown,
    Noun,
    Verb,
    Adjective,
    Adverb,
    Pronoun,
    Numeral,
    Conjunction,
    Interjection,
    Proper,    // proper noun (city, name, brand)
    Foreign,   // transliterated / loanword that doesn't follow KK patterns
}

public record PosGuess(Pos Pos, double Confidence, string Reason);

/// Rule-based POS classifier for Kazakh lemmas in citation form.
/// We trust HIGH confidence (>= 0.8) and apply inflection accordingly;
/// LOW confidence leaves the lemma alone (defaults to nominal handling).
public static class KazakhPos
{
    private static readonly string[] CyrlKk = { "ә","і","ң","ғ","ү","ұ","қ","ө","һ" };
    private static readonly char[] KazakhSpecific = { 'ә','і','ң','ғ','ү','ұ','қ','ө','һ' };

    public static PosGuess Classify(string lemma)
    {
        if (string.IsNullOrWhiteSpace(lemma))
            return new PosGuess(Pos.Unknown, 0, "empty");

        var w = lemma.Trim().ToLowerInvariant();
        var len = w.Length;

        // ── 1. Non-Cyrillic or too-short → Foreign
        if (!ContainsCyrillic(w))
            return new PosGuess(Pos.Foreign, 0.95, "no Cyrillic letters");

        // ── 2. Multi-word → Phrase (handled separately by qlemma.isPhrase)
        if (w.Contains(' '))
            return new PosGuess(Pos.Unknown, 0.3, "multi-word phrase");

        // ── 3. Verb signal: ends in -у/-ю after a consonant, length ≥ 3.
        // Citation form of all Kazakh verbs is X+у (жазу, оқу, беру, келу, көру, болу...).
        // Short 2-char -у words like "су, бу, ту" are nouns — drop those.
        if (len >= 3 && (w.EndsWith("у") || w.EndsWith("ю")))
        {
            var precedingVowel = len >= 2 && IsVowel(w[len - 2]);
            if (precedingVowel)
            {
                // -ау/-оу/-еу/-ұу after vowel can be either noun ("тау, жау, бау, сабау")
                // or verb ("боса+ту? no, that's т+у"). Medium confidence.
                return new PosGuess(Pos.Verb, 0.6, "ends in -у after vowel (ambiguous)");
            }
            return new PosGuess(Pos.Verb, 0.9, "ends in -у/-ю after consonant (citation infinitive)");
        }

        // ── 4. Adjective via "without" suffix -сыз/-сіз
        if (len >= 5 && (w.EndsWith("сыз") || w.EndsWith("сіз")))
            return new PosGuess(Pos.Adjective, 0.92, "ends in -сыз/-сіз (privative)");

        // ── 5. Abstract noun via -лық/-лік/-дық/-дік/-тық/-тік
        if (len >= 5 && (w.EndsWith("лық") || w.EndsWith("лік") || w.EndsWith("дық")
                       || w.EndsWith("дік") || w.EndsWith("тық") || w.EndsWith("тік")))
            return new PosGuess(Pos.Noun, 0.9, "ends in -лық/-лік (abstract noun)");

        // ── 6. Adjective via -ды/-ді/-ты/-ті/-лы/-лі (with-suffix)
        // Only when length >= 5 and after a noun-like stem — risky because case markers
        // share these endings. Require length >= 5 to filter out inflected forms.
        if (len >= 5 && (w.EndsWith("ды") || w.EndsWith("ді") || w.EndsWith("ты")
                       || w.EndsWith("ті") || w.EndsWith("лы") || w.EndsWith("лі")))
            return new PosGuess(Pos.Adjective, 0.55, "ends in -ды/-ді/-ты/-ті/-лы/-лі");

        // ── 7. Adverb via -дай/-дей/-тай/-тей/-ша/-ше
        if (len >= 5 && (w.EndsWith("дай") || w.EndsWith("дей") || w.EndsWith("тай")
                       || w.EndsWith("тей")))
            return new PosGuess(Pos.Adverb, 0.85, "ends in -дай/-дей (similative)");
        if (len >= 4 && (w.EndsWith("ша") || w.EndsWith("ше")))
            return new PosGuess(Pos.Adverb, 0.75, "ends in -ша/-ше (manner)");

        // ── 8. Agent / profession noun: -шы/-ші
        if (len >= 5 && (w.EndsWith("шы") || w.EndsWith("ші")))
            return new PosGuess(Pos.Noun, 0.85, "ends in -шы/-ші (agent noun)");

        // ── 9. Default: noun. Most Kazakh dictionary entries are nouns by frequency.
        // We DON'T flag as Foreign based on absence of Kazakh-specific letters —
        // many core Kazakh words ("адам", "ас", "бала", "мектеп", "жаман") use only
        // shared-Cyrillic letters and would be incorrectly tagged.
        return new PosGuess(Pos.Noun, 0.5, "default (nominal)");
    }

    public static string PosToColumn(Pos pos) => pos switch
    {
        Pos.Noun => "noun",
        Pos.Verb => "verb",
        Pos.Adjective => "adj",
        Pos.Adverb => "adv",
        Pos.Pronoun => "pron",
        Pos.Numeral => "num",
        Pos.Conjunction => "conj",
        Pos.Interjection => "intj",
        Pos.Proper => "propn",
        Pos.Foreign => "x",
        _ => ""
    };

    private static bool ContainsCyrillic(string s)
    {
        foreach (var c in s)
            if (c >= 'Ѐ' && c <= 'ӿ') return true;
        return false;
    }

    private static bool IsVowel(char c) =>
        KazakhPhonology.Vowels.Contains(c);
}
