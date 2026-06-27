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
    public async Task<List<Flashcard>> GetFlashcardsForStudyAsync(int setId, bool starredOnly = false)
    {
        return await _cardRepo.GetBySetIdAsync(setId, starredOnly);
    }

    // Đánh dấu thẻ đã biết hoặc chưa biết
    // Nếu chưa có tiến trình → tạo mới
    // Nếu đã có → cập nhật trạng thái
    public async Task MarkLearnedAsync(string userId, int setId, int flashcardId, bool learned)
    {
        var set = await _setRepo.GetByIdAsync(setId);
        if (set == null)
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        if (!set.IsPublic && set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");

        var card = await _cardRepo.GetByIdAsync(flashcardId);
        if (card == null || card.FlashcardSetId != setId)
            throw new KeyNotFoundException("Thẻ không tồn tại trong bộ thẻ này.");

        var progress = await _studyRepo.GetProgressAsync(userId, flashcardId);
        if (progress == null)
        {
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
            progress.IsLearned = learned;
            progress.LastReviewed = DateTime.UtcNow;
            _studyRepo.UpdateProgress(progress);
        }

        await _setRepo.SaveChangesAsync();
    }

    // Ghi nhận hoàn thành một phiên học
    // Lưu lại chế độ học (Flashcard, Quiz, Write, Match) và thời gian hoàn thành
    public async Task CompleteSessionAsync(string userId, int setId, StudyMode mode)
    {
        var set = await _setRepo.GetByIdAsync(setId);
        if (set == null)
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        if (!set.IsPublic && set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");

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
