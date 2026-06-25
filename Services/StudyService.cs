using ltwnc.Models.Entities;
using ltwnc.Repositories;

namespace ltwnc.Services;

// Service xử lý nghiệp vụ học tập
// Quản lý tiến trình học (đã biết/chưa biết) và phiên học
public class StudyService : IStudyService
{
    private readonly IFlashcardRepository _cardRepo;
    private readonly IStudySessionRepository _studyRepo;
    private readonly IFlashcardSetRepository _setRepo;

    // Inject 3 repository: thẻ, phiên học, bộ thẻ
    public StudyService(IFlashcardRepository cardRepo, IStudySessionRepository studyRepo, IFlashcardSetRepository setRepo)
    {
        _cardRepo = cardRepo;
        _studyRepo = studyRepo;
        _setRepo = setRepo;
    }

    // Lấy danh sách thẻ trong một bộ để học
    public async Task<List<Flashcard>> GetFlashcardsForStudyAsync(int setId)
    {
        return await _cardRepo.GetBySetIdAsync(setId);
    }

    // Đánh dấu thẻ đã biết hoặc chưa biết
    // Nếu chưa có tiến trình → tạo mới
    // Nếu đã có → cập nhật trạng thái
    public async Task MarkLearnedAsync(string userId, int flashcardId, bool learned)
    {
        // Kiểm tra đã có tiến trình học cho thẻ này chưa
        var progress = await _studyRepo.GetProgressAsync(userId, flashcardId);
        if (progress == null)
        {
            // Chưa có → tạo mới tiến trình
            progress = new UserProgress
            {
                UserId = userId,
                FlashcardId = flashcardId,
                IsLearned = learned,
                LastReviewed = DateTime.UtcNow
            };
            await _studyRepo.AddProgressAsync(progress);
        }
        else
        {
            // Đã có → cập nhật trạng thái và thời gian học
            progress.IsLearned = learned;
            progress.LastReviewed = DateTime.UtcNow;
            _studyRepo.UpdateProgress(progress);
        }

        // Lưu thay đổi vào database
        await _setRepo.SaveChangesAsync();
    }

    // Ghi nhận hoàn thành một phiên học
    // Lưu lại chế độ học (Flashcard, Quiz, Write, Match) và thời gian hoàn thành
    public async Task CompleteSessionAsync(string userId, int setId, StudyMode mode)
    {
        var session = new StudySession
        {
            UserId = userId,
            FlashcardSetId = setId,
            Mode = mode,
            CompletedAt = DateTime.UtcNow
        };
        await _studyRepo.AddAsync(session);
    }
}
