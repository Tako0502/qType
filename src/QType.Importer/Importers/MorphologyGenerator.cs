using System.Diagnostics;
using Dapper;
using QType.COMMON.Morphology;
using QType.DBHelper;

namespace QType.Importer.Importers;

/// Stage 4 — generate inflected forms for every lemma using the rule-based Kazakh inflector.
/// Writes to qwordform with origin='rules'. Idempotent (TRUNCATEs first).
public static class MorphologyGenerator
{
    private static readonly string[] WordformCols =
    {
        "lemmaId", "form", "features", "origin", "addTime", "qStatus"
    };

    public static async Task RunAsync(int batchSize = 5000)
    {
        Console.WriteLine("[morph] Stage 4 — generating inflected forms");
        var sw = Stopwatch.StartNew();

        await using var conn = Utilities.GetOpenMySqlConnection();

        var lemmaCount = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM qlemma WHERE isPhrase = 0");
        if (lemmaCount == 0)
        {
            Console.Error.WriteLine("[morph] qlemma is empty — run Stage 2 (lemmas) first.");
            return;
        }
        Console.WriteLine($"[morph] {lemmaCount:N0} non-phrase lemmas to inflect");

        var runId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO qpipelinerun (stage, startedAt, notes, qStatus) VALUES ('stage4-morph', UNIX_TIMESTAMP(), '', 0); SELECT LAST_INSERT_ID();");

        await conn.ExecuteAsync("TRUNCATE qwordform");

        var lemmas = await conn.QueryAsync<(int Id, string Text)>(
            "SELECT id, text FROM qlemma WHERE isPhrase = 0",
            commandTimeout: 600);

        var batch = new List<object[]>(batchSize);
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long totalForms = 0;
        long processedLemmas = 0;

        foreach (var (id, text) in lemmas)
        {
            processedLemmas++;
            var seen = new HashSet<string>();
            foreach (var inf in KazakhInflector.Inflect(text))
            {
                if (inf.Form.Length == 0 || inf.Form.Length > 128) continue;
                if (!seen.Add(inf.Form)) continue;
                batch.Add(new object[]
                {
                    id,
                    inf.Form,
                    $"{{\"tag\":\"{inf.Tag}\"}}",
                    "rules",
                    now,
                    (byte)0
                });

                if (batch.Count >= batchSize)
                {
                    totalForms += await FlushAsync(batch);
                    batch.Clear();
                }
            }

            if (processedLemmas % 10000 == 0)
            {
                var rate = processedLemmas / Math.Max(1, sw.Elapsed.TotalSeconds);
                Console.WriteLine($"[morph] {processedLemmas:N0} / {lemmaCount:N0} lemmas, {totalForms:N0} forms, {rate:F0} lemma/s");
            }
        }

        if (batch.Count > 0)
            totalForms += await FlushAsync(batch);

        await conn.ExecuteAsync(@"
            UPDATE qpipelinerun
            SET finishedAt = UNIX_TIMESTAMP(),
                rowsProcessed = @rows,
                notes = @notes,
                qStatus = 1
            WHERE id = @id",
            new
            {
                id = runId,
                rows = (uint)totalForms,
                notes = $"lemmas={processedLemmas}, forms={totalForms}, elapsed={sw.Elapsed:hh\\:mm\\:ss}"
            });

        Console.WriteLine($"[morph] done — {totalForms:N0} forms from {processedLemmas:N0} lemmas in {sw.Elapsed:hh\\:mm\\:ss}");
    }

    private static async Task<int> FlushAsync(List<object[]> rows)
    {
        await using var conn = Utilities.GetOpenMySqlConnection();
        await using var tx = await conn.BeginTransactionAsync();
        // qwordform UNIQUE is on (lemmaId, form). Per-lemma dedup in the caller
        // (HashSet `seen`) guarantees uniqueness, so plain INSERT is safe.
        var inserted = await BulkInsert.ExecuteAsync(conn, tx, "qwordform", WordformCols, rows, batchSize: 2000);
        await tx.CommitAsync();
        return inserted;
    }
}
