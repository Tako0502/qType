using System.Text;
using MySqlConnector;

namespace QType.DBHelper;

/// Batched parameterized INSERT for high-throughput loads (no temp files, no special MySQL grants).
public static class BulkInsert
{
    public static async Task<int> ExecuteAsync(
        MySqlConnection conn,
        MySqlTransaction tx,
        string table,
        IReadOnlyList<string> columns,
        IReadOnlyList<object[]> rows,
        int batchSize = 1000,
        bool ignoreDuplicates = false)
    {
        if (rows.Count == 0) return 0;
        var total = 0;
        for (var offset = 0; offset < rows.Count; offset += batchSize)
        {
            var take = Math.Min(batchSize, rows.Count - offset);
            total += await InsertBatchAsync(conn, tx, table, columns, rows, offset, take, ignoreDuplicates);
        }
        return total;
    }

    private static async Task<int> InsertBatchAsync(
        MySqlConnection conn,
        MySqlTransaction tx,
        string table,
        IReadOnlyList<string> columns,
        IReadOnlyList<object[]> rows,
        int offset,
        int count,
        bool ignoreDuplicates)
    {
        var sb = new StringBuilder(64 * 1024);
        sb.Append(ignoreDuplicates ? "INSERT IGNORE INTO " : "INSERT INTO ").Append(table).Append(" (");
        for (var i = 0; i < columns.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(columns[i]);
        }
        sb.Append(") VALUES ");

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        for (var r = 0; r < count; r++)
        {
            if (r > 0) sb.Append(',');
            sb.Append('(');
            for (var c = 0; c < columns.Count; c++)
            {
                if (c > 0) sb.Append(',');
                var paramName = $"@p_{r}_{c}";
                sb.Append(paramName);
                cmd.Parameters.AddWithValue(paramName, rows[offset + r][c] ?? DBNull.Value);
            }
            sb.Append(')');
        }

        cmd.CommandText = sb.ToString();
        cmd.CommandTimeout = 600;
        return await cmd.ExecuteNonQueryAsync();
    }
}
