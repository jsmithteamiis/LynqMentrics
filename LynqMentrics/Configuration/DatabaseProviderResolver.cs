namespace LynqMentrics.Configuration;

public static class DatabaseProviderResolver
{
    public static DatabaseProvider Resolve(string? configuredValue)
    {
        if (string.Equals(configuredValue, "Postgres", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(configuredValue, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseProvider.PostgreSql;
        }

        if (string.Equals(configuredValue, "Sqlite", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(configuredValue, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseProvider.Sqlite;
        }

        throw new InvalidOperationException(
            $"Unsupported Database provider '{configuredValue}'. Supported values: Sqlite, PostgreSql.");
    }
}
