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

    // cardId -> IsStarred trước khi Execute (dùng Undo)
    private readonly Dictionary<int, bool> _previousStates = new();

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
    public async Task ExecuteAsync()
    {
        // 1. Gọi `ToListAsync` và lưu kết quả vào `cards`.
        List<Flashcard> cards = await _context.Flashcards
            .Where(flashcard =>
                flashcard.FlashcardSetId == SetId
                && CardIds.Contains(flashcard.Id))
            .ToListAsync();

        // 2. Gọi `Clear` để thực hiện bước nghiệp vụ này.
        _previousStates.Clear();

        // 3. Duyệt từng `card` trong `cards` để xử lý lần lượt.
        foreach (Flashcard card in cards)
        {
            // 4. Cập nhật `_previousStates[card.Id]` bằng giá trị mới.
            _previousStates[card.Id] = card.IsStarred;
            // 5. Cập nhật `card.IsStarred` bằng giá trị mới.
            card.IsStarred = true;
        }

        // 6. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();
    }

    // Gán lại IsStarred theo snapshot; thẻ không có trong snapshot thì bỏ qua
    public async Task UndoAsync()
    {
        // 1. Gọi `ToListAsync` và lưu kết quả vào `cards`.
        List<Flashcard> cards = await _context.Flashcards
            .Where(flashcard =>
                flashcard.FlashcardSetId == SetId
                && CardIds.Contains(flashcard.Id))
            .ToListAsync();

        // 2. Duyệt từng `card` trong `cards` để xử lý lần lượt.
        foreach (Flashcard card in cards)
        {
            // 3. Kiểm tra `_previousStates.TryGetValue(card.Id, out bool oldState)` để chọn nhánh xử lý phù hợp.
            if (_previousStates.TryGetValue(card.Id, out bool oldState))
            {
                // 4. Cập nhật `card.IsStarred` bằng giá trị mới.
                card.IsStarred = oldState;
            }
        }

        // 5. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();
    }

    // JSON dictionary cardId -> IsStarred cũ
    public string GetSnapshotJson()
    {
        // 1. Trả kết quả từ `Serialize` cho nơi gọi.
        return JsonSerializer.Serialize(_previousStates);
    }

    // Nạp dictionary từ log; json null/rỗng thì để snapshot trống
    public void LoadSnapshot(string json)
    {
        // 1. Gọi `Clear` để thực hiện bước nghiệp vụ này.
        _previousStates.Clear();

        // 2. Gọi `Deserialize` và lưu kết quả vào `loaded`.
        Dictionary<int, bool>? loaded =
            JsonSerializer.Deserialize<Dictionary<int, bool>>(json);

        // 3. Kiểm tra `loaded == null` để chọn nhánh xử lý phù hợp.
        if (loaded == null)
        {
            // 4. Kết thúc phương thức sau khi hoàn tất xử lý.
            return;
        }

        // 5. Duyệt từng `pair` trong `loaded` để xử lý lần lượt.
        foreach (KeyValuePair<int, bool> pair in loaded)
        {
            // 6. Cập nhật `_previousStates[pair.Key]` bằng giá trị mới.
            _previousStates[pair.Key] = pair.Value;
        }
    }
}
