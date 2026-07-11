using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Services.StudyModes;

// Thực hiện bộ lọc dùng chung cho mọi chế độ học:
// - thuộc đúng bộ thẻ
// - chỉ thẻ đánh sao (nếu bật)
// - chỉ thẻ chưa thuộc (nếu bật và có userId)
// Không thực hiện query sớm — trả về IQueryable để mỗi strategy thêm điều kiện riêng.
public class StudyCardQueryService : IStudyCardQueryService
{
    private readonly AppDbContext _context;

    public StudyCardQueryService(AppDbContext context)
    {
        _context = context;
    }

    public IQueryable<Flashcard> CreateFilteredQuery(
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        // Bắt đầu từ toàn bộ thẻ của bộ thẻ chỉ định
        var query = _context.Flashcards.Where(f => f.FlashcardSetId == setId);

        // Lọc "Chỉ đã sao": bỏ thẻ chưa gắn sao
        if (settings.StarredOnly)
        {
            query = query.Where(f => f.IsStarred);
        }

        // Lọc "Chỉ chưa thuộc": bỏ thẻ đã có UserProgress.IsLearned = true
        // Chỉ áp dụng khi biết userId; user ẩn danh không có tiến trình cá nhân
        if (settings.UnlearnedOnly && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(f => !_context.UserProgresses.Any(p =>
                p.UserId == userId &&
                p.FlashcardId == f.Id &&
                p.IsLearned));
        }

        return query;
    }
}
