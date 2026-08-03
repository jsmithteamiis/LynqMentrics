using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LynqMentrics.Areas.Identity.Pages
{
    public abstract class IdentityPageModel : PageModel
    {
        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }
    }
}