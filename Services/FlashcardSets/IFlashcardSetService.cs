using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.FlashcardSet;
using Microsoft.AspNetCore.Http;

namespace ltwnc.Services.FlashcardSets;

// Contract CRUD bộ thẻ / thẻ / copy public set.
// Controller và consumer khác inject interface này thay vì FlashcardSetService concrete.
public interface IFlashcardSetService
{
    Task<List<FlashcardSet>> GetMySetsAsync(string userId);

    Task<List<FlashcardSetListItemViewModel>> GetMySetsWithProgressAsync(string userId);

    Task<List<FlashcardSet>> GetPublicSetsAsync();

    Task<List<FlashcardSet>> SearchPublicSetsAsync(string query);

    Task<FlashcardSet?> GetSetByIdAsync(int id);

    Task<FlashcardSet?> GetAccessibleSetAsync(int id, string? userId);

    Task<FlashcardSet?> GetSetWithCardsAsync(int id, string userId);

    Task<FlashcardSet?> GetAccessibleSetWithCardsAsync(int id, string? userId);

    Task<FlashcardSet?> GetOwnedSetAsync(int id, string userId);

    Task<FlashcardSet?> GetExistingCopyAsync(int sourceSetId, string learnerId);

    Task<FlashcardSet> CopyPublicSetAsync(int sourceSetId, string learnerId);

    Task<FlashcardSet> CreateSetAsync(
        string title,
        string? description,
        bool isPublic,
        string userId);

    Task UpdateSetAsync(
        int id,
        string title,
        string? description,
        bool isPublic,
        string userId);

    Task DeleteSetAsync(int id, string userId);

    Task<Flashcard> AddCardAsync(
        int setId,
        string frontText,
        string backText,
        string pronunciation,
        string partOfSpeech,
        string exampleSentence,
        string exampleMeaning,
        string? synonyms,
        string? imageUrl,
        IFormFile? imageFile,
        bool isStarred,
        string userId);

    Task<int> UpdateCardAsync(
        int cardId,
        string frontText,
        string backText,
        string pronunciation,
        string partOfSpeech,
        string exampleSentence,
        string exampleMeaning,
        string? synonyms,
        string? imageUrl,
        IFormFile? imageFile,
        bool removeUploadedImage,
        bool isStarred,
        string userId);

    Task<int> DeleteCardAsync(int cardId, string userId);

    Task<bool> ToggleStarAsync(int cardId, string userId);
}
