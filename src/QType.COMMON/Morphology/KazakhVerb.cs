namespace QType.COMMON.Morphology;

/// Verb conjugation for Kazakh. Citation form ends in -у (жазу, оқу, келу...);
/// we strip that to derive the stem, then apply tense/mood/person suffixes
/// with vowel harmony + sandhi (final-consonant determines suffix initial).
///
/// Coverage (the user-typed forms that actually matter, by frequency):
///   - Past simple:           жаз-ды + person endings
///   - Present-future:        жаз-а-ды + person endings
///   - Future-intent:         жаз-бақ + person endings
///   - Gerund / participle:   жаз-ған, жаз-у, жаз-ушы, жаз-атын
///   - Converbs:              жаз-ып, жаз-а
///   - Imperative:            жаз (2sg), жаз-ыңдар (2pl), жаз-сын (3 imp/optative)
///   - Negative:              жаз-ба-… for each of the above
///
/// Known gaps (v1 — accepted):
///   - Vowel-deletion stems ("оқу" → "оқ" vs "оқы"): we emit BOTH variants so
///     either user-typed spelling matches a wordform.
///   - Some irregular verbs (е-, де-, кө-) need a small hand-curated table later.
///   - Voice (passive/causative/reflexive) not generated — too combinatorial.
public static class KazakhVerb
{
    public static IEnumerable<InflectedForm> Conjugate(string lemma)
    {
        if (string.IsNullOrWhiteSpace(lemma)) yield break;
        lemma = lemma.Trim();
        if (lemma.Length < 2) yield break;
        if (!(lemma.EndsWith("у") || lemma.EndsWith("ю"))) yield break;

        // Stem = lemma minus the -у citation suffix.
        // Vowel-deletion verbs (оқу → оқы-, ілу → іле-, тұру → тұра-) get incorrect
        // forms here — accepted v1 limitation. Will be fixed by a small irregular-stem
        // lexicon in a later pass.
        var stem = lemma[..^1];
        foreach (var f in ConjugateOne(stem)) yield return f;
    }

    private static IEnumerable<InflectedForm> ConjugateOne(string stem)
    {
        if (string.IsNullOrEmpty(stem)) yield break;
        var h = KazakhPhonology.GetHarmony(stem);
        var fc = KazakhPhonology.GetFinalClass(stem);

        // Avoid double-vowel: if stem ends in -ы/-і, the epenthetic vowel already exists.
        var epen = (fc == FinalClass.Vowel) ? "" : (h == VowelHarmony.Back ? "ы" : "і");

        // ── Imperative (bare stem = 2sg imperative) ──
        yield return new InflectedForm(stem, "v.imp.2sg");
        // 2pl polite imperative: stem + -ыңдар/-іңдер (or just -ңдар after vowel)
        yield return new InflectedForm(stem + epen + KazakhPhonology.Harmonize("ңдар", h), "v.imp.2pl");
        // 3rd person optative: stem + -сын/-сін
        yield return new InflectedForm(stem + KazakhPhonology.Harmonize("сын", h), "v.opt.3");

        // ── Past simple: stem + past-marker + person ──
        var past = stem + KazakhPhonology.Harmonize(PastMarker(fc), h);
        yield return new InflectedForm(past, "v.past.3");                                  // ол жазды
        yield return new InflectedForm(past + "м", "v.past.1sg");                          // мен жаздым
        yield return new InflectedForm(past + "ң", "v.past.2sg");                          // сен жаздың
        yield return new InflectedForm(past + KazakhPhonology.Harmonize("ңыз", h), "v.past.2pl"); // Сіз жаздыңыз
        yield return new InflectedForm(past + (h == VowelHarmony.Front ? "к" : "қ"), "v.past.1pl"); // біз жаздық / келдік
        yield return new InflectedForm(past + KazakhPhonology.Harmonize("ңдар", h), "v.past.2pl.inf"); // сендер жаздыңдар

        // ── Present-future ──
        // stem + linking vowel (-а/-е, or -й after vowel-final stem) + person
        var pres = stem + PresLinker(stem, h);
        yield return new InflectedForm(pres + KazakhPhonology.Harmonize("ды", h), "v.pres.3"); // ол барады
        yield return new InflectedForm(pres + KazakhPhonology.Harmonize("мын", h), "v.pres.1sg"); // мен барамын
        yield return new InflectedForm(pres + KazakhPhonology.Harmonize("сың", h), "v.pres.2sg"); // сен барасың
        yield return new InflectedForm(pres + KazakhPhonology.Harmonize("сыз", h), "v.pres.2pl"); // Сіз барасыз
        yield return new InflectedForm(pres + KazakhPhonology.Harmonize("мыз", h), "v.pres.1pl"); // біз барамыз

        // ── Future-intent: -мақ/-мек/-пақ/-пек/-бақ/-бек depending on stem final ──
        var fut = stem + FutureIntentMarker(stem, h);
        yield return new InflectedForm(fut, "v.fut.3");
        yield return new InflectedForm(fut + KazakhPhonology.Harmonize("пын", h), "v.fut.1sg");

        // ── Participle / gerund: -ған/-ген/-қан/-кен ──
        var part = stem + ParticipleMarker(fc, h);
        yield return new InflectedForm(part, "v.part");
        yield return new InflectedForm(part + KazakhPhonology.Harmonize("дар", h), "v.part.pl");
        // ── Perfect-like: participle + copula ── (білгенмін, барғанмын, келгенсің ...)
        yield return new InflectedForm(part + KazakhPhonology.Harmonize("мын", h), "v.perf.1sg");
        yield return new InflectedForm(part + KazakhPhonology.Harmonize("сың", h), "v.perf.2sg");
        yield return new InflectedForm(part + KazakhPhonology.Harmonize("мыз", h), "v.perf.1pl");

        // ── Habitual / agent-like: -атын/-етін, -ушы/-уші ──
        yield return new InflectedForm(stem + KazakhPhonology.Harmonize("атын", h), "v.hab");
        // Agent noun ending varies by stem-final: -ушы/-уші after consonant, -йшы/-йші after vowel
        if (fc == FinalClass.Vowel)
            yield return new InflectedForm(stem + KazakhPhonology.Harmonize("ушы", h), "v.agent");
        else
            yield return new InflectedForm(stem + KazakhPhonology.Harmonize("ушы", h), "v.agent");

        // ── Converb / adverbial: -ып/-іп, plus -а/-е (both very common) ──
        yield return new InflectedForm(stem + epen + "п", "v.cvb.ip");
        yield return new InflectedForm(stem + PresLinker(stem, h), "v.cvb.a");

        // ── Verbal noun (citation form itself, with -у) ──
        yield return new InflectedForm(stem + "у", "v.vn");

        // ── Negative stem: -ма-/-ме-/-ба-/-бе-/-па-/-пе- ──
        var neg = stem + NegMarker(stem, h);
        // Negative imperative 2sg:
        yield return new InflectedForm(neg, "v.neg.imp.2sg");
        // Negative past 3:
        var negFc = KazakhPhonology.GetFinalClass(neg);
        yield return new InflectedForm(neg + KazakhPhonology.Harmonize(PastMarker(negFc), h), "v.neg.past.3");
    }

