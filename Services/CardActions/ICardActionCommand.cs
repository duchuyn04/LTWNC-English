namespace ltwnc.Services.CardActions;

// Contract command thao tác hàng loạt trên thẻ (xóa / sao / bỏ sao).
// Execute trả Memento của trạng thái cũ, Undo dùng Memento để khôi phục.
public interface ICardActionCommand
{
    // Tên loại action: "Delete", "Star", "Unstar" (khớp factory và CardActionLog)
    string ActionType { get; }

    // Bộ thẻ đang thao tác
    int SetId { get; }

    // Chủ sở hữu thực hiện action (phân quyền / log)
    string UserId { get; }

    // Id các thẻ bị ảnh hưởng
    IReadOnlyList<int> CardIds { get; }

    // Chụp trạng thái cũ, chạy thao tác và trả Memento để lưu cho Undo
    Task<CardActionMemento> ExecuteAsync();

    // Khôi phục trạng thái từ Memento đã lưu
    Task UndoAsync(CardActionMemento memento);
}
