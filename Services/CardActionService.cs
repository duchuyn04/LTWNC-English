using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

// Service thực thi và hoàn tác các hành động hàng loạt trên thẻ
// Mỗi hành động được ghi log kèm snapshot để có thể Undo sau này
public class CardActionService
{
    private readonly AppDbContext _context;
    private readonly CardActionCommandFactory _commandFactory;

    public CardActionService(AppDbContext context, CardActionCommandFactory commandFactory)
    {
        _context = context;
        _commandFactory = commandFactory;
    }

    // Thực thi command, lưu snapshot và ghi log trong một transaction
    public async Task<CardActionLog> ExecuteAsync(ICardActionCommand command)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await command.ExecuteAsync();

        // Snapshot lưu trạng thái trước khi thay đổi, dùng để hoàn tác
        var snapshot = command.GetSnapshotJson();

        var log = new CardActionLog
        {
            UserId = command.UserId,
            SetId = command.SetId,
            ActionType = command.ActionType,
            CardIdsJson = JsonSerializer.Serialize(command.CardIds),
            SnapshotJson = snapshot,
            ExecutedAt = DateTime.UtcNow
        };

        _context.CardActionLogs.Add(log);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return log;
    }

    // Hoàn tác một hành động đã ghi log bằng cách khôi phục snapshot
    public async Task UndoAsync(int logId, string userId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var log = await GetLogByIdAsync(logId, userId)
                  ?? throw new KeyNotFoundException("Không tìm thấy hành động để hoàn tác.");

        // Tránh hoàn tác một log hai lần
        if (log.UndoneAt.HasValue)
            throw new InvalidOperationException("Hành động này đã được hoàn tác.");

        // Tạo lại command từ log, nạp snapshot và thực hiện undo
        var cardIds = JsonSerializer.Deserialize<List<int>>(log.CardIdsJson) ?? [];
        var command = _commandFactory.Create(log.ActionType, log.SetId, userId, cardIds);
        command.LoadSnapshot(log.SnapshotJson);

        await command.UndoAsync();
        log.UndoneAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    // Lấy các log chưa hoàn tác của một bộ thẻ, mới nhất trước
    public async Task<IReadOnlyList<CardActionLog>> GetUndoableLogsAsync(int setId, string userId, int limit = 5)
        => await _context.CardActionLogs
            .Where(l => l.SetId == setId && l.UserId == userId && !l.UndoneAt.HasValue)
            .OrderByDescending(l => l.ExecutedAt)
            .Take(limit)
            .ToListAsync();

    // Lấy log theo id — chỉ trả về nếu thuộc về user hiện tại
    public Task<CardActionLog?> GetLogByIdAsync(int logId, string userId)
        => _context.CardActionLogs.FirstOrDefaultAsync(l => l.Id == logId && l.UserId == userId);
}
