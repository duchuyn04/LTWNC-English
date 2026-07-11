namespace ltwnc.Services.CardActions;

// Interface cho các command xử lý hành động hàng loạt trên thẻ
// Mỗi command phải biết thực thi, hoàn tác và tạo/khôi phục snapshot
public interface ICardActionCommand
{
    string ActionType { get; }
    int SetId { get; }
    string UserId { get; }
    IReadOnlyList<int> CardIds { get; }

    Task ExecuteAsync();
    Task UndoAsync();

    string GetSnapshotJson();
    void LoadSnapshot(string json);
}
