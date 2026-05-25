namespace QType.COMMON.Morphology;

public record InflectedForm(string Form, string Tag);

/// Generates the most common ~20 inflected forms per Kazakh lemma.
/// Coverage: plural, possessive (1/2/3 sg+pl), case (gen/dat/acc/loc/abl/inst),
/// and the high-frequency plural+case combos (pl+loc, pl+dat, pl+abl).
///
/// Rule sources: standard Kazakh grammar (Бектұров, Мырзабекова).
/// Known limits (v1 — pragmatic 80% coverage):
///   - No consonant alternation before vowel-initial possessive suffixes
///     (п/к/қ/т → б/г/ғ/д), so "кітап" generates "кітапым" not "кітабым".
///     User-typed wrong-spelling still hits this list, which is actually useful.
///   - No verb conjugation (needs POS classification from Stage 1).
public static class KazakhInflector
{
    public static IEnumerable<InflectedForm> Inflect(string lemma)
    {
        if (string.IsNullOrWhiteSpace(lemma)) yield break;
        lemma = lemma.Trim();

        var h = KazakhPhonology.GetHarmony(lemma);
        var fc = KazakhPhonology.GetFinalClass(lemma);

        // ── Plural ──
        var pluralBack = PluralSuffixBack(fc);
        if (pluralBack != null)
        {
            yield return new InflectedForm(lemma + KazakhPhonology.Harmonize(pluralBack, h), "pl");
        }

        // ── Singular case ──
        foreach (var (suf, tag) in SingularCases(fc, h))
            yield return new InflectedForm(lemma + suf, tag);

        // ── Possessives (singular) ──
        // Voiceless stem + vowel-initial suffix → also emit alternated form
        // (п→б, к→г, қ→ғ). Lexical alternation is unpredictable per word, so
        // we store BOTH variants and let the dictionary match decide.
        foreach (var (suf, tag) in Possessives(fc, h))
        {
            yield return new InflectedForm(lemma + suf, tag);
            if (fc == FinalClass.Voiceless && suf.Length > 0 && IsVowel(suf[0]))
            {
                var altered = AlternateFinal(lemma);
                if (altered != null)
                    yield return new InflectedForm(altered + suf, tag + "+alt");
            }
        }

        // ── Plural + case (most common combos) ──
        if (pluralBack != null)
        {
            var pluralStem = lemma + KazakhPhonology.Harmonize(pluralBack, h);
            // Plural stems all end in "р" → Liquid. Apply singular-case rules with FC=Liquid.
            foreach (var (suf, tag) in SingularCases(FinalClass.Liquid, h))
            {
                if (tag is "loc" or "dat" or "abl" or "acc")
                    yield return new InflectedForm(pluralStem + suf, $"pl+{tag}");
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //                      Plural
    // After Vowel / Liquid → -лар    (vowels, л р й з ж)
    // After Nasal / Voiced → -дар    (м н ң, б в г д)
    // After Voiceless      → -тар    (п ф к қ с т ш х ц ч щ һ)
    // ──────────────────────────────────────────────────────────────────
    private static string PluralSuffixBack(FinalClass fc) => fc switch
    {
        FinalClass.Vowel or FinalClass.Liquid => "лар",
        FinalClass.Nasal or FinalClass.Voiced => "дар",
        FinalClass.Voiceless => "тар",
        _ => null
    };

    // ──────────────────────────────────────────────────────────────────
    //                      Singular case suffixes
    // ──────────────────────────────────────────────────────────────────
    private static IEnumerable<(string Suffix, string Tag)> SingularCases(FinalClass fc, VowelHarmony h)
    {
        // Genitive: V/L/N → -ның, Voiced → -дың, Voiceless → -тың
        var gen = fc switch
        {
            FinalClass.Vowel or FinalClass.Liquid or FinalClass.Nasal => "ның",
            FinalClass.Voiced => "дың",
            FinalClass.Voiceless => "тың",
            _ => null
        };
        if (gen != null) yield return (KazakhPhonology.Harmonize(gen, h), "gen");

        // Dative: Voiceless → -қа, else → -ға
        var dat = fc == FinalClass.Voiceless ? "қа" : "ға";
        yield return (KazakhPhonology.Harmonize(dat, h), "dat");

        // Accusative: Vowel → -ны, Voiceless → -ты, everything else → -ды
        // (sonorants and voiced obstruents both take -ды; only true vowel finals take -ны)
        var acc = fc switch
        {
            FinalClass.Vowel => "ны",
            FinalClass.Voiceless => "ты",
            _ => "ды"
        };
        yield return (KazakhPhonology.Harmonize(acc, h), "acc");

        // Locative: Voiceless → -та, else → -да
        var loc = fc == FinalClass.Voiceless ? "та" : "да";
        yield return (KazakhPhonology.Harmonize(loc, h), "loc");

        // Ablative: Voiceless → -тан, Nasal → -нан, else → -дан
        var abl = fc switch
        {
            FinalClass.Voiceless => "тан",
            FinalClass.Nasal => "нан",
            _ => "дан"
        };
        yield return (KazakhPhonology.Harmonize(abl, h), "abl");

        // Instrumental: Voiceless → -пен, Voiced → -бен, else → -мен
        // (no a↔e harmony — always -ен)
        var ins = fc switch
        {
            FinalClass.Voiceless => "пен",
            FinalClass.Voiced => "бен",
            _ => "мен"
        };
        yield return (ins, "ins");
    }

    private static bool IsVowel(char c) => KazakhPhonology.Vowels.Contains(c);

    /// Apply Kazakh stem-final voicing before vowel-initial suffixes:
    /// п → б, к → г, қ → ғ. Returns null if no alternation applies.
    private static string AlternateFinal(string lemma)
    {
        if (lemma.Length == 0) return null;
        var last = lemma[^1];
        var replacement = last switch
        {
            'п' => 'б',
            'к' => 'г',
            'қ' => 'ғ',
            _ => '\0'
        };
        if (replacement == '\0') return null;
        return lemma[..^1] + replacement;
    }

    // ──────────────────────────────────────────────────────────────────
    //                      Possessive suffixes
    //  After vowel:  -м  -ң  -сы  -мыз  -ңыз
    //  After cons:   -ым -ың -ы   -ымыз -ыңыз
    // ──────────────────────────────────────────────────────────────────
    private static IEnumerable<(string Suffix, string Tag)> Possessives(FinalClass fc, VowelHarmony h)
    {
        var afterVowel = fc == FinalClass.Vowel;

        yield return (afterVowel ? "м" : KazakhPhonology.Harmonize("ым", h), "poss1sg");
        yield return (afterVowel ? "ң" : KazakhPhonology.Harmonize("ың", h), "poss2sg");
        yield return (afterVowel ? KazakhPhonology.Harmonize("сы", h) : KazakhPhonology.Harmonize("ы", h), "poss3sg");
        yield return (afterVowel ? KazakhPhonology.Harmonize("мыз", h) : KazakhPhonology.Harmonize("ымыз", h), "poss1pl");
        yield return (afterVowel ? KazakhPhonology.Harmonize("ңыз", h) : KazakhPhonology.Harmonize("ыңыз", h), "poss2pl");
    }
}
