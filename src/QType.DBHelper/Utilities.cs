using System.Data;
using Dapper;
using MySqlConnector;
using QType.COMMON;

namespace QType.DBHelper;

public static class Utilities
{
    private static int _initialized;

    public static IDbConnection GetOpenConnection()
    {
        EnsureInitialized();
        var cs = QSingleton.GetInstance().GetConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Connection string not set. Call QSingleton.GetInstance().SetConnectionString(...) at startup.");
        var conn = new MySqlConnection(cs);
        conn.Open();
        return conn;
    }

    public static MySqlConnection GetOpenMySqlConnection()
    {
        return (MySqlConnection)GetOpenConnection();
    }

    private static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1) return;
        SimpleCRUD.SetDialect(SimpleCRUD.Dialect.MySQL);
        SimpleCRUD.SetTableNameResolver(new QTableNameResolver());
    }
}
