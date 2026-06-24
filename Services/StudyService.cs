using ltwnc.Models.Entities;
using ltwnc.Repositories;

namespace ltwnc.Services;

public class StudyService : IStudyService
{
    private readonly IFlashcardRepository _cardRepo;
    private readonly IStudySessionRepository _studyRepo;
    private readonly IFlashcardSetRepository _setRepo;

    public StudyService(IFlashcardRepository cardRepo, IStudySessionRepository studyRepo, IFlashcardSetRepository setRepo)
    {
        _cardRepo = cardRepo;
        _studyRepo = studyRepo;
        _setRepo = setRepo;
    }

    public async Task<List<Flashcard>> GetFlashcardsForStudyAsync(int setId)
    {
        return await _cardRepo.GetBySetIdAsync(setId);
    }

    public async Task MarkLearnedAsync(string userId, int flashcardId, bool learned)
    {
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
