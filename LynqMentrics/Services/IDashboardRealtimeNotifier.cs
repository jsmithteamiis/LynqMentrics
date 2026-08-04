namespace LynqMentrics.Services;

public interface IDashboardRealtimeNotifier
{
    Task NotifyLinksChangedAsync(string userId, string reason, CancellationToken cancellationToken = default);

    Task NotifyLinkStatsChangedAsync(string userId, Guid linkId, CancellationToken cancellationToken = default);
}
