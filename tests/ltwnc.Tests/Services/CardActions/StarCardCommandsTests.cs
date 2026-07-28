using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.CardActions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ltwnc.Tests;

// Kiểm tra Memento của hai thao tác thay đổi trạng thái sao.
public class StarCardCommandsTests : IDisposable
{
    private const string OwnerId = "owner";
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly FlashcardSet _set;

    public StarCardCommandsTests()
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
    }

    [Fact]
    public async Task Star_memento_restores_the_previous_unstarred_state()
    {
        Flashcard card = AddCard(isStarred: false);
        var command = new StarCardsCommand(_context, _set.Id, OwnerId, [card.Id]);

        CardActionMemento memento = await command.ExecuteAsync();
        Assert.True(card.IsStarred);

        var undo = new StarCardsCommand(_context, _set.Id, OwnerId, [card.Id]);
        await undo.UndoAsync(memento);

        Assert.False(card.IsStarred);
    }

    [Fact]
    public async Task Unstar_memento_restores_the_previous_starred_state()
    {
        Flashcard card = AddCard(isStarred: true);
        var command = new UnstarCardsCommand(_context, _set.Id, OwnerId, [card.Id]);

        CardActionMemento memento = await command.ExecuteAsync();
        Assert.False(card.IsStarred);

        var undo = new UnstarCardsCommand(_context, _set.Id, OwnerId, [card.Id]);
        await undo.UndoAsync(memento);

        Assert.True(card.IsStarred);
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
}
