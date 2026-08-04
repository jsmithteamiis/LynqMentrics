using System.Data.Common;
using LynqMentrics.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LynqMentrics.Data;

public sealed class DatabaseConnectionFactory(
    IConfiguration configuration,
    IOptions<DatabaseOptions> databaseOptions) : IDatabaseConnectionFactory
{
    private readonly IConfiguration _configuration = configuration;
    private readonly DatabaseOptions _databaseOptions = databaseOptions.Value;

    public DbConnection CreateConfiguredProviderConnection()
    {
        var provider = DatabaseProviderResolver.Resolve(_databaseOptions.Provider);
        return provider == DatabaseProvider.PostgreSql
            ? CreatePostgreSqlConnection()
            : CreateSqliteConnection();
    }

    public DbConnection CreateSqliteConnection()
    {
        var connectionString = ResolveConnectionString(_databaseOptions.Sqlite.ConnectionStringName);
        return new SqliteConnection(connectionString);
    }

    public DbConnection CreatePostgreSqlConnection()
    {
        var connectionString = ResolveConnectionString(_databaseOptions.PostgreSql.ConnectionStringName);
        return new NpgsqlConnection(connectionString);
    }

    private string ResolveConnectionString(string name)
    {
        var connectionString = _configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Missing connection string '{name}'.");
        }

        return connectionString;
    }
}
