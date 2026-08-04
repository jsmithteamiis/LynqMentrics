using System.Globalization;
using System.Linq.Expressions;
using LynqMentrics.Configuration;
using LynqMentrics.Data;
using LynqMentrics.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LynqMentrics.Services;

public sealed class SqliteToPostgreSqlMigrationService(
    IDatabaseConnectionFactory connectionFactory,
    IOptions<DatabaseOptions> optionsAccessor,
    ILogger<SqliteToPostgreSqlMigrationService> logger) : IDataMigrationService
{
    private static readonly TableDefinition[] MigrationPlan =
    [
        TableDefinition.Create<IdentityRole>("AspNetRoles", x => x.Id),
        TableDefinition.Create<AppUser>("AspNetUsers", x => x.Id),
        TableDefinition.Create<IdentityRoleClaim<string>>("AspNetRoleClaims", x => x.Id),
        TableDefinition.Create<IdentityUserClaim<string>>("AspNetUserClaims", x => x.Id),
        TableDefinition.Create<IdentityUserLogin<string>>("AspNetUserLogins", x => x.UserId + "|" + x.LoginProvider + "|" + x.ProviderKey),
        TableDefinition.Create<IdentityUserRole<string>>("AspNetUserRoles", x => x.UserId + "|" + x.RoleId),
        TableDefinition.Create<IdentityUserToken<string>>("AspNetUserTokens", x => x.UserId + "|" + x.LoginProvider + "|" + x.Name),
        TableDefinition.Create<Link>("Links", x => x.Id),
        TableDefinition.Create<Click>("Clicks", x => x.Id),
        TableDefinition.Create<PrivacyConsent>("PrivacyConsents", x => x.Id)
    ];

    private readonly DataMigrationOptions _migrationOptions = optionsAccessor.Value.Migration;
    private readonly DatabaseOptions _databaseOptions = optionsAccessor.Value;

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        if (!_migrationOptions.Enabled)
        {
            throw new InvalidOperationException(
                "Data migration is disabled. Set Database:Migration:Enabled=true to run SQLite to PostgreSQL migration.");
        }

        if (_migrationOptions.BatchSize <= 0)
        {
            throw new InvalidOperationException("Database:Migration:BatchSize must be greater than zero.");
        }

        await using var sourceContext = CreateSqliteContext(connectionFactory);
        await using var targetContext = CreatePostgreSqlContext(connectionFactory);

        await RunPreFlightValidationAsync(sourceContext, targetContext, cancellationToken);

        logger.LogInformation(
            "Starting SQLite -> PostgreSQL data migration. Batch size: {BatchSize}.",
            _migrationOptions.BatchSize);

        var tableResults = new List<TableResult>(MigrationPlan.Length);
        foreach (var table in MigrationPlan)
        {
            var result = await table.CopyAsync(
                sourceContext,
                targetContext,
                _migrationOptions.BatchSize,
                logger,
                cancellationToken);
            tableResults.Add(result);
        }

        await ResetIdentitySequencesAsync(targetContext, cancellationToken);
        await VerifyRowCountsAsync(tableResults, cancellationToken);

        logger.LogInformation("SQLite -> PostgreSQL data migration completed successfully.");
    }

    private async Task RunPreFlightValidationAsync(
        AppDbContext sourceContext,
        AppDbContext targetContext,
        CancellationToken cancellationToken)
    {
        if (!await sourceContext.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException("Could not connect to the source SQLite database.");
        }

        if (!await targetContext.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException("Could not connect to the target PostgreSQL database.");
        }

        if (_migrationOptions.RunSchemaMigrations)
        {
            logger.LogInformation("Ensuring PostgreSQL schema exists before data transfer.");
            await targetContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        foreach (var table in MigrationPlan)
        {
            var exists = await TableExistsAsync(targetContext, table.Name, cancellationToken);
            if (!exists)
            {
                throw new InvalidOperationException(
                    $"PostgreSQL schema readiness check failed. Expected table '{table.Name}' was not found.");
            }

            var targetCount = await table.CountTargetAsync(targetContext, cancellationToken);
            if (!_migrationOptions.AllowNonEmptyTarget && targetCount > 0)
            {
                throw new InvalidOperationException(
                    $"Target table '{table.Name}' is not empty ({targetCount} rows). " +
                    "Set Database:Migration:AllowNonEmptyTarget=true if this is intentional.");
            }
        }
    }

    private async Task ResetIdentitySequencesAsync(AppDbContext targetContext, CancellationToken cancellationToken)
    {
        logger.LogInformation("Resetting PostgreSQL identity sequences.");

        const string roleClaimsSequenceSql = """
            SELECT setval(
                pg_get_serial_sequence('"AspNetRoleClaims"', 'Id'),
                COALESCE(MAX("Id"), 1),
                MAX("Id") IS NOT NULL)
            FROM "AspNetRoleClaims";
            """;
        await targetContext.Database.ExecuteSqlRawAsync(roleClaimsSequenceSql, cancellationToken);

        const string userClaimsSequenceSql = """
            SELECT setval(
                pg_get_serial_sequence('"AspNetUserClaims"', 'Id'),
                COALESCE(MAX("Id"), 1),
                MAX("Id") IS NOT NULL)
            FROM "AspNetUserClaims";
            """;
        await targetContext.Database.ExecuteSqlRawAsync(userClaimsSequenceSql, cancellationToken);
    }

    private Task VerifyRowCountsAsync(IEnumerable<TableResult> results, CancellationToken cancellationToken)
    {
        foreach (var result in results)
        {
            if (result.SourceCount != result.TargetCount)
            {
                throw new InvalidOperationException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Row count mismatch for table '{result.Name}'. Source: {result.SourceCount}, Target: {result.TargetCount}."));
            }

            logger.LogInformation(
                "Row count verified for {Table}. Source={SourceCount}, Target={TargetCount}.",
                result.Name,
                result.SourceCount,
                result.TargetCount);
        }

        return Task.CompletedTask;
    }

    private static async Task<bool> TableExistsAsync(
        AppDbContext targetContext,
        string tableName,
        CancellationToken cancellationToken)
    {
        const string existsSql = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = {0}) AS "Value"
            """;
        return await targetContext.Database.SqlQueryRaw<bool>(existsSql, tableName).SingleAsync(cancellationToken);
    }

    private sealed record TableResult(string Name, int SourceCount, int TargetCount);

    private sealed class TableDefinition
    {
        private readonly Func<AppDbContext, CancellationToken, Task<int>> _sourceCount;
        private readonly Func<AppDbContext, CancellationToken, Task<int>> _targetCount;
        private readonly Func<AppDbContext, AppDbContext, int, ILogger, CancellationToken, Task<TableResult>> _copy;

        public string Name { get; }

        private TableDefinition(
            string name,
            Func<AppDbContext, CancellationToken, Task<int>> sourceCount,
            Func<AppDbContext, CancellationToken, Task<int>> targetCount,
            Func<AppDbContext, AppDbContext, int, ILogger, CancellationToken, Task<TableResult>> copy)
        {
            Name = name;
            _sourceCount = sourceCount;
            _targetCount = targetCount;
            _copy = copy;
        }

        public static TableDefinition Create<TEntity>(
            string name,
            Expression<Func<TEntity, object>> orderBy)
            where TEntity : class
        {
            return new TableDefinition(
                name,
                (source, ct) => source.Set<TEntity>().CountAsync(ct),
                (target, ct) => target.Set<TEntity>().CountAsync(ct),
                async (source, target, batchSize, logger, ct) =>
                {
                    var sourceCount = await source.Set<TEntity>().CountAsync(ct);
                    var copied = 0;
                    for (var offset = 0; offset < sourceCount; offset += batchSize)
                    {
                        var batch = await source.Set<TEntity>()
                            .AsNoTracking()
                            .OrderBy(orderBy)
                            .Skip(offset)
                            .Take(batchSize)
                            .ToListAsync(ct);

                        NormalizeTemporalValues(batch);

                        var executionStrategy = target.Database.CreateExecutionStrategy();
                        await executionStrategy.ExecuteAsync(async () =>
                        {
                            await using var transaction = await target.Database.BeginTransactionAsync(ct);
                            target.Set<TEntity>().AddRange(batch);
                            await target.SaveChangesAsync(ct);
                            await transaction.CommitAsync(ct);
                        });

                        copied += batch.Count;
                        target.ChangeTracker.Clear();
                        source.ChangeTracker.Clear();

                        logger.LogInformation(
                            "Migrated {BatchCount} records for {Table}. Total migrated: {MigratedCount}/{SourceCount}.",
                            batch.Count,
                            name,
                            copied,
                            sourceCount);
                    }

                    var targetCount = await target.Set<TEntity>().CountAsync(ct);
                    return new TableResult(name, sourceCount, targetCount);
                });
        }

        public Task<int> CountTargetAsync(AppDbContext target, CancellationToken cancellationToken)
            => _targetCount(target, cancellationToken);

        public Task<TableResult> CopyAsync(
            AppDbContext source,
            AppDbContext target,
            int batchSize,
            ILogger logger,
            CancellationToken cancellationToken)
            => _copy(source, target, batchSize, logger, cancellationToken);
    }

    private AppDbContext CreateSqliteContext(IDatabaseConnectionFactory factory)
    {
        var connection = factory.CreateSqliteConnection();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection, sqlite => sqlite.CommandTimeout(_databaseOptions.Sqlite.CommandTimeoutSeconds))
            .Options;
        return new AppDbContext(options);
    }

    private AppDbContext CreatePostgreSqlContext(IDatabaseConnectionFactory factory)
    {
        var connection = factory.CreatePostgreSqlConnection();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection, npgsql =>
            {
                npgsql.CommandTimeout(_databaseOptions.PostgreSql.CommandTimeoutSeconds);
                npgsql.EnableRetryOnFailure(
                    _databaseOptions.PostgreSql.MaxRetryCount,
                    TimeSpan.FromSeconds(_databaseOptions.PostgreSql.MaxRetryDelaySeconds),
                    errorCodesToAdd: null);
            })
            .Options;
        return new AppDbContext(options);
    }

    private static void NormalizeTemporalValues<TEntity>(IEnumerable<TEntity> batch)
    {
        var properties = typeof(TEntity).GetProperties()
            .Where(property => property.CanRead && property.CanWrite)
            .ToArray();

        foreach (var entity in batch)
        {
            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(DateTime))
                {
                    var value = (DateTime)property.GetValue(entity)!;
                    property.SetValue(entity, NormalizeDateTime(value));
                    continue;
                }

                if (property.PropertyType == typeof(DateTime?))
                {
                    var value = (DateTime?)property.GetValue(entity);
                    if (value.HasValue)
                    {
                        property.SetValue(entity, NormalizeDateTime(value.Value));
                    }

                    continue;
                }

                if (property.PropertyType == typeof(DateTimeOffset))
                {
                    var value = (DateTimeOffset)property.GetValue(entity)!;
                    property.SetValue(entity, value.ToUniversalTime());
                    continue;
                }

                if (property.PropertyType == typeof(DateTimeOffset?))
                {
                    var value = (DateTimeOffset?)property.GetValue(entity);
                    if (value.HasValue)
                    {
                        property.SetValue(entity, value.Value.ToUniversalTime());
                    }
                }
            }
        }
    }

    private static DateTime NormalizeDateTime(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value
        };
    }
}
