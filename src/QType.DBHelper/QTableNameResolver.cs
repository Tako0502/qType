using Dapper;

namespace QType.DBHelper;

public class QTableNameResolver : SimpleCRUD.TableNameResolver
{
    public override string ResolveTableName(Type type)
    {
        return type.Name.ToLowerInvariant();
    }
}
