using System.Diagnostics;
using Dapper;
using QType.COMMON.Morphology;
using QType.DBHelper;

namespace QType.Importer.Importers;

/// Stage 1 — assign part-of-speech tags + lemma noise flags.
///
/// Approach: rule-based suffix classifier on the lemma text. The dictionary
/// corpus does NOT carry reliable explicit POS markers (we checked — most
/// "marker" hits are false positives from definitions referring to grammar
/// concepts). Suffix patterns in Kazakh are highly regular though, so a
/// pure-text classifier captures ~70% with high confidence.
///
/// Writes:
///   - qlemma.pos        — "verb" | "noun" | "adj" | "adv" | "propn" | "x" | ""
///   - qlemma.qStatus    — bit 1 set if confidence < 0.7 (review queue)
public static class PosClassifier
{
    public static async Task RunAsync()
    {
        Console.WriteLine("[pos] Stage 1 — POS classification + lemma noise flagging");
        var sw = Stopwatch.StartNew();

        await using var conn = Utilities.GetOpenMySqlConnection();
        var total = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM qlemma WHERE isPhrase = 0");
        if (total == 0)
        {
            Console.Error.WriteLine("[pos] qlemma is empty — run Stage 2 (lemmas) first.");
            return;
        }
        Console.WriteLine($"[pos] {total:N0} non-phrase lemmas to classify");

        var runId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO qpipelinerun (stage, startedAt, notes, qStatus) VALUES ('stage1-pos', UNIX_TIMESTAMP(), '', 0); SELECT LAST_INSERT_ID();");

        var lemmas = await conn.QueryAsync<(int Id, string Text)>(
            "SELECT id, text FROM qlemma WHERE isPhrase = 0",
            commandTimeout: 600);

        // Tally for reporting
        var tally = new Dictionary<string, int>();
        var lowConfidence = 0;

        var batch = new List<(int Id, string Pos, byte Status)>(2000);
        long done = 0;

        foreach (var (id, text) in lemmas)
        {
            var g = KazakhPos.Classify(text);
            var posCol = KazakhPos.PosToColumn(g.Pos);
            tally[posCol] = tally.GetValueOrDefault(posCol) + 1;

            byte status = 0;
            if (g.Confidence < 0.7)
            {
                status = 2; // bit 1 = review-needed
                lowConfidence++;
            }
            // Add separate "foreign / loanword" flag — bit 2
            if (g.Pos == Pos.Foreign) status |= 4;

            batch.Add((id, posCol, status));

            if (batch.Count >= 2000)
            {
                await FlushAsync(batch);
                batch.Clear();
            }
            done++;
            if (done % 20_000 == 0)
            {
                var rate = done / Math.Max(1, sw.Elapsed.TotalSeconds);
                Console.WriteLine($"[pos] {done:N0} / {total:N0}, {rate:F0} lemma/s");
            }
        }

        if (batch.Count > 0) await FlushAsync(batch);

        Console.WriteLine($"[pos] done in {sw.Elapsed:hh\\:mm\\:ss}. Distribution:");
        foreach (var kv in tally.OrderByDescending(x => x.Value))
        {
            var pct = 100.0 * kv.Value / total;
            Console.WriteLine($"  {(string.IsNullOrEmpty(kv.Key) ? "(empty)" : kv.Key),-8} {kv.Value,8:N0}  {pct:F1}%");
        }
        Console.WriteLine($"  low-confidence (status bit 1): {lowConfidence:N0}");

        await conn.ExecuteAsync(@"
            UPDATE qpipelinerun
            SET finishedAt = UNIX_TIMESTAMP(), rowsProcessed = @rows, notes = @notes, qStatus = 1
            WHERE id = @id",
            new
            {
                id = runId,
                rows = (uint)total,
                notes = $"verbs={tally.GetValueOrDefault("verb")}, nouns={tally.GetValueOrDefault("noun")}, " +
                        $"adj={tally.GetValueOrDefault("adj")}, adv={tally.GetValueOrDefault("adv")}, " +
                        $"foreign={tally.GetValueOrDefault("x")}, lowConf={lowConfidence}, elapsed={sw.Elapsed:hh\\:mm\\:ss}"
            });
    }

    private static async Task FlushAsync(List<(int Id, string Pos, byte Status)> batch)
    {
        // Multi-row UPDATE via CASE … WHEN — single round-trip per batch.
        await using var conn = Utilities.GetOpenMySqlConnection();
        await using var tx = await conn.BeginTransactionAsync();

        var posSb = new System.Text.StringBuilder("UPDATE qlemma SET pos = CASE id ");
        var statusSb = new System.Text.StringBuilder("qStatus = CASE id ");
        var ids = new List<int>(batch.Count);
        var paramObj = new DynamicParameters();

        for (var i = 0; i < batch.Count; i++)
        {
            var b = batch[i];
            posSb.Append($"WHEN @id_{i} THEN @pos_{i} ");
            statusSb.Append($"WHEN @id_{i} THEN @st_{i} ");
            paramObj.Add($"id_{i}", b.Id);
            paramObj.Add($"pos_{i}", b.Pos);
            paramObj.Add($"st_{i}", b.Status);
            ids.Add(b.Id);
        }
        posSb.Append("END, ").Append(statusSb).Append("END WHERE id IN @ids");
        paramObj.Add("ids", ids);

        await conn.ExecuteAsync(posSb.ToString(), paramObj, transaction: tx, commandTimeout: 300);
        await tx.CommitAsync();
    }
}