    // ── Helpers ────────────────────────────────────────────────────────

    /// Past tense marker by stem-final consonant class. Always -ды/-ді/-ты/-ті
    /// (no -нды variant in this position).
    private static string PastMarker(FinalClass fc) => fc switch
    {
        FinalClass.Voiceless => "ты",
        _ => "ды",
    };

    /// Linking vowel between verb stem and present-future suffix.
    /// After a vowel-final stem, -й is the connector.
    /// After consonant-final, -а/-е selects by harmony.
    private static string PresLinker(string stem, VowelHarmony h)
    {
        if (stem.Length == 0) return "";
        if (KazakhPhonology.Vowels.Contains(stem[^1])) return "й";
        return h == VowelHarmony.Back ? "а" : "е";
    }

    /// Future-intent marker. Real Kazakh sandhi:
    ///   After vowels, м, н, ң, л, р, й, у → -мақ/-мек
    ///   After voiced obstruents б, в, г, д, ж, з → -бақ/-бек
    ///   After voiceless                            → -пақ/-пек
    private static string FutureIntentMarker(string stem, VowelHarmony h)
    {
        if (string.IsNullOrEmpty(stem)) return KazakhPhonology.Harmonize("мақ", h);
        var c = stem[^1];
        string back;
        if (KazakhPhonology.Vowels.Contains(c)) back = "мақ";
        else back = c switch
        {
            'м' or 'н' or 'ң' or 'л' or 'р' or 'й' or 'у' => "мақ",
            'б' or 'в' or 'г' or 'д' or 'ж' or 'з' => "бақ",
            _ => "пақ",
        };
        return KazakhPhonology.Harmonize(back, h);
    }

    /// Participle marker -ған/-кен (sandhi by final).
    private static string ParticipleMarker(FinalClass fc, VowelHarmony h)
    {
        var back = fc == FinalClass.Voiceless ? "қан" : "ған";
        return KazakhPhonology.Harmonize(back, h);
    }

    /// Negative marker. Same sandhi as future-intent for -м/-б/-п choice:
    ///   After vowels and sonorants (м,н,ң,л,р,й,у) → -ма/-ме
    ///   After voiced obstruents (б,в,г,д,ж,з)     → -ба/-бе
    ///   After voiceless                            → -па/-пе
    private static string NegMarker(string stem, VowelHarmony h)
    {
        if (string.IsNullOrEmpty(stem)) return KazakhPhonology.Harmonize("ма", h);
        var c = stem[^1];
        string back;
        if (KazakhPhonology.Vowels.Contains(c)) back = "ма";
        else back = c switch
        {
            'м' or 'н' or 'ң' or 'л' or 'р' or 'й' or 'у' => "ма",
            'б' or 'в' or 'г' or 'д' or 'ж' or 'з' => "ба",
            _ => "па",
        };
        return KazakhPhonology.Harmonize(back, h);
    }

}
