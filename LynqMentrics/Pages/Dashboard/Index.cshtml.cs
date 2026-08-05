using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc.RazorPages;


namespace LynqMentrics.Pages.Dashboard;

[Authorize]
[EnableCors("SignalRPolicy")]
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
