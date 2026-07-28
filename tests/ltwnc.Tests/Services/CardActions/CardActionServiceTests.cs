using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.CardActions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ltwnc.Tests;

// Kiểm tra CardActionService giữ Memento và dùng lại snapshot của log cũ.
public class CardActionServiceTests : IDisposable
{
    private const string OwnerId = "owner";
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly CardActionService _service;
    private readonly FlashcardSet _set;

    public CardActionServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
        _set = new FlashcardSet { Title = "Set", UserId = OwnerId };
        _context.FlashcardSets.Add(_set);
        _context.SaveChanges();

        var factory = new CardActionCommandFactory(_context);
        _service = new CardActionService(_context, factory);
    }

    [Fact]
    public async Task ExecuteAsync_persists_the_memento_state_without_changing_its_json_format()
    {
        Flashcard card = AddCard(isStarred: false);
        var command = new StarCardsCommand(_context, _set.Id, OwnerId, [card.Id]);

        CardActionLog log = await _service.ExecuteAsync(command);

        Assert.Equal($"{{\"{card.Id}\":false}}", log.SnapshotJson);
        Assert.True(card.IsStarred);
    }

    [Fact]
    public async Task UndoAsync_restores_a_legacy_snapshot_json()
    {
        Flashcard card = AddCard(isStarred: true);
        CardActionLog log = AddLog(card, $"{{\"{card.Id}\":false}}");

        await _service.UndoAsync(log.Id, OwnerId);

        Assert.False(card.IsStarred);
        Assert.NotNull(log.UndoneAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("null")]
    public async Task UndoAsync_rejects_invalid_memento_without_marking_the_log_as_undone(string stateJson)
    {
        Flashcard card = AddCard(isStarred: true);
        CardActionLog log = AddLog(card, stateJson);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UndoAsync(log.Id, OwnerId));

        Assert.Equal("Dữ liệu hoàn tác không hợp lệ.", exception.Message);
        Assert.True(card.IsStarred);
        Assert.Null(log.UndoneAt);

        await _context.Entry(log).ReloadAsync();
        Assert.Null(log.UndoneAt);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[null]")]
    public async Task UndoAsync_rejects_an_empty_or_null_delete_snapshot(string snapshotJson)
    {
        const int deletedCardId = 990;
        var log = new CardActionLog
        {
            UserId = OwnerId,
            SetId = _set.Id,
            ActionType = "Delete",
            CardIdsJson = $"[{deletedCardId}]",
            SnapshotJson = snapshotJson,
            ExecutedAt = DateTime.UtcNow
        };
        _context.CardActionLogs.Add(log);
        _context.SaveChanges();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UndoAsync(log.Id, OwnerId));

        Assert.Equal("Dữ liệu hoàn tác không hợp lệ.", exception.Message);
        Assert.Null(log.UndoneAt);
    }

    [Fact]
    public async Task UndoAsync_rolls_back_a_structurally_corrupt_delete_memento()
    {
        const int deletedCardId = 991;
        string snapshotJson = $$"""
            [{
              "Id": {{deletedCardId}},
              "FlashcardSetId": {{_set.Id}},
              "FrontText": "Front",
              "BackText": "Back",
              "UserProgresses": null
            }]
            """;
        var log = new CardActionLog
        {
            UserId = OwnerId,
            SetId = _set.Id,
            ActionType = "Delete",
            CardIdsJson = $"[{deletedCardId}]",
            SnapshotJson = snapshotJson,
            ExecutedAt = DateTime.UtcNow
        };
        _context.CardActionLogs.Add(log);
        _context.SaveChanges();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UndoAsync(log.Id, OwnerId));

        Assert.Equal("Dữ liệu hoàn tác không hợp lệ.", exception.Message);
        _context.ChangeTracker.Clear();
        Assert.False(await _context.Flashcards.AnyAsync(card => card.Id == deletedCardId));

        CardActionLog persistedLog = await _context.CardActionLogs.SingleAsync(row => row.Id == log.Id);
        Assert.Null(persistedLog.UndoneAt);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private Flashcard AddCard(bool isStarred)
    {
        var card = new Flashcard
        {
            FlashcardSetId = _set.Id,
            FrontText = "Front",
            BackText = "Back",
            IsStarred = isStarred
        };
        _context.Flashcards.Add(card);
        _context.SaveChanges();
        return card;
    }

    private CardActionLog AddLog(Flashcard card, string snapshotJson)
    {
        var log = new CardActionLog
        {
            UserId = OwnerId,
            SetId = _set.Id,
            ActionType = "Star",
            CardIdsJson = $"[{card.Id}]",
            SnapshotJson = snapshotJson,
            ExecutedAt = DateTime.UtcNow
        };
        _context.CardActionLogs.Add(log);
        _context.SaveChanges();
        return log;
    }
}
