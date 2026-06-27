using ltwnc.Models.Entities;

namespace ltwnc.Services;

// Interface định nghĩa các phương thức xử lý nghiệp vụ học tập
public interface IStudyService
{
    // Lấy danh sách thẻ để học trong một bộ
    Task<List<Flashcard>> GetFlashcardsForStudyAsync(int setId, bool starredOnly = false);

    // Đánh dấu thẻ đã biết hoặc chưa biết
    Task MarkLearnedAsync(string userId, int flashcardId, bool learned);

    // Ghi nhận hoàn thành một phiên học
    Task CompleteSessionAsync(string userId, int setId, StudyMode mode);
}
