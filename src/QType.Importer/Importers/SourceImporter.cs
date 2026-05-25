using System.Text.Json;
using QType.DBHelper;
using QType.Importer.JsonModels;

namespace QType.Importer.Importers;

public static class SourceImporter
{
    public static async Task RunAsync(string sourcesPath)
    {
        Console.WriteLine($"[sources] Reading {sourcesPath}");
        await using var fs = File.OpenRead(sourcesPath);
        var sources = await JsonSerializer.DeserializeAsync<List<SqSource>>(fs)
                      ?? new List<SqSource>();
        Console.WriteLine($"[sources] Parsed {sources.Count} sources");

        await using var conn = Utilities.GetOpenMySqlConnection();
        await using var tx = await conn.BeginTransactionAsync();

        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var rows = new List<object[]>(sources.Count);
        foreach (var s in sources)
        {
            rows.Add(new object[] { s.Id, s.Title ?? string.Empty, now, (byte)0 });
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM qsource";
            await cmd.ExecuteNonQueryAsync();
        }

        var inserted = await BulkInsert.ExecuteAsync(
            conn, tx, "qsource",
            new[] { "id", "title", "addTime", "qStatus" },
            rows,
            batchSize: 500);

        await tx.CommitAsync();
        Console.WriteLine($"[sources] Inserted {inserted} rows into qsource");
    }
}
