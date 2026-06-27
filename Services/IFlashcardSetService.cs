using ltwnc.Models.Entities;

namespace ltwnc.Services;

// Interface định nghĩa các phương thức xử lý nghiệp vụ bộ thẻ flashcard
public interface IFlashcardSetService
{
    // Lấy danh sách bộ thẻ của một người dùng
    Task<List<FlashcardSet>> GetMySetsAsync(string userId);

    // Lấy danh sách bộ thẻ public (công khai)
    Task<List<FlashcardSet>> GetPublicSetsAsync();

    // Tìm kiếm bộ thẻ public theo từ khóa
    Task<List<FlashcardSet>> SearchPublicSetsAsync(string query);

    // Lấy bộ thẻ theo id (không kèm danh sách thẻ)
    Task<FlashcardSet?> GetSetByIdAsync(int id);
    // Lấy bộ thẻ theo id kèm danh sách thẻ — chỉ trả về nếu là chủ sở hữu
    Task<FlashcardSet?> GetSetWithCardsAsync(int id, string userId);
    // Tạo bộ thẻ mới
    Task<FlashcardSet> CreateSetAsync(string title, string? description, bool isPublic, string userId);

    // Cập nhật thông tin bộ thẻ
    Task UpdateSetAsync(int id, string title, string? description, bool isPublic, string userId);

    // Xóa bộ thẻ (kèm tất cả thẻ bên trong)
    Task DeleteSetAsync(int id, string userId);

    // Thêm thẻ mới vào bộ
    Task<Flashcard> AddCardAsync(
        int setId,
        string frontText,
        string backText,
        string pronunciation,
        string partOfSpeech,
        string exampleSentence,
        string exampleMeaning,
        string? synonyms,
        bool isStarred,
        string userId);

    // Cập nhật nội dung thẻ — trả về setId để redirect
    Task<int> UpdateCardAsync(
        int cardId,
        string frontText,
        string backText,
        string pronunciation,
        string partOfSpeech,
        string exampleSentence,
        string exampleMeaning,
        string? synonyms,
        bool isStarred,
        string userId);

    // Xóa thẻ — trả về setId để redirect
    Task<int> DeleteCardAsync(int cardId, string userId);
}
