using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

// PROTOTYPE ONLY: route thử nghiệm UI, không dùng dữ liệu thật và không đưa thẳng vào production.
[AllowAnonymous]
public sealed class LibraryPrototypeController : Controller
{
    [HttpGet("/prototype/library")]
    public IActionResult Index() => View("~/Views/Prototype/Library.cshtml");
}
