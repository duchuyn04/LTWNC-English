using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

// Chạy command batch và ghi CardActionLog; Undo từ log + snapshot.
public class CardActionService
{
    // Lưu log và transaction
    private readonly AppDbContext _context;

    // Tái tạo command khi Undo theo ActionType trong log
    private readonly CardActionCommandFactory _commandFactory;

    // Inject DbContext và factory command
    public CardActionService(AppDbContext context, CardActionCommandFactory commandFactory)
    {
        _context = context;
        _commandFactory = commandFactory;
    }

    // Execute command trong transaction, ghi log kèm snapshot, trả về log vừa tạo
    public async Task<CardActionLog> ExecuteAsync(ICardActionCommand command)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync();

        await command.ExecuteAsync();

        // Snapshot trạng thái trước/sau tùy command (thường là trước khi đổi)
        string snapshot = command.GetSnapshotJson();

        CardActionLog log = new CardActionLog
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

    // Load log của user, chặn Undo lần 2, nạp snapshot rồi gọi command.UndoAsync
    public async Task UndoAsync(int logId, string userId)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync();

        CardActionLog? log = await GetLogByIdAsync(logId, userId);
        if (log == null)
        {
            throw new KeyNotFoundException("Không tìm thấy hành động để hoàn tác.");
        }

        if (log.UndoneAt.HasValue)
        {
            throw new InvalidOperationException("Hành động này đã được hoàn tác.");
        }

        List<int>? cardIds = JsonSerializer.Deserialize<List<int>>(log.CardIdsJson);
        if (cardIds == null)
        {
            cardIds = new List<int>();
        }

        ICardActionCommand command = _commandFactory.Create(
            log.ActionType,
            log.SetId,
            userId,
            cardIds);

        command.LoadSnapshot(log.SnapshotJson);

        await command.UndoAsync();
        log.UndoneAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    // Log chưa Undo của một bộ thẻ, mới nhất trước, giới hạn limit
    public async Task<IReadOnlyList<CardActionLog>> GetUndoableLogsAsync(
        int setId,
        string userId,
        int limit = 5)
    {
        List<CardActionLog> logs = await _context.CardActionLogs
            .Where(log =>
                log.SetId == setId
                && log.UserId == userId
                && !log.UndoneAt.HasValue)
            .OrderByDescending(log => log.ExecutedAt)
            .Take(limit)
            .ToListAsync();

        return logs;
    }

    // Log theo id, chỉ khi đúng user
    public async Task<CardActionLog?> GetLogByIdAsync(int logId, string userId)
    {
        CardActionLog? log = await _context.CardActionLogs
            .FirstOrDefaultAsync(row => row.Id == logId && row.UserId == userId);

        return log;
    }
}
