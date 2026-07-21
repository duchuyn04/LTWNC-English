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

        IQueryable<PublicLibrarySetItem> projected =
            from set in visibleSets
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

        string? search = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : query.Search.Trim().ToLowerInvariant();
        if (search != null)
        {
            projected = projected.Where(item =>
                item.Title.ToLower().Contains(search) ||
                (item.Description != null && item.Description.ToLower().Contains(search)) ||
                item.AuthorName.ToLower().Contains(search));
        }

        int totalItems = await projected.CountAsync(cancellationToken);
        string sort = PublicLibrarySort.Normalize(query.Sort);

        IQueryable<PublicLibrarySetItem> ordered = sort switch
        {
            PublicLibrarySort.Recent => projected
                .OrderByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Id),
            PublicLibrarySort.Cards => projected
                .OrderByDescending(item => item.CardCount)
                .ThenByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Id),
            _ => projected
                .OrderByDescending(item => item.CopyCount)
                .ThenByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Id)
        };

        int totalPages = (totalItems + PageSize - 1) / PageSize;
        int page = totalPages == 0 ? 1 : Math.Clamp(query.Page, 1, totalPages);
        List<PublicLibrarySetItem> items = await ordered
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

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
