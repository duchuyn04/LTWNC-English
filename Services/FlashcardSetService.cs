using ltwnc.Models.Entities;
using ltwnc.Repositories;

namespace ltwnc.Services;

// Service xử lý nghiệp vụ bộ thẻ flashcard
// Phân quyền: chỉ chủ sở hữu mới được sửa/xóa bộ thẻ và thẻ
public class FlashcardSetService : IFlashcardSetService
{
    private readonly IFlashcardSetRepository _setRepo;
    private readonly IFlashcardRepository _cardRepo;

    // Inject repository bộ thẻ và repository thẻ
    public FlashcardSetService(IFlashcardSetRepository setRepo, IFlashcardRepository cardRepo)
    {
        _setRepo = setRepo;
        _cardRepo = cardRepo;
    }

    private static string RequiredText(string? value, string fieldName)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException($"{fieldName} không được để trống.");
        }

        return trimmed;
    }

    private static string? OptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    // Lấy tất cả bộ thẻ thuộc về một người dùng
    public async Task<List<FlashcardSet>> GetMySetsAsync(string userId)
    {
        return await _setRepo.GetByUserIdAsync(userId);
    }

    // Lấy danh sách bộ thẻ public (mới nhất)
    public async Task<List<FlashcardSet>> GetPublicSetsAsync()
    {
        return await _setRepo.GetPublicSetsAsync();
    }

    // Tìm kiếm bộ thẻ public theo tiêu đề
    public async Task<List<FlashcardSet>> SearchPublicSetsAsync(string query)
    {
        return await _setRepo.SearchPublicSetsAsync(query);
    }

    // Lấy bộ thẻ theo id (không kèm thẻ)
    public async Task<FlashcardSet?> GetSetByIdAsync(int id)
    {
        return await _setRepo.GetByIdAsync(id);
    }

    public async Task<FlashcardSet?> GetAccessibleSetAsync(int id, string? userId)
    {
        var set = await _setRepo.GetByIdAsync(id);
        if (set == null || (!set.IsPublic && set.UserId != userId)) return null;
        return set;
    }

    // Lấy bộ thẻ kèm danh sách thẻ — chỉ trả về nếu người yêu cầu là chủ sở hữu
    public async Task<FlashcardSet?> GetSetWithCardsAsync(int id, string userId)
    {
        var set = await _setRepo.GetByIdWithCardsAsync(id);
        if (set == null || set.UserId != userId) return null;
        return set;
    }

    public async Task<FlashcardSet?> GetAccessibleSetWithCardsAsync(int id, string? userId)
    {
        var set = await _setRepo.GetByIdWithCardsAsync(id);
        if (set == null || (!set.IsPublic && set.UserId != userId)) return null;
        return set;
    }

    // Tạo bộ thẻ mới
    // Gán thời gian tạo/cập nhật là thời điểm hiện tại (UTC)
    public async Task<FlashcardSet> CreateSetAsync(string title, string? description, bool isPublic, string userId)
    {
        var set = new FlashcardSet
        {
            Title = title,
            Description = description,
            IsPublic = isPublic,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _setRepo.AddAsync(set);
        await _setRepo.SaveChangesAsync();
        return set;
    }

    // Cập nhật thông tin bộ thẻ
    // Kiểm tra quyền sở hữu trước khi sửa — ném UnauthorizedAccessException nếu không phải chủ
    public async Task UpdateSetAsync(int id, string title, string? description, bool isPublic, string userId)
    {
        var set = await _setRepo.GetByIdAsync(id);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền sửa bộ thẻ này.");
        set.Title = title;
        set.Description = description;
        set.IsPublic = isPublic;
        set.UpdatedAt = DateTime.UtcNow;
        _setRepo.Update(set);
        await _setRepo.SaveChangesAsync();
    }

    // Xóa bộ thẻ
    // Kiểm tra quyền sở hữu trước khi xóa
    public async Task DeleteSetAsync(int id, string userId)
    {
        var set = await _setRepo.GetByIdAsync(id);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền xóa bộ thẻ này.");
        _setRepo.Delete(set);
        await _setRepo.SaveChangesAsync();
    }

    // Thêm thẻ mới vào bộ
    // Tự động gán OrderIndex = max hiện tại + 1 (thẻ mới luôn ở cuối)
    public async Task<Flashcard> AddCardAsync(
        int setId,
        string frontText,
        string backText,
        string pronunciation,
        string partOfSpeech,
        string exampleSentence,
        string exampleMeaning,
        string? synonyms,
        bool isStarred,
        string userId)
    {
        var set = await _setRepo.GetByIdWithCardsAsync(setId);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền thêm thẻ.");

        frontText = RequiredText(frontText, "Thuật ngữ");
        backText = RequiredText(backText, "Định nghĩa");
        pronunciation = RequiredText(pronunciation, "IPA");
        partOfSpeech = RequiredText(partOfSpeech, "Loại từ");
        exampleSentence = RequiredText(exampleSentence, "Ví dụ tiếng Anh");
        exampleMeaning = RequiredText(exampleMeaning, "Nghĩa câu ví dụ tiếng Việt");
        synonyms = OptionalText(synonyms);

        var maxOrder = set.Flashcards.Any() ? set.Flashcards.Max(f => f.OrderIndex) : 0;
        var card = new Flashcard
        {
            FlashcardSetId = setId,
            FrontText = frontText,
            BackText = backText,
            Pronunciation = pronunciation,
            PartOfSpeech = partOfSpeech,
            ExampleSentence = exampleSentence,
            ExampleMeaning = exampleMeaning,
            Synonyms = synonyms,
            IsStarred = isStarred,
            OrderIndex = maxOrder + 1
        };

        await _cardRepo.AddAsync(card);
        await _setRepo.SaveChangesAsync();
        return card;
    }

    // Cập nhật nội dung thẻ (mặt trước + mặt sau)
    // Trả về setId để controller redirect về trang chỉnh sửa
    public async Task<int> UpdateCardAsync(
        int cardId,
        string frontText,
        string backText,
        string pronunciation,
        string partOfSpeech,
        string exampleSentence,
        string exampleMeaning,
        string? synonyms,
        bool isStarred,
        string userId)
    {
        var card = await _cardRepo.GetByIdAsync(cardId);
        if (card == null) throw new KeyNotFoundException("Thẻ không tồn tại.");

        var setId = card.FlashcardSetId;
        var set = await _setRepo.GetByIdAsync(setId);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền sửa thẻ này.");

        card.FrontText = RequiredText(frontText, "Thuật ngữ");
        card.BackText = RequiredText(backText, "Định nghĩa");
        card.Pronunciation = RequiredText(pronunciation, "IPA");
        card.PartOfSpeech = RequiredText(partOfSpeech, "Loại từ");
        card.ExampleSentence = RequiredText(exampleSentence, "Ví dụ tiếng Anh");
        card.ExampleMeaning = RequiredText(exampleMeaning, "Nghĩa câu ví dụ tiếng Việt");
        card.Synonyms = OptionalText(synonyms);
        card.IsStarred = isStarred;

        _cardRepo.Update(card);
        await _setRepo.SaveChangesAsync();
        return setId;
    }

    // Xóa thẻ khỏi bộ
    // Trả về setId để controller redirect về trang chỉnh sửa
    public async Task<int> DeleteCardAsync(int cardId, string userId)
    {
        var card = await _cardRepo.GetByIdAsync(cardId);
        if (card == null) throw new KeyNotFoundException("Thẻ không tồn tại.");
        var setId = card.FlashcardSetId;
        var set = await _setRepo.GetByIdAsync(setId);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền xóa thẻ này.");
        _cardRepo.Delete(card);
        await _setRepo.SaveChangesAsync();
        return setId;
    }
}
