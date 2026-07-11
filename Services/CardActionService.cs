using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

public class CardActionService
{
    private readonly AppDbContext _context;

    public CardActionService(AppDbContext context) => _context = context;

    public AppDbContext Context => _context;

    public async Task<CardActionLog> ExecuteAsync(ICardActionCommand command)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await command.ExecuteAsync();

        var snapshot = command switch
        {
            DeleteCardsCommand delete => delete.GetSnapshotJson(),
            StarCardsCommand star => star.GetSnapshotJson(),
            UnstarCardsCommand unstar => unstar.GetSnapshotJson(),
            _ => throw new InvalidOperationException("Unknown command type.")
        };

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
        ICardActionCommand command = log.ActionType switch
        {
            "Delete" => new DeleteCardsCommand(_context, log.SetId, userId, cardIds),
            "Star" => new StarCardsCommand(_context, log.SetId, userId, cardIds),
            "Unstar" => new UnstarCardsCommand(_context, log.SetId, userId, cardIds),
            _ => throw new InvalidOperationException("Unknown action type.")
        };

        switch (command)
        {
            case DeleteCardsCommand delete:
                delete.LoadSnapshot(log.SnapshotJson);
                break;
            case StarCardsCommand star:
                star.LoadSnapshot(log.SnapshotJson);
                break;
            case UnstarCardsCommand unstar:
                unstar.LoadSnapshot(log.SnapshotJson);
                break;
        }

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
