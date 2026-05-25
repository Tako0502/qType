using System.Diagnostics;
using Dapper;
using QType.DBHelper;

namespace QType.Importer.Importers;

/// Stage 5 — Load word-frequency data into qfrequency.
///
/// Format: Leipzig Corpora TSV (id\tword\tcount). One row per unique surface form.
/// Default source: Wikipedia kk corpus (~200k unique tokens, downloaded once).
public static class FrequencyImporter
{
    public static async Task RunAsync(string path, string source = "wikipedia-kk")
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"[freq] file not found: {path}");
            return;
        }

        Console.WriteLine($"[freq] Stage 5 — loading frequencies from {path}");
        var sw = Stopwatch.StartNew();

        var rows = new List<(string form, int count)>(250_000);
        var skipped = 0;
        using (var reader = new StreamReader(path))
        {
            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                var parts = line.Split('\t');
                if (parts.Length < 3) { skipped++; continue; }
                var word = parts[1].Trim();
                if (string.IsNullOrEmpty(word)) { skipped++; continue; }
                // skip non-Kazakh tokens (punctuation, numbers, latin-only)
                if (!ContainsKazakhLetter(word)) { skipped++; continue; }
                if (!int.TryParse(parts[2].Trim(), out var count)) { skipped++; continue; }

                var normalized = word.ToLowerInvariant();
                if (normalized.Length > 128) continue;
                rows.Add((normalized, count));
            }
        }
        Console.WriteLine($"[freq] parsed {rows.Count:N0} entries ({skipped:N0} skipped non-Kazakh / malformed)");

        // Aggregate by normalized form (lowercase collisions)
        var agg = rows
            .GroupBy(r => r.form)
            .Select(g => (form: g.Key, count: g.Sum(x => x.count)))
            .ToList();
        Console.WriteLine($"[freq] {agg.Count:N0} unique after lowercase aggregation");

        await using var conn = Utilities.GetOpenMySqlConnection();

        var runId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO qpipelinerun (stage, startedAt, notes, qStatus) VALUES ('stage5-freq', UNIX_TIMESTAMP(), '', 0); SELECT LAST_INSERT_ID();");

        // Clear existing for this source, then bulk insert
        await conn.ExecuteAsync("DELETE FROM qfrequency WHERE source = @source", new { source }, commandTimeout: 300);

        var batchSize = 5000;
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long total = 0;
        for (var i = 0; i < agg.Count; i += batchSize)
        {
            var slice = agg.Skip(i).Take(batchSize).ToList();
            await using var tx = await conn.BeginTransactionAsync();
            var batch = slice.Select(r => new object[] { r.form, (uint)r.count, source, now }).ToList();
            await BulkInsert.ExecuteAsync(conn, tx, "qfrequency",
                new[] { "form", "count", "source", "updateTime" },
                batch, batchSize: 2000, ignoreDuplicates: true);
            await tx.CommitAsync();
            total += slice.Count;
            if (total % 50_000 == 0)
                Console.WriteLine($"[freq] {total:N0} inserted, {total / Math.Max(1, sw.Elapsed.TotalSeconds):F0} row/s");
        }

        await conn.ExecuteAsync(@"
            UPDATE qpipelinerun
            SET finishedAt = UNIX_TIMESTAMP(), rowsProcessed = @rows, notes = @notes, qStatus = 1
            WHERE id = @id",
            new
            {
                id = runId,
                rows = (uint)total,
                notes = $"source={source}, rows={total}, parsed={rows.Count}, skipped={skipped}, elapsed={sw.Elapsed:hh\\:mm\\:ss}"
            });

        Console.WriteLine($"[freq] done — {total:N0} rows in {sw.Elapsed:hh\\:mm\\:ss}");
    }

    private static bool ContainsKazakhLetter(string s)
    {
        foreach (var c in s)
            if (c >= 'а' && c <= 'я') return true;
            else if (c >= 'А' && c <= 'Я') return true;
            else if ("әіңғүұқөһӘІҢҒҮҰҚӨҺ".Contains(c)) return true;
        return false;
    }
}
