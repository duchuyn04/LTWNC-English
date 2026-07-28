using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.PublicLibrary;

// Truy vấn chỉ đọc cho /Library: chỉ lấy set public + Active, không expose email tác giả.
public sealed class PublicLibraryService : IPublicLibraryService
{
    private const int PageSize = 12;

    private readonly AppDbContext _db;

    public PublicLibraryService(AppDbContext db)
    {
        // 1. Lưu dependency `_db` để các phương thức khác sử dụng.
        _db = db;
    }

    public async Task<PublicLibraryResult> BrowseAsync(
        PublicLibraryQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `Where` và lưu kết quả vào `visibleSets`.
        IQueryable<FlashcardSet> visibleSets = _db.FlashcardSets
            .AsNoTracking()
            .Where(set => set.IsPublic &&
                set.ModerationStatus == FlashcardSetModerationStatus.Active);

        // Thống kê aggregate trên toàn bộ thư viện công khai, không phụ thuộc bộ lọc tìm kiếm.
        // 2. Khởi tạo `summary` với dữ liệu ban đầu cần thiết.
        PublicLibrarySummary summary = new(
            await visibleSets.CountAsync(cancellationToken),
            await _db.Flashcards.AsNoTracking().CountAsync(
                card => visibleSets.Any(set => set.Id == card.FlashcardSetId),
                cancellationToken),
            await _db.FlashcardSets.AsNoTracking().CountAsync(
                copy => copy.SourceSetId.HasValue &&
                    visibleSets.Any(set => set.Id == copy.SourceSetId.Value),
                cancellationToken));

        // 3. Tính giá trị và lưu vào `search` để dùng ở bước tiếp theo.
        string? search = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : query.Search.Trim().ToLowerInvariant();

        // Lọc và sắp xếp ở tầng entity để EF dịch được sang SQL;
        // sắp xếp trên DTO sau projection không dịch được (subquery đếm trong OrderBy).
        // 4. Tính giá trị và lưu vào `filtered` để dùng ở bước tiếp theo.
        IQueryable<FlashcardSet> filtered = visibleSets;
        // 5. Kiểm tra `search != null` để chọn nhánh xử lý phù hợp.
        if (search != null)
        {
            // 6. Cập nhật `filtered` bằng giá trị mới.
            filtered = filtered.Where(set =>
                set.Title.ToLower().Contains(search) ||
                (set.Description != null && set.Description.ToLower().Contains(search)) ||
                _db.AppUsers.Any(author => author.Id == set.UserId &&
                    author.UserName != null && author.UserName.ToLower().Contains(search)));
        }

        // 7. Gọi `CountAsync` và lưu kết quả vào `totalItems`.
        int totalItems = await filtered.CountAsync(cancellationToken);
        // 8. Gọi `Normalize` và lưu kết quả vào `sort`.
        string sort = PublicLibrarySort.Normalize(query.Sort);

        // 9. Tính giá trị và lưu vào `ordered` để dùng ở bước tiếp theo.
        IQueryable<FlashcardSet> ordered = sort switch
        {
            PublicLibrarySort.Recent => filtered
                .OrderByDescending(set => set.UpdatedAt)
                .ThenBy(set => set.Id),
            PublicLibrarySort.Cards => filtered
                .OrderByDescending(set => set.Flashcards.Count)
                .ThenByDescending(set => set.UpdatedAt)
                .ThenBy(set => set.Id),
            _ => filtered
                .OrderByDescending(set => _db.FlashcardSets.Count(copy => copy.SourceSetId == set.Id))
                .ThenByDescending(set => set.UpdatedAt)
                .ThenBy(set => set.Id)
        };

        // 10. Tính giá trị và lưu vào `totalPages` để dùng ở bước tiếp theo.
        int totalPages = (totalItems + PageSize - 1) / PageSize;
        // 11. Tính giá trị và lưu vào `page` để dùng ở bước tiếp theo.
        int page = totalPages == 0 ? 1 : Math.Clamp(query.Page, 1, totalPages);

        // 12. Tính giá trị và lưu vào `projected` để dùng ở bước tiếp theo.
        IQueryable<PublicLibrarySetItem> projected =
            from set in ordered.Skip((page - 1) * PageSize).Take(PageSize)
            join author in _db.AppUsers.AsNoTracking() on set.UserId equals author.Id into authors
            from author in authors.DefaultIfEmpty()
            select new PublicLibrarySetItem(
                set.Id,
                set.Title,
                set.Description,
                author != null && author.UserName != null ? author.UserName : "Thành viên",
                set.Flashcards.Count,
                _db.FlashcardSets.Count(copy => copy.SourceSetId == set.Id),
                set.UpdatedAt);

        // 13. Gọi `ToListAsync` và lưu kết quả vào `items`.
        List<PublicLibrarySetItem> items = await projected.ToListAsync(cancellationToken);

        // LEFT JOIN ngoài subquery phân trang không bảo đảm thứ tự;
        // áp lại đúng comparator trên tối đa 12 dòng của trang.
        // 14. Cập nhật `items` bằng giá trị mới.
        items = (sort switch
        {
            PublicLibrarySort.Recent => items
                .OrderByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Id),
            PublicLibrarySort.Cards => items
                .OrderByDescending(item => item.CardCount)
                .ThenByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Id),
            _ => items
                .OrderByDescending(item => item.CopyCount)
                .ThenByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Id)
        }).ToList();

        // 15. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new PublicLibraryResult(
            search,
            sort,
            page,
            PageSize,
            totalItems,
            totalPages,
            summary,
            items);
    }
}
