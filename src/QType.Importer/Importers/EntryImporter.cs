using System.Diagnostics;
using System.Text.Json;
using Dapper;
using QType.COMMON;
using QType.DBHelper;
using QType.Importer.JsonModels;

namespace QType.Importer.Importers;

public static class EntryImporter
{
    private static readonly string[] EntryCols = {
        "rawIndex", "rawHeadword", "rawHtml", "sourceId", "entryDate", "addTime", "qStatus"
    };

    private static readonly string[] HeadwordCols = {
        "entryId", "text", "textNormalized", "variantIndex", "charCount",
        "isPhrase", "isDialectal", "addTime", "qStatus"
    };

    public static async Task RunAsync(string itemsPath, int batchSize, int maxItems)
    {
        Console.WriteLine($"[entries] Streaming {itemsPath} (batch={batchSize}, maxItems={(maxItems == 0 ? "all" : maxItems.ToString())})");

        var resumeFrom = -1;
        await using (var probe = Utilities.GetOpenMySqlConnection())
        {
            resumeFrom = await probe.ExecuteScalarAsync<int?>(
                "SELECT IFNULL(MAX(rawIndex), -1) FROM qentry") ?? -1;
        }
        if (resumeFrom >= 0)
            Console.WriteLine($"[entries] Resuming after rawIndex={resumeFrom}");

        var pipelineRunId = await StartPipelineRunAsync("stage0-entries");

        var sw = Stopwatch.StartNew();
        var totalEntries = 0L;
        var totalHeadwords = 0L;
        var skipped = 0L;
        var rawIndex = -1;

        var batch = new List<(int RawIndex, SqItem Item)>(batchSize);

        await using var fs = File.OpenRead(itemsPath);
        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<SqItem>(fs))
        {
            rawIndex++;
            if (item == null) continue;
            if (rawIndex <= resumeFrom) { skipped++; continue; }
            if (maxItems > 0 && totalEntries >= maxItems) break;

            batch.Add((rawIndex, item));
            if (batch.Count >= batchSize)
            {
                var (e, h) = await FlushBatchAsync(batch);
                totalEntries += e;
                totalHeadwords += h;
                batch.Clear();

                if (totalEntries % (batchSize * 20) == 0)
                {
                    var rate = totalEntries / Math.Max(1, sw.Elapsed.TotalSeconds);
                    Console.WriteLine($"[entries] {totalEntries:N0} entries, {totalHeadwords:N0} headwords, {rate:F0} rows/s");
                }
            }
        }

        if (batch.Count > 0)
        {
            var (e, h) = await FlushBatchAsync(batch);
            totalEntries += e;
            totalHeadwords += h;
        }

        await FinishPipelineRunAsync(pipelineRunId, (uint)totalEntries,
            $"entries={totalEntries}, headwords={totalHeadwords}, skipped={skipped}, elapsed={sw.Elapsed:hh\\:mm\\:ss}");

        Console.WriteLine($"[entries] Done. Entries={totalEntries:N0}, Headwords={totalHeadwords:N0}, Skipped={skipped:N0}, Elapsed={sw.Elapsed:hh\\:mm\\:ss}");
    }

    private static async Task<(int entries, int headwords)> FlushBatchAsync(List<(int RawIndex, SqItem Item)> batch)
    {
        if (batch.Count == 0) return (0, 0);
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var entryRows = new List<object[]>(batch.Count);
        foreach (var (idx, item) in batch)
        {
            var entryDate = TextNormalizer.ParseSqDate(item.SqDate);
            entryRows.Add(new object[]
            {
                idx,
                item.SqWord ?? string.Empty,
                item.SqText ?? string.Empty,
                item.SqSid,
                entryDate == null ? DBNull.Value : (object)entryDate,
                now,
                (byte)0,
            });
        }

        await using var conn = Utilities.GetOpenMySqlConnection();
        await using var tx = await conn.BeginTransactionAsync();

        var inserted = await BulkInsert.ExecuteAsync(conn, tx, "qentry", EntryCols, entryRows, batchSize: 500);
        if (inserted != batch.Count)
            throw new InvalidOperationException($"Expected to insert {batch.Count} entries, inserted {inserted}");

        var minRawIndex = batch[0].RawIndex;
        var maxRawIndex = batch[^1].RawIndex;
        var idMap = (await conn.QueryAsync<(int Id, int RawIndex)>(
            "SELECT id, rawIndex FROM qentry WHERE rawIndex BETWEEN @min AND @max",
            new { min = minRawIndex, max = maxRawIndex },
            transaction: tx)).ToDictionary(x => x.RawIndex, x => x.Id);

        var hwRows = new List<object[]>(batch.Count * 2);
        foreach (var (idx, item) in batch)
        {
            if (!idMap.TryGetValue(idx, out var entryId))
                throw new InvalidOperationException($"Missing id for rawIndex={idx}");
            var isDialectal = TextNormalizer.LooksDialectal(item.SqText) ? (byte)1 : (byte)0;
            byte variantIndex = 0;
            foreach (var variant in TextNormalizer.SplitVariants(item.SqWord ?? string.Empty))
            {
                var text = variant.Trim();
                if (text.Length == 0) continue;
                if (text.Length > 256) text = text[..256];
                var norm = TextNormalizer.NormalizeHeadword(text);
                if (norm.Length > 256) norm = norm[..256];
                var isPhrase = TextNormalizer.IsPhrase(text) ? (byte)1 : (byte)0;
                hwRows.Add(new object[]
                {
                    entryId,
                    text,
                    norm,
                    variantIndex,
                    (short)Math.Min(text.Length, short.MaxValue),
                    isPhrase,
                    isDialectal,
                    now,
                    (byte)0,
                });
                if (variantIndex < byte.MaxValue) variantIndex++;
            }
        }

        var hwInserted = 0;
        if (hwRows.Count > 0)
        {
            hwInserted = await BulkInsert.ExecuteAsync(conn, tx, "qheadword", HeadwordCols, hwRows, batchSize: 1000);
        }

        await tx.CommitAsync();
        return (inserted, hwInserted);
    }

    private static async Task<int> StartPipelineRunAsync(string stage)
    {
        await using var conn = Utilities.GetOpenMySqlConnection();
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return await conn.ExecuteScalarAsync<int>(
            "INSERT INTO qpipelinerun (stage, startedAt, notes, qStatus) VALUES (@stage, @now, '', 0); SELECT LAST_INSERT_ID();",
            new { stage, now });
    }

    private static async Task FinishPipelineRunAsync(int id, uint rowsProcessed, string notes)
    {
        await using var conn = Utilities.GetOpenMySqlConnection();
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await conn.ExecuteAsync(
            "UPDATE qpipelinerun SET finishedAt=@now, rowsProcessed=@rows, notes=@notes, qStatus=1 WHERE id=@id",
            new { now, rows = rowsProcessed, notes, id });
    }
}
