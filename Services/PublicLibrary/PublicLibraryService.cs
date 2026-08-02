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
        //Lấy các bộ thẻ được phép hiển thị
        // 1. Gọi `Where` và lưu kết quả vào `visibleSets`.
        IQueryable<FlashcardSet> visibleSets = _db.FlashcardSets
            .AsNoTracking()
            .Where(set => set.IsPublic &&
                set.ModerationStatus == FlashcardSetModerationStatus.Active);

        /*Tính thống kê toàn thư viện
         Thống kê aggregate trên toàn bộ thư viện công khai, không phụ thuộc bộ lọc tìm kiếm.
         2. Khởi tạo `summary` với dữ liệu ban đầu cần thiết.
        */
        PublicLibrarySummary summary = new(
            //1. Tổng số bộ flashcard công khai
            await visibleSets.CountAsync(cancellationToken),

            //2.Tổng số flashcard trong các bộ công khai
            await _db.Flashcards.AsNoTracking().CountAsync(
                card => visibleSets.Any(set => set.Id == card.FlashcardSetId),
                cancellationToken),

            //3. Tổng số bộ flashcard được sao chép từ các bộ công khai
            await _db.FlashcardSets.AsNoTracking().CountAsync(
                copy => copy.SourceSetId.HasValue &&
                    visibleSets.Any(set => set.Id == copy.SourceSetId.Value),
                cancellationToken));

        // 3. Tính giá trị và lưu vào `search` để dùng ở bước tiếp theo.
        // Giữ nguyên từ khóa người dùng nhập.
        string? search = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : query.Search;

        // Khởi tạo truy vấn tìm kiếm từ các bộ flashcard công khai và đang hoạt động.
        IQueryable<FlashcardSet> filtered = visibleSets;


        if (search != null)
        {
            // Lọc bộ thẻ theo tiêu đề, mô tả
            filtered = filtered.Where(set =>
                set.Title.Contains(search) ||
                (set.Description != null && set.Description.Contains(search)) ||
                //Tìm theo tên tác giả
                _db.AppUsers.Any(author => author.Id == set.UserId &&
                    author.UserName != null && author.UserName.Contains(search)));

         ///Khi có từ khóa tìm kiếm, chỉ giữ những bộ flashcard mà từ khóa xuất hiện trong tiêu đề, mô tả hoặc tên của người tạo bộ thẻ.
        }

        // Gọi `CountAsync` và lưu kết quả vào `totalItems`.
        int totalItems = await filtered.CountAsync(cancellationToken);
        // Gọi `Normalize` và lưu kết quả vào `sort`.
        string sort = PublicLibrarySort.Normalize(query.Sort);


        IQueryable<FlashcardSet> ordered = sort switch
        {
            // Các trường hợp sắp xếp

            // Sắp xếp theo thời gian cập nhật mới nhất
            PublicLibrarySort.Recent => filtered
                .OrderByDescending(set => set.UpdatedAt)
                .ThenBy(set => set.Id), //Nếu hai bộ thẻ có cùng thời gian cập nhật,sắp xếp theo Id tăng dần.

            // Sắp xếp theo số lượng flashcard giảm dần.
            PublicLibrarySort.Cards => filtered
                .OrderByDescending(set => set.Flashcards.Count)
                .ThenByDescending(set => set.UpdatedAt)
                .ThenBy(set => set.Id),

            // Mặc định sắp xếp theo số lượt sao chép giảm dần.
            _ => filtered //trường hợp mặc định
                .OrderByDescending(set => _db.FlashcardSets.Count(copy => copy.SourceSetId == set.Id)) //Số lượt sao chép
                .ThenByDescending(set => set.UpdatedAt) //Thời gian cập nhật
                .ThenBy(set => set.Id) //ID tăng dần
        };

        // Tính tổng số trang bằng phép chia làm tròn lên
        /*
         totalItems = 13
         PageSize = 12
        (13 + 12 - 1) / 12
        = 24 / 12
        = 2 => Kết quả là 2 trang
         */
        int totalPages = (totalItems + PageSize - 1) / PageSize;

        // Chuẩn hóa trang yêu cầu về phạm vi hợp lệ;
        // nếu không có kết quả thì vẫn dùng trang 1 làm trạng thái mặc định.
        int page;
        if (totalPages == 0)
        {
            page = 1;
        }
        else
        {
            page = Math.Clamp(query.Page, 1, totalPages);
        }

        //Tạo truy vấn dữ liệu dùng để hiển thị trên trang thư viện
        IQueryable<PublicLibrarySetItem> projected =

            // Lấy các bộ flashcard thuộc trang hiện tại
            from set in ordered.Skip((page - 1) * PageSize).Take(PageSize)
            //Ghép bộ flashcard với tài khoản tác giả
            join author in _db.AppUsers.AsNoTracking() on set.UserId equals author.Id into authors
            // Dùng LEFT JOIN để vẫn giữ bộ thẻ nếu không tìm thấy tác giả
            from author in authors.DefaultIfEmpty()
            //Chỉ lấy các trường cần thiết cho giao diện
            select new PublicLibrarySetItem(
                set.Id,
                set.Title,
                set.Description,

                //Dùng tên mặc định nếu không có thông tin tác giả
                author != null && author.UserName != null ? author.UserName : "Thành viên",
                // Username thật để chỉ tạo link khi tác giả còn tồn tại.
                author != null ? author.UserName : null,
                // Số flashcard trong bộ
                set.Flashcards.Count,
                //Số bộ thẻ được sao chép từ bộ hiện tại
                _db.FlashcardSets.Count(copy => copy.SourceSetId == set.Id),
                set.UpdatedAt);

        //Thực thi truy vấn và tải dữ liệu của trang hiện tại vào bộ nhớ
        List<PublicLibrarySetItem> items = await projected.ToListAsync(cancellationToken);

        //Sắp xếp lại dữ liệu của trang hiện tại để bảo đảm đúng thứ tự hiển thị
        items = (sort switch
        {
            //Ưu tiên bộ thẻ được cập nhật gần đây nhất
            PublicLibrarySort.Recent => items
                .OrderByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Id),
            //Ưu tiên bộ có nhiều flashcard nhất
            PublicLibrarySort.Cards => items
                .OrderByDescending(item => item.CardCount)
                .ThenByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Id),

            //Mặc định ưu tiên bộ được sao chép nhiều nhất
            _ => items
                .OrderByDescending(item => item.CopyCount)
                .ThenByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Id)
        }).ToList();

        //Đóng gói trạng thái tìm kiếm, phân trang, thống kê và dữ liệu hiển thị
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
