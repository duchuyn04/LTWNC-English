using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

// Command gắn sao nhiều thẻ. Snapshot giữ IsStarred cũ để Undo.
public class StarCardsCommand : ICardActionCommand
{
    // Query / update Flashcards
    private readonly AppDbContext _context;

    // Cố định "Star" cho log và factory
    public string ActionType => "Star";

    // Bộ thẻ chứa các thẻ đang thao tác
    public int SetId { get; }

    // User thực hiện (log)
    public string UserId { get; }

    // Id thẻ cần gắn sao
    public IReadOnlyList<int> CardIds { get; }

    // Tạo command với set, user và danh sách card id
    public StarCardsCommand(AppDbContext context, int setId, string userId, IEnumerable<int> cardIds)
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

    // Chụp IsStarred cũ rồi set true cho mọi thẻ trong CardIds thuộc SetId
    public async Task<CardActionMemento> ExecuteAsync()
    {
        // 1. Xác thực quyền sở hữu và toàn bộ target trước khi thay đổi.
        List<Flashcard> cards = await CardActionTargetValidator.ValidateAsync(
            _context,
            SetId,
            UserId,
            CardIds);

        // Memento giữ trạng thái cũ, command không cần snapshot tạm trên field.
        Dictionary<int, bool> previousStates = new();

        // 3. Duyệt từng `card` trong `cards` để xử lý lần lượt.
        foreach (Flashcard card in cards)
        {
            // 4. Lưu trạng thái hiện tại vào dữ liệu Memento.
            previousStates[card.Id] = card.IsStarred;
            // 5. Cập nhật `card.IsStarred` bằng giá trị mới.
            card.IsStarred = true;
        }

        // 6. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();

        return new CardActionMemento(JsonSerializer.Serialize(previousStates));
    }

    // Gán lại IsStarred theo Memento sau khi kiểm tra snapshot đủ cho các thẻ hiện có
    public async Task UndoAsync(CardActionMemento memento)
    {
        Dictionary<int, bool> previousStates =
            CardActionMemento.Restore<Dictionary<int, bool>>(memento);

        List<Flashcard> cards;
        try
        {
            cards = await CardActionTargetValidator.ValidateAsync(
                _context,
                SetId,
                UserId,
                CardIds);
        }
        catch (ArgumentException exception)
        {
            throw CardActionMemento.InvalidMemento(exception);
        }

        HashSet<int> expectedCardIds = CardIds.ToHashSet();
        HashSet<int> actualCardIds = cards.Select(card => card.Id).ToHashSet();
        bool isInvalid = previousStates.Count == 0
            || expectedCardIds.Count == 0
            || !previousStates.Keys.ToHashSet().SetEquals(expectedCardIds)
            || !actualCardIds.SetEquals(expectedCardIds)
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
