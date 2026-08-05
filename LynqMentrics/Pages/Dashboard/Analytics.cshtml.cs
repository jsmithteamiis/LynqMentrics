using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LynqMentrics.Pages.Dashboard;

[EnableCors("SignalRPolicy")]
[Authorize]
public class AnalyticsModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid LinkId { get; set; }

    public IActionResult OnGet()
    {
        return LinkId == Guid.Empty ? RedirectToPage("/Dashboard/Index") : Page();
    }
}
