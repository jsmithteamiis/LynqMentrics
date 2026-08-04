using LynqMentrics.Configuration;
using LynqMentrics.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LynqMentrics.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguredDataAccess(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName));

        services.PostConfigure<DatabaseOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.Provider))
            {
                options.Provider = configuration["DatabaseProvider"] ??
                                   (environment.IsDevelopment() ? "Sqlite" : "PostgreSql");
            }
        });

        services.AddSingleton<IDatabaseConnectionFactory, DatabaseConnectionFactory>();

        services.AddDbContext<AppDbContext>((serviceProvider, optionsBuilder) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var provider = DatabaseProviderResolver.Resolve(options.Provider);

            if (provider == DatabaseProvider.PostgreSql)
            {
                ConfigurePostgreSql(
                    optionsBuilder,
                    configuration.GetConnectionString(options.PostgreSql.ConnectionStringName),
                    options.PostgreSql);
                return;
            }

            ConfigureSqlite(
                optionsBuilder,
                configuration.GetConnectionString(options.Sqlite.ConnectionStringName),
                options.Sqlite);
        });

        services.AddScoped<IDataMigrationService, SqliteToPostgreSqlMigrationService>();

        return services;
    }

    private static void ConfigureSqlite(
        DbContextOptionsBuilder optionsBuilder,
        string? connectionString,
        SqliteDatabaseOptions options)
    {
        optionsBuilder.UseSqlite(
            ResolveRequiredConnectionString(connectionString, options.ConnectionStringName),
            sqlite => sqlite.CommandTimeout(options.CommandTimeoutSeconds));
    }

    private static void ConfigurePostgreSql(
        DbContextOptionsBuilder optionsBuilder,
        string? connectionString,
        PostgreSqlDatabaseOptions options)
    {
        optionsBuilder.UseNpgsql(
            ResolveRequiredConnectionString(connectionString, options.ConnectionStringName),
            npgsql =>
            {
                npgsql.CommandTimeout(options.CommandTimeoutSeconds);
                npgsql.EnableRetryOnFailure(
                    options.MaxRetryCount,
                    TimeSpan.FromSeconds(options.MaxRetryDelaySeconds),
                    errorCodesToAdd: null);
            });
    }

    private static string ResolveRequiredConnectionString(string? connectionString, string connectionStringName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Missing connection string '{connectionStringName}'.");
        }

        return connectionString;
    }
}
