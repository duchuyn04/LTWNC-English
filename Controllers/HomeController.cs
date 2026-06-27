using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ltwnc.Models;
using ltwnc.Services;
using ltwnc.Models.ViewModels.Home;

namespace ltwnc.Controllers;

// Controller xử lý trang chủ và tìm kiếm
public class HomeController : Controller
{
    private readonly FlashcardSetService _setService;

    // Inject service xử lý bộ thẻ flashcard
    public HomeController(FlashcardSetService setService)
    {
        _setService = setService;
    }

    // Hiển thị trang chủ với danh sách bộ thẻ public
    // Tham số q: từ khóa tìm kiếm (nếu có)
    public async Task<IActionResult> Index(string? q)
    {
        var model = new HomeViewModel();

        if (!string.IsNullOrEmpty(q))
        {
            // Có từ khóa → tìm kiếm bộ thẻ theo tiêu đề
            model.SearchQuery = q;
            model.PublicSets = await _setService.SearchPublicSetsAsync(q);
        }
        else
        {
            // Không có từ khóa → hiển thị bộ thẻ public mới nhất
            model.PublicSets = await _setService.GetPublicSetsAsync();
        }

        return View(model);
    }

    // Trang chính sách bảo mật
    public IActionResult Privacy()
    {
        return View();
    }

    // Trang lỗi chung — không cache để luôn hiển thị lỗi mới nhất
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
