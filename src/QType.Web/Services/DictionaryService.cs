using Dapper;
using Microsoft.Extensions.Caching.Memory;
using QType.COMMON;
using QType.COMMON.Morphology;
using QType.DBHelper;

namespace QType.Web.Services;

public record SpellCheckResult(
    string Word,
    string Normalized,
    bool Valid,
    string Via,              // "lemma" | "wordform" | "none"
    int LemmaId,
    string Lemma,            // canonical form (filled when Via != "none")
    string MatchedForm,      // the actual matched form (== normalized for lemma match, == form text for wordform match)
    string Tag,              // morphological tag if matched via wordform
    int HeadwordHits,
    bool IsPhrase
);

public record SuggestionItem(string Form, string Lemma, int LemmaId, int Distance, string Source, int Frequency);

public record SuggestResult(string Word, string Normalized, List<SuggestionItem> Suggestions);

public class EntryHit
{
    public int EntryId { get; set; }
    public int SourceId { get; set; }
    public string SourceTitle { get; set; }
    public string RawHtml { get; set; }
    public string EntryDate { get; set; }
}

public record DefineResult(
    string Word,
    string Normalized,
    int? LemmaId,
    string Lemma,
    bool IsPhrase,
    bool IsDialectal,
    string MatchedVia,       // "lemma" | "wordform" | "none"
    string Tag,
    List<EntryHit> Entries
);

