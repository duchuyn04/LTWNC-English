using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ltwnc.Models;
using ltwnc.Services.FlashcardSets;
using ltwnc.Models.ViewModels.Home;

namespace ltwnc.Controllers;

// Trang chủ (khách) và trang lỗi. User đã login vào /Set luôn.
public class HomeController : Controller
{
    // Lấy / tìm bộ thẻ public
    private readonly IFlashcardSetService _setService;

    // Inject service bộ thẻ
    public HomeController(IFlashcardSetService setService)
    {
        _setService = setService;
    }

    // GET / : nếu đã login redirect /Set; không thì list public hoặc search theo q
    public async Task<IActionResult> Index(string? q)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect("/Set");
        }

        HomeViewModel model = new HomeViewModel();

        if (!string.IsNullOrEmpty(q))
        {
            // Có từ khóa: tìm theo tiêu đề
            model.SearchQuery = q;
            model.PublicSets = await _setService.SearchPublicSetsAsync(q);
        }
        else
        {
            // Không có q: vài bộ public mới nhất
            model.PublicSets = await _setService.GetPublicSetsAsync();
        }

        return View(model);
    }

    // GET Privacy
    public IActionResult Privacy()
    {
        return View();
    }

    // GET Error: không cache, gắn RequestId để debug
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ErrorViewModel model = new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        };
        return View(model);
    }
}
