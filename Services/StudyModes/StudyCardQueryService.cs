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
        _context = context;
    }

    // Bắt đầu từ thẻ của setId, gắn StarredOnly / UnlearnedOnly nếu bật
    public IQueryable<Flashcard> CreateFilteredQuery(
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        IQueryable<Flashcard> query = _context.Flashcards
            .Where(flashcard => flashcard.FlashcardSetId == setId);

        // Chỉ thẻ đã gắn sao
        if (settings.StarredOnly)
        {
            query = query.Where(flashcard => flashcard.IsStarred);
        }

        // Chỉ thẻ chưa thuộc: loại thẻ có progress IsLearned = true của user này
        if (settings.UnlearnedOnly && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(flashcard =>
                !_context.UserProgresses.Any(progress =>
                    progress.UserId == userId
                    && progress.FlashcardId == flashcard.Id
                    && progress.IsLearned));
        }

        return query;
    }
}
