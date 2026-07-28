using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Services.StudyModes;

// Lọc thẻ chung: đúng bộ, có thể chỉ sao / chỉ chưa thuộc.
// Không ToList ở đây; strategy tự thêm điều kiện rồi materialize.
public class StudyCardQueryService : IStudyCardQueryService
{
    // Nguồn Flashcards và UserProgresses
    private readonly AppDbContext _context;

    // Inject DbContext
    public StudyCardQueryService(AppDbContext context)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
    }

    // Bắt đầu từ thẻ của setId, gắn StarredOnly / UnlearnedOnly nếu bật
    public IQueryable<Flashcard> CreateFilteredQuery(
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        // 1. Gọi `Where` và lưu kết quả vào `query`.
        IQueryable<Flashcard> query = _context.Flashcards
            .Where(flashcard => flashcard.FlashcardSetId == setId);

        // Chỉ thẻ đã gắn sao
        // 2. Kiểm tra `settings.StarredOnly` để chọn nhánh xử lý phù hợp.
        if (settings.StarredOnly)
        {
            // 3. Cập nhật `query` bằng giá trị mới.
            query = query.Where(flashcard => flashcard.IsStarred);
        }

        // Chỉ thẻ chưa thuộc: loại thẻ có progress IsLearned = true của user này
        // 4. Kiểm tra `settings.UnlearnedOnly && !string.IsNullOrWhiteSpace(userId)` để chọn nhánh xử lý phù hợp.
        if (settings.UnlearnedOnly && !string.IsNullOrWhiteSpace(userId))
        {
            // 5. Cập nhật `query` bằng giá trị mới.
            query = query.Where(flashcard =>
                !_context.UserProgresses.Any(progress =>
                    progress.UserId == userId
                    && progress.FlashcardId == flashcard.Id
                    && progress.IsLearned));
        }

        // 6. Trả `query` cho nơi gọi.
        return query;
    }
}
