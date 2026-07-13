namespace ltwnc.Services.CardActions;

// Contract command thao tác hàng loạt trên thẻ (xóa / sao / bỏ sao).
// Execute chạy thao tác, Undo hoàn tác, snapshot JSON lưu trạng thái trước khi đổi.
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

    // Chạy thao tác (có thể ghi DB); thường chụp snapshot trước khi đổi
    Task ExecuteAsync();

    // Khôi phục theo snapshot đã load
    Task UndoAsync();

    // Serialize snapshot hiện tại (lưu vào CardActionLog)
    string GetSnapshotJson();

    // Nạp snapshot từ log trước khi Undo
    void LoadSnapshot(string json);
}
