using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Services.StudyModes;

// Thực hiện bộ lọc dùng chung: setId, chỉ sao, chỉ chưa thuộc.
// Không query sớm — trả về IQueryable để strategy tiếp tục compose.
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
        var query = _context.Flashcards.Where(f => f.FlashcardSetId == setId);

        if (settings.StarredOnly)
        {
            query = query.Where(f => f.IsStarred);
        }

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
