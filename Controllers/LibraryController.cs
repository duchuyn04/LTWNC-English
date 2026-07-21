using ltwnc.Models.ViewModels.Library;
using ltwnc.Services.PublicLibrary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

// Trang thư viện cộng đồng /Library — truy cập ẩn danh, chỉ đọc dữ liệu qua IPublicLibraryService.
[AllowAnonymous]
public sealed class LibraryController : Controller
{
    private readonly IPublicLibraryService _libraryService;

    public LibraryController(IPublicLibraryService libraryService) =>
        _libraryService = libraryService;

    [HttpGet("/Library")]
    public async Task<IActionResult> Index(
        string? q,
        string? sort,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        PublicLibraryResult result = await _libraryService.BrowseAsync(
            new PublicLibraryQuery(q, sort, page),
            cancellationToken);
        return View(LibraryIndexViewModel.FromResult(result));
    }
}
