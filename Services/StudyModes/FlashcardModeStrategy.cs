using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services.StudyModes;

// Strategy cho chế độ học Flashcard: lật thẻ để ghi nhớ
public class FlashcardModeStrategy : IStudyModeStrategy
{
    // Chế độ này phụ trách Flashcard
    public StudyMode Mode => StudyMode.Flashcard;

    // Lấy tất cả thẻ trong bộ, sau đó lọc theo sao / chưa thuộc nếu cần
    public async Task<List<Flashcard>> GetCards(
        int setId,
        UserStudySettings settings,
        string? userId,
        AppDbContext context)
    {
        var query = context.Flashcards.Where(f => f.FlashcardSetId == setId);

        // Chỉ lấy thẻ đã gắn sao nếu ngườ dùng chọn "Chỉ đã sao"
        if (settings.StarredOnly)
        {
            query = query.Where(f => f.IsStarred);
        }

        // Chỉ lấy thẻ chưa thuộc nếu ngườ dùng chọn "Chỉ chưa thuộc"
        // Cần userId để biết thẻ nào họ đã học
        if (settings.UnlearnedOnly && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(f => !context.UserProgresses.Any(p =>
                p.UserId == userId &&
                p.FlashcardId == f.Id &&
                p.IsLearned));
        }

        return await query.OrderBy(f => f.OrderIndex).ToListAsync();
    }

    // Tạo thông tin hiển thị chế độ Flashcard trên Study Hub
    public StudyModeOptionViewModel BuildOption(
        int setId,
        List<Flashcard> cards,
        UserStudySettings settings)
    {
        return new StudyModeOptionViewModel
        {
            Mode = StudyMode.Flashcard,
            Name = "Flashcard",
            Description = "Lật thẻ và ghi nhớ",
            IconClass = "ph-cards",
            ActionUrl = $"/Study/{setId}/Flashcard",
            IsAvailable = cards.Any(),           // Có thẻ mới cho học
            CardCount = cards.Count,             // Số thẻ sau khi lọc
            EstimatedSeconds = cards.Count * 15  // Ước tính 15 giây/thẻ
        };
    }
}
