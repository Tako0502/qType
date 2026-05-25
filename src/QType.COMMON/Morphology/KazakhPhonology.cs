namespace QType.COMMON.Morphology;

public enum VowelHarmony { Back, Front }

public enum FinalClass
{
    Vowel,         // а, ә, е, и, і, о, ө, ы, ұ, ү, у (treated as vowel)
    Liquid,        // л, р, й, з, ж  (sonorant non-nasal)
    Nasal,         // м, н, ң
    Voiced,        // б, в, г, д
    Voiceless,     // п, ф, к, қ, с, т, ш, х, ц, ч, щ, һ
    Other
}

public static class KazakhPhonology
{
    public const string BackVowels = "аоұыя";
    public const string FrontVowels = "әеөүіэ";
    // у and и are harmonically neutral in modern Kazakh — they don't override
    // the harmony of the preceding vowel. Treated as vowels for phonology but
    // skipped when determining harmony.
    public const string NeutralVowels = "уию";
    public const string Vowels = BackVowels + FrontVowels + NeutralVowels;

    public static VowelHarmony GetHarmony(string lemma)
    {
        // Scan from the END for the rightmost NON-neutral vowel.
        // "білу" → і governs (front), не у.  "оқу" → о governs (back).
        for (var i = lemma.Length - 1; i >= 0; i--)
        {
            var c = lemma[i];
            if (FrontVowels.Contains(c)) return VowelHarmony.Front;
            if (BackVowels.Contains(c)) return VowelHarmony.Back;
            // skip neutral vowels and consonants
        }
        return VowelHarmony.Back;
    }

    public static FinalClass GetFinalClass(string lemma)
    {
        if (string.IsNullOrEmpty(lemma)) return FinalClass.Other;
        var c = lemma[^1];
        if (Vowels.Contains(c)) return FinalClass.Vowel;
        return c switch
        {
            'м' or 'н' or 'ң' => FinalClass.Nasal,
            'л' or 'р' or 'й' or 'з' or 'ж' => FinalClass.Liquid,
            'б' or 'в' or 'г' or 'д' => FinalClass.Voiced,
            'п' or 'ф' or 'к' or 'қ' or 'с' or 'т' or 'ш' or 'х' or 'ц' or 'ч' or 'щ' or 'һ' => FinalClass.Voiceless,
            _ => FinalClass.Other
        };
    }

    /// Apply vowel + consonant harmony to a back-vowel template string.
    /// Swaps:  а↔е, ы↔і, у↔ү, ұ↔ү, о↔ө, я↔е, ё↔ө,  қ↔к, ғ↔г
    public static string Harmonize(string back, VowelHarmony h)
    {
        if (h == VowelHarmony.Back) return back;
        var sb = new System.Text.StringBuilder(back.Length);
        foreach (var c in back)
        {
            sb.Append(c switch
            {
                'а' => 'е',
                'ы' => 'і',
                'у' => 'ү',
                'ұ' => 'ү',
                'о' => 'ө',
                'я' => 'е',
                'ё' => 'ө',
                'қ' => 'к',
                'ғ' => 'г',
                _ => c
            });
        }
        return sb.ToString();
    }
}
