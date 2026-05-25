using System.Globalization;
using System.Text.RegularExpressions;

namespace QType.COMMON;

public static class TextNormalizer
{
    private static readonly Regex DialectPrefix = new(@"^\s*\([А-ЯҚҒҢӨҰҮІӘҺA-Z][А-ЯҚҒҢӨҰҮІӘҺA-Za-zа-яқғңөұүіәһ.\s,:]*\)", RegexOptions.Compiled);
    private static readonly Regex WhitespaceCollapse = new(@"\s+", RegexOptions.Compiled);

    public static string NormalizeHeadword(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var s = raw.Trim();
        s = WhitespaceCollapse.Replace(s, " ");
        return s.ToLowerInvariant();
    }

    public static IEnumerable<string> SplitVariants(string rawHeadword)
    {
        if (string.IsNullOrEmpty(rawHeadword)) yield break;
        var parts = rawHeadword.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            if (!string.IsNullOrWhiteSpace(p)) yield return p;
        }
    }

    public static bool IsPhrase(string headword)
    {
        return !string.IsNullOrEmpty(headword) && headword.Trim().Contains(' ');
    }

    public static bool LooksDialectal(string rawHtml)
    {
        return !string.IsNullOrEmpty(rawHtml) && DialectPrefix.IsMatch(rawHtml);
    }

    public static DateTime? ParseSqDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var formats = new[] { "d/M/yyyy H:m:s", "d/M/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm:ss" };
        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            return dt;
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var fallback) ? fallback : null;
    }
}
