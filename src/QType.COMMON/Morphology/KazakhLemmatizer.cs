namespace QType.COMMON.Morphology;

/// Reduces an inflected form back to candidate lemmas by stripping known Kazakh suffixes.
/// Strategy: try every suffix in length-descending order; for each match,
/// yield the stem. Caller (DictionaryService) validates against qlemma.
///
/// We deliberately over-generate candidates and let the lemma table filter — false positives
/// at this stage just mean a few extra DB lookups, false negatives mean a missed correction.
public static class KazakhLemmatizer
{
    // Ordered by length descending so longer suffixes match first
    // (otherwise "балалар" would strip "р" → "балала" before "лар" → "бала").
    private static readonly string[] Suffixes = new[]
    {
        // pl+poss+case combos (most aggressive)
        "ларымыз", "лерiмiз", "ларыңыз", "леріңіз", "ларында", "леріндe",
        // poss + case
        "ымызда", "імізде", "ыңызда", "іңізде", "ымызды", "імізді",
        // plural + case
        "ларда", "лерде", "тарда", "терде", "дарда", "дерде",
        "ларға", "лерге", "тарға", "терге", "дарға", "дерге",
        "ларды", "лерді", "тарды", "терді", "дарды", "дерді",
        "ларды", "лерді",
        // plural alone
        "лар", "лер", "дар", "дер", "тар", "тер",
        // possessive 1pl, 2pl
        "ымыз", "іміз", "мыз", "міз",
        "ыңыз", "іңіз", "ңыз", "ңіз",
        // case markers
        "ның", "нің", "дың", "дің", "тың", "тің",
        "дан", "ден", "тан", "тен", "нан", "нен",
        "мен", "бен", "пен",
        "ға", "ге", "қа", "ке",
        "да", "де", "та", "те",
        "ны", "ні", "ды", "ді", "ты", "ті",
        // possessive 1sg, 2sg, 3sg
        "ым", "ім", "ың", "ің", "сы", "сі",
        "м", "ң", "ы", "і",
    };

    /// Returns candidate stems by removing each matching suffix.
    /// Includes the original form as the first candidate.
    public static IEnumerable<string> Strip(string form)
    {
        if (string.IsNullOrEmpty(form)) yield break;
        yield return form;

        var seen = new HashSet<string> { form };
        foreach (var suf in Suffixes)
        {
            if (form.Length <= suf.Length + 1) continue; // need at least a 2-char stem
            if (form.EndsWith(suf, StringComparison.Ordinal))
            {
                var stem = form[..^suf.Length];
                if (seen.Add(stem)) yield return stem;
            }
        }
    }
}
