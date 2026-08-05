using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc.RazorPages;


namespace LynqMentrics.Pages.Dashboard;

[EnableCors("SignalRPolicy")]
[Authorize]
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
