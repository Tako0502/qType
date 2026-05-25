using System.Text;
using System.Text.RegularExpressions;

namespace QType.Web.Services;

/// Server-side allow-list HTML sanitizer for dictionary entries.
///
/// Defense-in-depth: even though the source data is our own MySQL database, we
/// don't trust that no malicious entry slipped in via a future contribution
/// flow. Strip everything except a small allow-list of formatting tags.
public static class HtmlSanitizer
{
    // Tags we explicitly keep. Anything else is removed (tag dropped, inner text kept).
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "i", "em", "b", "strong", "u", "sub", "sup", "small"
    };

    // Tags whose entire contents (not just the tag) should be removed.
    private static readonly HashSet<string> DangerousTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "iframe", "object", "embed", "frame", "frameset",
        "applet", "form", "input", "button", "textarea", "select", "link", "meta"
    };

    private static readonly Regex TagRegex = new(@"<(?<close>/)?(?<name>[a-zA-Z][a-zA-Z0-9]*)\b[^>]*>",
        RegexOptions.Compiled);
    private static readonly Regex DangerousBlock = new(
        @"<(script|style|iframe|object|embed|frame|frameset|applet|form|input|button|textarea|select|link|meta)\b[^>]*>.*?</\1>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex SelfClosingDangerous = new(
        @"<(script|style|iframe|object|embed|frame|frameset|applet|form|input|button|textarea|select|link|meta)\b[^>]*/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Sanitize(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        // 1. Strip dangerous tag+content blocks entirely (e.g. <script>...</script>)
        var s = DangerousBlock.Replace(html, "");
        s = SelfClosingDangerous.Replace(s, "");

        // 2. Walk every remaining tag: keep allow-listed ones (stripped of attributes),
        //    drop the rest while preserving inner content.
        var sb = new StringBuilder(s.Length);
        var pos = 0;
        foreach (Match m in TagRegex.Matches(s))
        {
            if (m.Index > pos) sb.Append(s, pos, m.Index - pos);
            var name = m.Groups["name"].Value;
            var isClose = m.Groups["close"].Success;
            if (AllowedTags.Contains(name))
            {
                // Re-emit as a clean tag without any attributes — kills onerror, onclick, etc.
                sb.Append(isClose ? $"</{name.ToLowerInvariant()}>" : $"<{name.ToLowerInvariant()}>");
            }
            // else: drop the tag itself, keep nothing of it
            pos = m.Index + m.Length;
        }
        if (pos < s.Length) sb.Append(s, pos, s.Length - pos);

        // 3. Strip remaining stray angle brackets / event-handler-looking strings just in case
        var result = sb.ToString();
        return result;
    }
}
