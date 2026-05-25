using System.Collections.Concurrent;

namespace QType.COMMON;

public class QSingleton
{
    private static QSingleton _instance;
    private static readonly object Lock = new();
    private readonly ConcurrentDictionary<string, string> _strings = new();

    private QSingleton() { }

    public static QSingleton GetInstance()
    {
        if (_instance != null) return _instance;
        lock (Lock)
        {
            _instance ??= new QSingleton();
        }
        return _instance;
    }

    public string GetConnectionString()
    {
        return _strings.TryGetValue("connectionString", out var v) ? v ?? string.Empty : string.Empty;
    }

    public void SetConnectionString(string value)
    {
        _strings.AddOrUpdate("connectionString", value, (_, _) => value);
    }
}
