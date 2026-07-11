using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services.StudyModes;

// Strategy cho chế độ học Dictation: nghe câu ví dụ và viết lại từ
public class DictationModeStrategy : IStudyModeStrategy
{
    // Chế độ này phụ trách Dictation
    public StudyMode Mode => StudyMode.Dictation;

    // Lấy thẻ trong bộ, lọc theo sao / chưa thuộc,
    // sau đó chỉ giữ lại thẻ có câu ví dụ để nghe chép
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
        if (settings.UnlearnedOnly && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(f => !context.UserProgresses.Any(p =>
                p.UserId == userId &&
                p.FlashcardId == f.Id &&
                p.IsLearned));
        }

        var cards = await query.OrderBy(f => f.OrderIndex).ToListAsync();

        // Dictation cần câu ví dụ để phát âm, nên loại thẻ không có câu ví dụ
        return cards.Where(c => !string.IsNullOrWhiteSpace(c.ExampleSentence)).ToList();
    }

    // Tạo thông tin hiển thị chế độ Dictation trên Study Hub
    public StudyModeOptionViewModel BuildOption(
        int setId,
        List<Flashcard> cards,
        UserStudySettings settings)
    {
        var option = new StudyModeOptionViewModel
        {
            Mode = StudyMode.Dictation,
            Name = "Nghe chép",
            Description = "Nghe và viết lại từ",
            IconClass = "ph-headphones",
            ActionUrl = $"/Study/{setId}/Dictation",
            IsAvailable = cards.Any(),            // Có ít nhất 1 thẻ có câu ví dụ
            CardCount = cards.Count,              // Số thẻ có câu ví dụ sau lọc
            EstimatedSeconds = cards.Count * 25   // Ước tính 25 giây/thẻ
        };

        // Nếu không có thẻ nào khả dụng, giải thích lý do
        if (!option.IsAvailable)
        {
            option.UnavailableReason = "Không đủ thẻ có câu ví dụ";
        }

        return option;
    }
}
