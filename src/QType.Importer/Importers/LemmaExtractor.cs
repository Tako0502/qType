using System.Diagnostics;
using Dapper;
using QType.DBHelper;

namespace QType.Importer.Importers;

/// Stage 2 — extract canonical lemmas from qheadword and link entries.
/// Pure SQL, idempotent (TRUNCATE+rebuild). Run after Stage 0.
public static class LemmaExtractor
{
    public static async Task RunAsync()
    {
        Console.WriteLine("[lemmas] Stage 2 — extract lemmas from qheadword");
        var sw = Stopwatch.StartNew();

        await using var conn = Utilities.GetOpenMySqlConnection();

        var headwordCount = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM qheadword");
        if (headwordCount == 0)
        {
            Console.Error.WriteLine("[lemmas] qheadword is empty — run Stage 0 (entries) first.");
            return;
        }

        var runId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO qpipelinerun (stage, startedAt, notes, qStatus) VALUES ('stage2-lemmas', UNIX_TIMESTAMP(), '', 0); SELECT LAST_INSERT_ID();");

        await using (var tx = await conn.BeginTransactionAsync())
        {
            await conn.ExecuteAsync("TRUNCATE qlemmaentry", transaction: tx);
            await conn.ExecuteAsync("TRUNCATE qlemma", transaction: tx);
            await conn.ExecuteAsync("TRUNCATE qphrase", transaction: tx);
            await tx.CommitAsync();
        }

        Console.WriteLine("[lemmas] inserting single-word lemmas...");
        var lemmaRows = await conn.ExecuteAsync(@"
            INSERT INTO qlemma (text, pos, isPhrase, isDialectal, isLoanword, latinTranslit, notes, addTime, updateTime, qStatus)
            SELECT
                textNormalized,
                '' AS pos,
                0 AS isPhrase,
                MAX(isDialectal) AS isDialectal,
                0 AS isLoanword,
                '' AS latinTranslit,
                '' AS notes,
                UNIX_TIMESTAMP() AS addTime,
                UNIX_TIMESTAMP() AS updateTime,
                0 AS qStatus
            FROM qheadword
            WHERE isPhrase = 0 AND CHAR_LENGTH(textNormalized) > 0
            GROUP BY textNormalized;", commandTimeout: 600);
        Console.WriteLine($"[lemmas]   {lemmaRows:N0} lemmas");

        Console.WriteLine("[lemmas] linking entries via qlemmaentry...");
        var linkRows = await conn.ExecuteAsync(@"
            INSERT INTO qlemmaentry (lemmaId, entryId, weight, addTime)
            SELECT l.id, h.entryId, 1.0, UNIX_TIMESTAMP()
            FROM qheadword h
            JOIN qlemma l ON l.text = h.textNormalized
            WHERE h.isPhrase = 0
            GROUP BY l.id, h.entryId;", commandTimeout: 1200);
        Console.WriteLine($"[lemmas]   {linkRows:N0} lemma↔entry links");

        Console.WriteLine("[lemmas] inserting phrases...");
        var phraseRows = await conn.ExecuteAsync(@"
            INSERT INTO qphrase (headLemmaId, text, definition, sourceId, entryId, addTime)
            SELECT
                0 AS headLemmaId,
                h.text,
                LEFT(e.rawHtml, 4096) AS definition,
                e.sourceId,
                h.entryId,
                UNIX_TIMESTAMP()
            FROM qheadword h
            JOIN qentry e ON e.id = h.entryId
            WHERE h.isPhrase = 1;", commandTimeout: 600);
        Console.WriteLine($"[lemmas]   {phraseRows:N0} phrases");

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
                rows = (uint)lemmaRows,
                notes = $"lemmas={lemmaRows}, links={linkRows}, phrases={phraseRows}, elapsed={sw.Elapsed:hh\\:mm\\:ss}"
            });

        Console.WriteLine($"[lemmas] done in {sw.Elapsed:hh\\:mm\\:ss}");
    }
}
