using ltwnc.Models.Entities;

namespace ltwnc.Services;

public interface IFlashcardSetService
{
    Task<List<FlashcardSet>> GetMySetsAsync(string userId);
    Task<List<FlashcardSet>> GetPublicSetsAsync();
    Task<List<FlashcardSet>> SearchPublicSetsAsync(string query);
    Task<FlashcardSet?> GetSetByIdAsync(int id);
    Task<FlashcardSet?> GetSetWithCardsAsync(int id, string userId);
    Task<FlashcardSet> CreateSetAsync(string title, string? description, bool isPublic, string userId);
    Task UpdateSetAsync(int id, string title, string? description, bool isPublic, string userId);
    Task DeleteSetAsync(int id, string userId);
    Task<Flashcard> AddCardAsync(int setId, string frontText, string backText, string userId);
    Task<int> UpdateCardAsync(int cardId, string frontText, string backText, string userId);
    Task<int> DeleteCardAsync(int cardId, string userId);
}
