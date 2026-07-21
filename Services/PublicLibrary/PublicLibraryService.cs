using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.PublicLibrary;

// Truy vấn chỉ đọc cho /Library: chỉ lấy set public + Active, không expose email tác giả.
public sealed class PublicLibraryService : IPublicLibraryService
{
    private const int PageSize = 12;

    private readonly AppDbContext _db;

    public PublicLibraryService(AppDbContext db) => _db = db;

    public async Task<PublicLibraryResult> BrowseAsync(
        PublicLibraryQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<FlashcardSet> visibleSets = _db.FlashcardSets
            .AsNoTracking()
            .Where(set => set.IsPublic &&
                set.ModerationStatus == FlashcardSetModerationStatus.Active);

        // Thống kê aggregate trên toàn bộ thư viện công khai, không phụ thuộc bộ lọc tìm kiếm.
        PublicLibrarySummary summary = new(
            await visibleSets.CountAsync(cancellationToken),
            await _db.Flashcards.AsNoTracking().CountAsync(
                card => visibleSets.Any(set => set.Id == card.FlashcardSetId),
                cancellationToken),
            await _db.FlashcardSets.AsNoTracking().CountAsync(
                copy => copy.SourceSetId.HasValue &&
                    visibleSets.Any(set => set.Id == copy.SourceSetId.Value),
                cancellationToken));

        string? search = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : query.Search.Trim().ToLowerInvariant();

        // Lọc và sắp xếp ở tầng entity để EF dịch được sang SQL;
        // sắp xếp trên DTO sau projection không dịch được (subquery đếm trong OrderBy).
        IQueryable<FlashcardSet> filtered = visibleSets;
        if (search != null)
        {
            filtered = filtered.Where(set =>
                set.Title.ToLower().Contains(search) ||
                (set.Description != null && set.Description.ToLower().Contains(search)) ||
                _db.Users.Any(author => author.Id == set.UserId &&
                    author.UserName != null && author.UserName.ToLower().Contains(search)));
        }

        int totalItems = await filtered.CountAsync(cancellationToken);
        string sort = PublicLibrarySort.Normalize(query.Sort);

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

        int totalPages = (totalItems + PageSize - 1) / PageSize;
        int page = totalPages == 0 ? 1 : Math.Clamp(query.Page, 1, totalPages);

        IQueryable<PublicLibrarySetItem> projected =
            from set in ordered.Skip((page - 1) * PageSize).Take(PageSize)
            join author in _db.Users.AsNoTracking() on set.UserId equals author.Id into authors
            from author in authors.DefaultIfEmpty()
            select new PublicLibrarySetItem(
                set.Id,
                set.Title,
                set.Description,
                author != null && author.UserName != null ? author.UserName : "Thành viên",
                set.Flashcards.Count,
                _db.FlashcardSets.Count(copy => copy.SourceSetId == set.Id),
                set.UpdatedAt);

        List<PublicLibrarySetItem> items = await projected.ToListAsync(cancellationToken);

        // LEFT JOIN ngoài subquery phân trang không bảo đảm thứ tự;
        // áp lại đúng comparator trên tối đa 12 dòng của trang.
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
