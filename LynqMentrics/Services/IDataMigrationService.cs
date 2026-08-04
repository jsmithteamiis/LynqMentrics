namespace LynqMentrics.Services;

public interface IDataMigrationService
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
