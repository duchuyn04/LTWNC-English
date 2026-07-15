using ltwnc.Models.Entities;

namespace ltwnc.Services.CardActions;

// Chạy command batch, ghi log, undo theo log + snapshot.
public interface ICardActionService
{
    Task<CardActionLog> ExecuteAsync(ICardActionCommand command);

    Task UndoAsync(int logId, string userId);

    Task<IReadOnlyList<CardActionLog>> GetUndoableLogsAsync(
        int setId,
        string userId,
        int limit = 5);

    Task<CardActionLog?> GetLogByIdAsync(int logId, string userId);
}
