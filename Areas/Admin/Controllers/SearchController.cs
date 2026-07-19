using ltwnc.Services.AdminSearch;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

[Area("Admin")]
[Route("Admin/Search")]
public sealed class SearchController : Controller
{
    private readonly IAdminGlobalSearchService _searchService;

    // Nhận service tìm kiếm để controller chỉ xử lý HTTP và render view.
    public SearchController(IAdminGlobalSearchService searchService)
    {
        _searchService = searchService;
    }

    // Hiển thị kết quả tìm kiếm toàn cục trên dữ liệu nhận diện an toàn của Admin.
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? q,
        int perTypeLimit = AdminGlobalSearchService.DefaultPerTypeLimit,
        CancellationToken cancellationToken = default)
    {
        AdminGlobalSearchResult result = await _searchService.SearchAsync(
            new AdminGlobalSearchQuery(q, perTypeLimit),
            cancellationToken);

        return View(result);
    }
}
