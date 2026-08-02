using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LynqMentrics.Pages.Dashboard;

[Authorize]
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
