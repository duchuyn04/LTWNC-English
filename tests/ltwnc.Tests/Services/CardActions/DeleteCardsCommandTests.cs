using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ltwnc.Tests;

// Kiểm tra command xóa thẻ: khi undo phải khôi phục đúng thẻ, tiến trình học và lịch sử nghe chép.
public class DeleteCardsCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly FlashcardSet _set;
    private readonly Flashcard _card;
    private readonly UserProgress _progress;
    private readonly StudySession _session;
    private readonly DictationSessionDetail _detail;
    private const string OwnerId = "owner";

    public DeleteCardsCommandTests()
    {
        // Dùng SQLite in-memory với foreign key bật để kiểm tra ràng buộc giống production
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        var owner = new Microsoft.AspNetCore.Identity.IdentityUser
        {
            Id = OwnerId,
            UserName = OwnerId,
            NormalizedUserName = OwnerId.ToUpperInvariant()
        };
        _context.Users.Add(owner);
        _context.SaveChanges();

        // Chuẩn bị một bộ thẻ có 1 thẻ và các dữ liệu liên quan
        _set = new FlashcardSet
        {
            Title = "Set",
            UserId = OwnerId,
            IsPublic = false
        };
        _context.FlashcardSets.Add(_set);
        _context.SaveChanges();

        _card = new Flashcard
        {
            FlashcardSetId = _set.Id,
            FrontText = "Front",
            BackText = "Back",
            Pronunciation = "/a/",
            PartOfSpeech = "noun",
            ExampleSentence = "Example",
            ExampleMeaning = "Meaning"
        };
        _context.Flashcards.Add(_card);
        _context.SaveChanges();

        _session = new StudySession
        {
            UserId = OwnerId,
            FlashcardSetId = _set.Id,
            Mode = StudyMode.Flashcard
        };
        _context.StudySessions.Add(_session);
        _context.SaveChanges();

        _progress = new UserProgress
        {
            UserId = OwnerId,
            FlashcardId = _card.Id,
            IsLearned = true,
            Status = UserProgressStatus.Mastered,
            CorrectCount = 3,
            WrongCount = 1,
            LastReviewed = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc)
        };
        _context.UserProgresses.Add(_progress);
        _context.SaveChanges();

        _detail = new DictationSessionDetail
        {
            StudySessionId = _session.Id,
            FlashcardId = _card.Id,
            IsCorrect = true,
            AnsweredText = "Answer"
        };
        _context.DictationSessionDetails.Add(_detail);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // Sau khi xóa rồi undo, toàn bộ dữ liệu liên quan đến thẻ phải được khôi phục
    [Fact]
    public async Task UndoAsync_restores_cards_progress_and_dictation_details()
    {
        var command = new ltwnc.Services.CardActions.DeleteCardsCommand(_context, _set.Id, OwnerId, [_card.Id]);
        await command.ExecuteAsync();
        var undo = new ltwnc.Services.CardActions.DeleteCardsCommand(_context, _set.Id, OwnerId, [_card.Id]);
        undo.LoadSnapshot(command.GetSnapshotJson());

        await undo.UndoAsync();

        Assert.NotNull(await _context.Flashcards.FindAsync(_card.Id));
        Assert.NotNull(await _context.UserProgresses.FindAsync(_progress.Id));
        Assert.NotNull(await _context.DictationSessionDetails.FindAsync(_detail.Id));
    }
}
