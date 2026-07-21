using ltwnc.Services.PublicLibrary;

namespace ltwnc.Models.ViewModels.Library;

// View model thẻ bộ trong thư viện công khai — chỉ giữ dữ liệu hiển thị, không có email tác giả.
public sealed class LibrarySetCardViewModel
{
    private static readonly string[] Accents = ["sage", "brass", "clay", "sky", "plum", "moss"];

    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public int CardCount { get; init; }
    public int CopyCount { get; init; }
    public DateTime UpdatedAt { get; init; }

    // Chữ cái đầu tên tác giả (tối đa 2), fallback "TV" (Thành viên) khi không tính được.
    public string AuthorInitials
    {
        get
        {
            string initials = string.Join(string.Empty, AuthorName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(part => char.ToUpperInvariant(part[0])));
            return string.IsNullOrEmpty(initials) ? "TV" : initials;
        }
    }

    // Accent màu xoay vòng theo Id để lưới thẻ không đơn sắc.
    public string AccentClass => $"library-accent-{Accents[Math.Abs(Id) % Accents.Length]}";
}

// View model trang /Library — giữ nguyên query đã chuẩn hoá và metadata phân trang.
public sealed class LibraryIndexViewModel
{
    public string? Search { get; init; }
    public string Sort { get; init; } = PublicLibrarySort.Popular;
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public PublicLibrarySummary Summary { get; init; } = new(0, 0, 0);
    public IReadOnlyList<LibrarySetCardViewModel> Items { get; init; } = [];

    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public static LibraryIndexViewModel FromResult(PublicLibraryResult result) => new()
    {
        Search = result.Search,
        Sort = result.Sort,
        Page = result.Page,
        PageSize = result.PageSize,
        TotalItems = result.TotalItems,
        TotalPages = result.TotalPages,
        Summary = result.Summary,
        Items = result.Items
            .Select(item => new LibrarySetCardViewModel
            {
                Id = item.Id,
                Title = item.Title,
                Description = item.Description,
                AuthorName = item.AuthorName,
                CardCount = item.CardCount,
                CopyCount = item.CopyCount,
                UpdatedAt = item.UpdatedAt
            })
            .ToList()
    };
}
