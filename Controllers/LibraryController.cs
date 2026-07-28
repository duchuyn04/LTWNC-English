using ltwnc.Models.ViewModels.Library;
using ltwnc.Services.PublicLibrary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

// Hiển thị thư viện cộng đồng công khai; khách có thể tìm kiếm, sắp xếp và chuyển trang.
[AllowAnonymous]
public sealed class LibraryController : Controller
{
    // Service chỉ đọc các bộ thẻ công khai trong thư viện cộng đồng.
    private readonly IPublicLibraryService _libraryService;

    // Nhận service thư viện qua dependency injection.
    public LibraryController(IPublicLibraryService libraryService)
    {
        // 1. Lưu service thư viện để action Index sử dụng.
        _libraryService = libraryService;
    }

    // Hiển thị danh sách bộ thẻ theo từ khóa, cách sắp xếp và số trang.
    [HttpGet("/Library")]
    public async Task<IActionResult> Index(
        string? q,
        string? sort,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        // 1. Gom từ khóa, cách sắp xếp và số trang thành truy vấn.
        // 2. Lấy dữ liệu bộ thẻ công khai từ service.
        // 3. Chuyển kết quả sang ViewModel và hiển thị.
        PublicLibraryResult result = await _libraryService.BrowseAsync(
            new PublicLibraryQuery(q, sort, page),
            cancellationToken);
        return View(LibraryIndexViewModel.FromResult(result));
    }
}
