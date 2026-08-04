using System.Data.Common;

namespace LynqMentrics.Data;

public interface IDatabaseConnectionFactory
{
    DbConnection CreateConfiguredProviderConnection();

    DbConnection CreateSqliteConnection();

    DbConnection CreatePostgreSqlConnection();
}
