using ltwnc.Data;

namespace ltwnc.Services.CardActions;

// Map chuỗi action type từ form/API sang command concrete.
// Controller không new trực tiếp Delete/Star/Unstar.
public class CardActionCommandFactory : ICardActionCommandFactory
{
    // Truyền vào constructor từng command (cần DbContext)
    private readonly AppDbContext _context;

    // Inject DbContext dùng chung cho mọi command tạo ra
    public CardActionCommandFactory(AppDbContext context)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
    }

    // actionType: "Delete" | "Star" | "Unstar". Sai type thì throw.
    public ICardActionCommand Create(
        string actionType,
        int setId,
        string userId,
        IReadOnlyList<int> cardIds)
    {
        // 1. Kiểm tra `actionType == "Delete"` để chọn nhánh xử lý phù hợp.
        if (actionType == "Delete")
        {
            // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new DeleteCardsCommand(_context, setId, userId, cardIds);
        }

        // 3. Kiểm tra `actionType == "Star"` để chọn nhánh xử lý phù hợp.
        if (actionType == "Star")
        {
            // 4. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new StarCardsCommand(_context, setId, userId, cardIds);
        }

        // 5. Kiểm tra `actionType == "Unstar"` để chọn nhánh xử lý phù hợp.
        if (actionType == "Unstar")
        {
            // 6. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new UnstarCardsCommand(_context, setId, userId, cardIds);
        }

        // 7. Dừng xử lý và phát sinh lỗi `new InvalidOperationException($"Unknown action type: {actionType}.")`.
        throw new InvalidOperationException($"Unknown action type: {actionType}.");
    }
}
