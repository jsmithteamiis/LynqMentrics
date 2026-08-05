using System.Security.Claims;
using LynqMentrics.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LynqMentrics.Hubs;

[Authorize]
[EnableCors("SignalRPolicy")]
public sealed class DashboardHub(AppDbContext dbContext) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new HubException("Authenticated user id was not found.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId));

        var linkIdRaw = Context.GetHttpContext()?.Request.Query["linkId"].ToString();
        if (Guid.TryParse(linkIdRaw, out var linkId))
        {
            var isOwner = await dbContext.Links
                .AsNoTracking()
                .AnyAsync(link => link.Id == linkId && link.UserId == userId);

            if (isOwner)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetLinkGroupName(linkId));
            }
        }

        await base.OnConnectedAsync();
    }

    public static string GetUserGroupName(string userId) => $"user:{userId}";

    public static string GetLinkGroupName(Guid linkId) => $"link:{linkId:D}";
}
