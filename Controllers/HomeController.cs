using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ltwnc.Models;
using ltwnc.Models.Entities;
using ltwnc.Services.FlashcardSets;
using ltwnc.Models.ViewModels.Home;
using ltwnc.Services.Credits;

namespace ltwnc.Controllers;

// Hiển thị trang chủ công khai, trang quyền riêng tư và các trang báo lỗi.
public class HomeController : Controller
{
    // Service lấy và tìm kiếm các bộ thẻ công khai.
    private readonly IFlashcardSetService _setService;
    private readonly ICreditService _creditService;

    // Nhận service bộ thẻ qua dependency injection.
    public HomeController(IFlashcardSetService setService, ICreditService creditService)
    {
        // 1. Lưu service bộ thẻ để trang chủ sử dụng.
        _setService = setService;
        _creditService = creditService;
    }

    // Hiển thị trang chủ cho cả khách và người đã đăng nhập, có hỗ trợ tìm kiếm.
    public async Task<IActionResult> Index(string? q)
    {
        // 1. Kiểm tra có từ khóa tìm kiếm hay không.
        // 2. Lấy bộ thẻ công khai phù hợp và ánh xạ sang ViewModel.
        // 3. Hiển thị trang chủ cùng danh sách kết quả.
        HomeViewModel model = new HomeViewModel();
        List<FlashcardSet> publicSets;

        if (!string.IsNullOrEmpty(q))
        {
            // Có từ khóa: tìm theo tiêu đề
            model.SearchQuery = q;
            publicSets = await _setService.SearchPublicSetsAsync(q);
        }
        else
        {
            // Không có từ khóa: lấy các bộ thẻ công khai mới nhất.
            publicSets = await _setService.GetPublicSetsAsync();
        }

        model.PublicSets = publicSets
            .Select(set => new PublicSetViewModel
            {
                Id = set.Id,
                Title = set.Title,
                Description = set.Description
            })
            .ToList();

        model.CreditPackages = (await _creditService.GetActivePackagesAsync())
            .Select(package => new HomeCreditPackageViewModel
            {
                Id = package.Id,
                Name = package.Name,
                Description = package.Description,
                PriceVnd = package.PriceVnd,
                Credits = package.Credits
            })
            .ToList();

        return View(model);
    }

    // Hiển thị trang chính sách quyền riêng tư.
    public IActionResult Privacy()
    {
        // 1. Hiển thị view chứa chính sách quyền riêng tư.
        return View();
    }

    // Chọn trang 403 hoặc 404 dựa trên mã lỗi ban đầu của request.
    [HttpGet]
    [AllowAnonymous]
    public IActionResult StatusCodePage()
    {
        // 1. Đọc mã lỗi gốc trước khi request được chuyển tới action này.
        // 2. Hiển thị trang 403 nếu bị cấm truy cập.
        // 3. Các trường hợp còn lại hiển thị trang 404.
        int originalStatusCode = HttpContext.Features
            .Get<IStatusCodeReExecuteFeature>()?
            .OriginalStatusCode ?? StatusCodes.Status404NotFound;

        if (originalStatusCode == StatusCodes.Status403Forbidden)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View("Forbidden");
        }

        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    // Hiển thị lỗi hệ thống, tắt cache và kèm mã request để hỗ trợ truy vết.
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        // 1. Lấy mã Activity hiện tại hoặc mã truy vết của request.
        // 2. Gắn mã này vào ViewModel để hỗ trợ tìm lỗi trong log.
        // 3. Hiển thị trang lỗi hệ thống.
        ErrorViewModel model = new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        };
        return View(model);
    }
}