public class DictionaryService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public DictionaryService(IMemoryCache cache)
    {
        _cache = cache;
    }

    // ─── Spellcheck ───────────────────────────────────────────────────
    public async Task<SpellCheckResult> SpellCheckAsync(string word)
    {
        var normalized = TextNormalizer.NormalizeHeadword(word);
        if (string.IsNullOrEmpty(normalized))
            return new SpellCheckResult(word, "", false, "none", 0, "", "", "", 0, false);

        var cacheKey = $"sc:{normalized}";
        if (_cache.TryGetValue(cacheKey, out SpellCheckResult cached))
            return cached with { Word = word };

        await using var conn = Utilities.GetOpenMySqlConnection();

        // Try exact lemma first
        var lemma = await conn.QueryFirstOrDefaultAsync<(int Id, string Text, byte IsPhrase)?>(
            "SELECT id, text, isPhrase FROM qlemma WHERE text = @t LIMIT 1",
            new { t = normalized });

        var hwCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM qheadword WHERE textNormalized = @t",
            new { t = normalized });

        if (lemma.HasValue)
        {
            var r1 = new SpellCheckResult(
                Word: word, Normalized: normalized, Valid: true, Via: "lemma",
                LemmaId: lemma.Value.Id, Lemma: lemma.Value.Text,
                MatchedForm: normalized, Tag: "lemma",
                HeadwordHits: hwCount, IsPhrase: lemma.Value.IsPhrase == 1
            );
            _cache.Set(cacheKey, r1, CacheTtl);
            return r1;
        }

        // Try inflected form
        var form = await conn.QueryFirstOrDefaultAsync<(int LemmaId, string Form, string Features)?>(
            @"SELECT wf.lemmaId AS LemmaId, wf.form AS Form, wf.features AS Features
              FROM qwordform wf
              WHERE wf.form = @t
              LIMIT 1",
            new { t = normalized });

        if (form.HasValue)
        {
            var parent = await conn.QueryFirstOrDefaultAsync<(string Text, byte IsPhrase)?>(
                "SELECT text, isPhrase FROM qlemma WHERE id = @id",
                new { id = form.Value.LemmaId });
            var tag = ExtractTag(form.Value.Features);
            var r2 = new SpellCheckResult(
                Word: word, Normalized: normalized, Valid: true, Via: "wordform",
                LemmaId: form.Value.LemmaId, Lemma: parent?.Text ?? "",
                MatchedForm: normalized, Tag: tag,
                HeadwordHits: hwCount, IsPhrase: parent?.IsPhrase == 1
            );
            _cache.Set(cacheKey, r2, CacheTtl);
            return r2;
        }

        var r3 = new SpellCheckResult(
            Word: word, Normalized: normalized, Valid: false, Via: "none",
            LemmaId: 0, Lemma: "", MatchedForm: "", Tag: "",
            HeadwordHits: hwCount, IsPhrase: false
        );
        _cache.Set(cacheKey, r3, CacheTtl);
        return r3;
    }

    // ─── Suggest ──────────────────────────────────────────────────────
    public async Task<SuggestResult> SuggestAsync(string word, int k)
    {
        var normalized = TextNormalizer.NormalizeHeadword(word);
        if (string.IsNullOrEmpty(normalized) || k <= 0)
            return new SuggestResult(word, normalized, new List<SuggestionItem>());
        k = Math.Min(k, 25);

        var prefixLen = normalized.Length >= 3 ? 2 : 1;
        var prefix = normalized[..prefixLen] + "%";

        await using var conn = Utilities.GetOpenMySqlConnection();

        // Pool 1: lemma candidates by prefix — order by frequency DESC so the most-used lemmas
        // are always considered first, even if the LIMIT cuts off the long tail.
        var lemmaPool = (await conn.QueryAsync<(int Id, string Text, int Freq)>(
            @"SELECT l.id, l.text, COALESCE(MAX(f.count), 0) AS freq
              FROM qlemma l
              LEFT JOIN qfrequency f ON f.form = l.text
              WHERE l.text LIKE @prefix AND l.isPhrase = 0
              GROUP BY l.id, l.text
              ORDER BY freq DESC, CHAR_LENGTH(l.text) ASC
              LIMIT 500",
            new { prefix })).ToList();

        // Pool 2: wordform candidates by prefix
        var formPool = (await conn.QueryAsync<(int LemmaId, string Form, int Freq)>(
            @"SELECT wf.lemmaId, wf.form, COALESCE(MAX(f.count), 0) AS freq
              FROM qwordform wf
              LEFT JOIN qfrequency f ON f.form = wf.form
              WHERE wf.form LIKE @prefix
              GROUP BY wf.lemmaId, wf.form
              ORDER BY freq DESC, CHAR_LENGTH(wf.form) ASC
              LIMIT 500",
            new { prefix })).ToList();

        // Merge, score, dedup by lemmaId (keep the form closest to user input)
        var byLemma = new Dictionary<int, SuggestionItem>();

        foreach (var (id, text, freq) in lemmaPool)
        {
            var d = Levenshtein.Distance(normalized, text);
            if (!byLemma.TryGetValue(id, out var cur) || ShouldReplace(cur, d, freq))
                byLemma[id] = new SuggestionItem(text, text, id, d, "lemma", freq);
        }

        if (formPool.Count > 0)
        {
            // Look up parent lemmas in bulk
            var lemmaIds = formPool.Select(f => f.LemmaId).Distinct().ToList();
            var parentLemmas = (await conn.QueryAsync<(int Id, string Text)>(
                "SELECT id, text FROM qlemma WHERE id IN @ids",
                new { ids = lemmaIds })).ToDictionary(x => x.Id, x => x.Text);

            foreach (var (lemmaId, form, freq) in formPool)
            {
                if (!parentLemmas.TryGetValue(lemmaId, out var lemmaText)) continue;
                var d = Levenshtein.Distance(normalized, form);
                if (!byLemma.TryGetValue(lemmaId, out var cur) || ShouldReplace(cur, d, freq))
                    byLemma[lemmaId] = new SuggestionItem(form, lemmaText, lemmaId, d, "form", freq);
            }
        }

        // Backstop: if nothing matched on the longer prefix, retry with 1-char
        if (byLemma.Count < 10 && prefixLen == 2)
        {
            var shortPrefix = normalized[..1] + "%";
            var more = await conn.QueryAsync<(int Id, string Text, int Freq)>(
                @"SELECT l.id, l.text, COALESCE(MAX(f.count), 0) AS freq
                  FROM qlemma l
                  LEFT JOIN qfrequency f ON f.form = l.text
                  WHERE l.text LIKE @prefix AND l.isPhrase = 0
                  GROUP BY l.id, l.text
                  LIMIT 500",
                new { prefix = shortPrefix });
            foreach (var (id, text, freq) in more)
            {
                if (byLemma.ContainsKey(id)) continue;
                var d = Levenshtein.Distance(normalized, text);
                byLemma[id] = new SuggestionItem(text, text, id, d, "lemma", freq);
            }
        }

        // Rank: smaller distance first, then higher frequency, then shorter form
        var scored = byLemma.Values
            .OrderBy(s => s.Distance)
            .ThenByDescending(s => s.Frequency)
            .ThenBy(s => s.Form.Length)
            .Take(k)
            .ToList();

        return new SuggestResult(word, normalized, scored);
    }

    // ─── Define ───────────────────────────────────────────────────────
    public async Task<DefineResult> DefineAsync(string word)
    {
        var normalized = TextNormalizer.NormalizeHeadword(word);
        if (string.IsNullOrEmpty(normalized))
            return new DefineResult(word, "", null, "", false, false, "none", "", new List<EntryHit>());

        var defKey = $"def:{normalized}";
        if (_cache.TryGetValue(defKey, out DefineResult defCached))
            return defCached with { Word = word };

        await using var conn = Utilities.GetOpenMySqlConnection();

        // 1. exact lemma
        var lemma = await conn.QueryFirstOrDefaultAsync<(int Id, string Text, byte IsPhrase, byte IsDialectal)?>(
            "SELECT id, text, isPhrase, isDialectal FROM qlemma WHERE text = @t LIMIT 1",
            new { t = normalized });

        var via = "none";
        var tag = "";
        int? lemmaId = null;
        var lemmaText = "";
        var isPhrase = false;
        var isDialectal = false;

        if (lemma.HasValue)
        {
            via = "lemma";
            lemmaId = lemma.Value.Id;
            lemmaText = lemma.Value.Text;
            isPhrase = lemma.Value.IsPhrase == 1;
            isDialectal = lemma.Value.IsDialectal == 1;
        }
        else
        {
            // 2. inflected form → resolve parent lemma
            var form = await conn.QueryFirstOrDefaultAsync<(int LemmaId, string Features)?>(
                "SELECT lemmaId, features FROM qwordform WHERE form = @t LIMIT 1",
                new { t = normalized });
            if (form.HasValue)
            {
                var parent = await conn.QueryFirstOrDefaultAsync<(int Id, string Text, byte IsPhrase, byte IsDialectal)?>(
                    "SELECT id, text, isPhrase, isDialectal FROM qlemma WHERE id = @id LIMIT 1",
                    new { id = form.Value.LemmaId });
                if (parent.HasValue)
                {
                    via = "wordform";
                    tag = ExtractTag(form.Value.Features);
                    lemmaId = parent.Value.Id;
                    lemmaText = parent.Value.Text;
                    isPhrase = parent.Value.IsPhrase == 1;
                    isDialectal = parent.Value.IsDialectal == 1;
                }
            }
        }

        List<EntryHit> entries;
        if (lemmaId.HasValue)
        {
            entries = (await conn.QueryAsync<EntryHit>(
                @"SELECT e.id AS EntryId, e.sourceId AS SourceId, COALESCE(s.title, '') AS SourceTitle,
                         e.rawHtml AS RawHtml,
                         IFNULL(DATE_FORMAT(e.entryDate, '%Y-%m-%d'), '') AS EntryDate
                  FROM qlemmaentry le
                  JOIN qentry e ON e.id = le.entryId
                  LEFT JOIN qsource s ON s.id = e.sourceId
                  WHERE le.lemmaId = @id
                  ORDER BY e.id
                  LIMIT 25",
                new { id = lemmaId.Value })).ToList();
        }
        else
        {
            // 3. fall back to raw headword match (unindexed lemmas)
            entries = (await conn.QueryAsync<EntryHit>(
                @"SELECT e.id AS EntryId, e.sourceId AS SourceId, COALESCE(s.title, '') AS SourceTitle,
                         e.rawHtml AS RawHtml,
                         IFNULL(DATE_FORMAT(e.entryDate, '%Y-%m-%d'), '') AS EntryDate
                  FROM qheadword h
                  JOIN qentry e ON e.id = h.entryId
                  LEFT JOIN qsource s ON s.id = e.sourceId
                  WHERE h.textNormalized = @t
                  ORDER BY e.id
                  LIMIT 25",
                new { t = normalized })).ToList();
        }

        // Sanitize HTML server-side — defense in depth against malicious entries
        foreach (var e in entries)
        {
            e.RawHtml = HtmlSanitizer.Sanitize(e.RawHtml);
        }

        var result = new DefineResult(
            Word: word, Normalized: normalized,
            LemmaId: lemmaId, Lemma: lemmaText,
            IsPhrase: isPhrase, IsDialectal: isDialectal,
            MatchedVia: via, Tag: tag, Entries: entries
        );
        _cache.Set(defKey, result, CacheTtl);
        return result;
    }

    public async Task<object> RandomAsync()
    {
        await using var conn = Utilities.GetOpenMySqlConnection();
        var max = await conn.ExecuteScalarAsync<int>("SELECT IFNULL(MAX(id), 0) FROM qlemma");
        if (max == 0) return new { error = "no lemmas yet" };
        var rnd = new Random().Next(1, max + 1);
        var lemma = await conn.QueryFirstOrDefaultAsync<(int Id, string Text)>(
            "SELECT id, text FROM qlemma WHERE id >= @id AND isPhrase = 0 ORDER BY id LIMIT 1",
            new { id = rnd });
        return new { id = lemma.Id, text = lemma.Text };
    }

    public async Task<object> StatsAsync()
    {
        await using var conn = Utilities.GetOpenMySqlConnection();
        var entries = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM qentry");
        var headwords = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM qheadword");
        var lemmas = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM qlemma");
        var phrases = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM qphrase");
        var sources = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM qsource");
        var wordforms = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM qwordform");
        return new { entries, headwords, lemmas, phrases, sources, wordforms };
    }

    private static bool ShouldReplace(SuggestionItem cur, int newDist, int newFreq)
    {
        if (newDist < cur.Distance) return true;
        if (newDist > cur.Distance) return false;
        return newFreq > cur.Frequency;
    }

    private static string ExtractTag(string featuresJson)
    {
        if (string.IsNullOrEmpty(featuresJson)) return "";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(featuresJson);
            return doc.RootElement.TryGetProperty("tag", out var t) ? t.GetString() ?? "" : "";
        }
        catch
        {
            return "";
        }
    }
}
