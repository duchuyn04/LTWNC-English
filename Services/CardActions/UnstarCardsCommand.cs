using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

// Command bỏ sao nhiều thẻ. Snapshot giữ IsStarred cũ để Undo.
public class UnstarCardsCommand : ICardActionCommand
{
    // Query / update Flashcards
    private readonly AppDbContext _context;

    // Cố định "Unstar"
    public string ActionType => "Unstar";

    // Bộ thẻ đang thao tác
    public int SetId { get; }

    // User thực hiện
    public string UserId { get; }

    // Id thẻ cần bỏ sao
    public IReadOnlyList<int> CardIds { get; }

    // Tạo command với set, user và danh sách card id
    public UnstarCardsCommand(AppDbContext context, int setId, string userId, IEnumerable<int> cardIds)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `SetId` để các phương thức khác sử dụng.
        SetId = setId;
        // 3. Lưu dependency `UserId` để các phương thức khác sử dụng.
        UserId = userId;
        // 4. Lưu dependency `CardIds` để các phương thức khác sử dụng.
        CardIds = cardIds.ToList().AsReadOnly();
    }

    // Chụp IsStarred cũ rồi set false
    public async Task<CardActionMemento> ExecuteAsync()
    {
        // 1. Gọi `ToListAsync` và lưu kết quả vào `cards`.
        List<Flashcard> cards = await _context.Flashcards
            .Where(flashcard =>
                flashcard.FlashcardSetId == SetId
                && CardIds.Contains(flashcard.Id))
            .ToListAsync();

        // Memento giữ trạng thái cũ, command không cần snapshot tạm trên field.
        Dictionary<int, bool> previousStates = new();

        // 3. Duyệt từng `card` trong `cards` để xử lý lần lượt.
        foreach (Flashcard card in cards)
        {
            // 4. Lưu trạng thái hiện tại vào dữ liệu Memento.
            previousStates[card.Id] = card.IsStarred;
            // 5. Cập nhật `card.IsStarred` bằng giá trị mới.
            card.IsStarred = false;
        }

        // 6. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();

        return new CardActionMemento(JsonSerializer.Serialize(previousStates));
    }

    // Khôi phục IsStarred theo Memento
    public async Task UndoAsync(CardActionMemento memento)
    {
        Dictionary<int, bool> previousStates =
            CardActionMemento.Restore<Dictionary<int, bool>>(memento);

        // 1. Gọi `ToListAsync` và lưu kết quả vào `cards`.
        List<Flashcard> cards = await _context.Flashcards
            .Where(flashcard =>
                flashcard.FlashcardSetId == SetId
                && CardIds.Contains(flashcard.Id))
            .ToListAsync();

        HashSet<int> expectedCardIds = CardIds.ToHashSet();
        bool isInvalid = previousStates.Count == 0
            || expectedCardIds.Count == 0
            || !previousStates.Keys.ToHashSet().SetEquals(expectedCardIds)
            || cards.Any(card => !previousStates.ContainsKey(card.Id));
        if (isInvalid)
        {
            throw CardActionMemento.InvalidMemento();
        }

        // 2. Duyệt từng `card` trong `cards` để xử lý lần lượt.
        foreach (Flashcard card in cards)
        {
            // 3. Nếu Memento có trạng thái cũ của thẻ thì khôi phục trạng thái đó.
            if (previousStates.TryGetValue(card.Id, out bool oldState))
            {
                // 4. Cập nhật `card.IsStarred` bằng giá trị mới.
                card.IsStarred = oldState;
            }
        }

        // 5. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();
    }
}
