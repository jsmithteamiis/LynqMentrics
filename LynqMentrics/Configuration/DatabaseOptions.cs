namespace LynqMentrics.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Provider { get; set; } = string.Empty;

    public SqliteDatabaseOptions Sqlite { get; set; } = new();

    public PostgreSqlDatabaseOptions PostgreSql { get; set; } = new();

    public DataMigrationOptions Migration { get; set; } = new();
}

public sealed class SqliteDatabaseOptions
{
    public string ConnectionStringName { get; set; } = "DefaultConnection";

    public int CommandTimeoutSeconds { get; set; } = 30;
}

public sealed class PostgreSqlDatabaseOptions
{
    public string ConnectionStringName { get; set; } = "PostgresConnection";

    public int CommandTimeoutSeconds { get; set; } = 30;

    public int MaxRetryCount { get; set; } = 5;

    public int MaxRetryDelaySeconds { get; set; } = 10;
}

public sealed class DataMigrationOptions
{
    public bool Enabled { get; set; }

    public int BatchSize { get; set; } = 500;

    public bool RunSchemaMigrations { get; set; } = true;

    public bool AllowNonEmptyTarget { get; set; }

    public string SourceConnectionStringName { get; set; } = "DefaultConnection";

    public string TargetConnectionStringName { get; set; } = "PostgresConnection";
}
