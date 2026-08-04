using LynqMentrics.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace LynqMentrics.Services;

public sealed class DashboardRealtimeNotifier(IHubContext<DashboardHub> hubContext) : IDashboardRealtimeNotifier
{
    public Task NotifyLinksChangedAsync(string userId, string reason, CancellationToken cancellationToken = default)
    {
        return hubContext.Clients
            .Group(DashboardHub.GetUserGroupName(userId))
            .SendAsync(
                "LinksChanged",
                new
                {
                    reason,
                    occurredAtUtc = DateTime.UtcNow
                },
                cancellationToken);
    }

    public async Task NotifyLinkStatsChangedAsync(string userId, Guid linkId, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            linkId,
            occurredAtUtc = DateTime.UtcNow
        };

        await hubContext.Clients
            .Group(DashboardHub.GetLinkGroupName(linkId))
            .SendAsync("LinkStatsChanged", payload, cancellationToken);

        await hubContext.Clients
            .Group(DashboardHub.GetUserGroupName(userId))
            .SendAsync("LinkStatsChanged", payload, cancellationToken);
    }
}
