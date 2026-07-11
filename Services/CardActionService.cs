using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

public class CardActionService
{
    private readonly AppDbContext _context;
    private readonly CardActionCommandFactory _commandFactory;

    public CardActionService(AppDbContext context, CardActionCommandFactory commandFactory)
    {
        _context = context;
        _commandFactory = commandFactory;
    }

    public async Task<CardActionLog> ExecuteAsync(ICardActionCommand command)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await command.ExecuteAsync();

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

    public async Task UndoAsync(int logId, string userId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var log = await GetLogByIdAsync(logId, userId)
                  ?? throw new KeyNotFoundException("Không tìm thấy hành động để hoàn tác.");

        if (log.UndoneAt.HasValue)
            throw new InvalidOperationException("Hành động này đã được hoàn tác.");

        var cardIds = JsonSerializer.Deserialize<List<int>>(log.CardIdsJson) ?? [];
        var command = _commandFactory.Create(log.ActionType, log.SetId, userId, cardIds);
        command.LoadSnapshot(log.SnapshotJson);

        await command.UndoAsync();
        log.UndoneAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<IReadOnlyList<CardActionLog>> GetUndoableLogsAsync(int setId, string userId, int limit = 5)
        => await _context.CardActionLogs
            .Where(l => l.SetId == setId && l.UserId == userId && !l.UndoneAt.HasValue)
            .OrderByDescending(l => l.ExecutedAt)
            .Take(limit)
            .ToListAsync();

    public Task<CardActionLog?> GetLogByIdAsync(int logId, string userId)
        => _context.CardActionLogs.FirstOrDefaultAsync(l => l.Id == logId && l.UserId == userId);
}
