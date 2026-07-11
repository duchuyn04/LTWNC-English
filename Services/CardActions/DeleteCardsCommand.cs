using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

public class DeleteCardsCommand : ICardActionCommand
{
    private readonly AppDbContext _context;
    private readonly List<FlashcardSnapshot> _snapshots = new();

    public string ActionType => "Delete";
    public int SetId { get; }
    public string UserId { get; }
    public IReadOnlyList<int> CardIds { get; }

    public DeleteCardsCommand(AppDbContext context, int setId, string userId, IEnumerable<int> cardIds)
    {
        _context = context;
        SetId = setId;
        UserId = userId;
        CardIds = cardIds.ToList().AsReadOnly();
    }

    public async Task ExecuteAsync()
    {
        var cards = await _context.Flashcards
            .Where(f => f.FlashcardSetId == SetId && CardIds.Contains(f.Id))
            .ToListAsync();
        var progresses = await _context.UserProgresses
            .Where(p => CardIds.Contains(p.FlashcardId))
            .ToListAsync();
        var details = await _context.DictationSessionDetails
            .Where(d => CardIds.Contains(d.FlashcardId))
            .ToListAsync();

        _snapshots.Clear();
        _snapshots.AddRange(cards.Select(c => new FlashcardSnapshot
        {
            Id = c.Id,
            FlashcardSetId = c.FlashcardSetId,
            FrontText = c.FrontText,
            BackText = c.BackText,
            Pronunciation = c.Pronunciation,
            PartOfSpeech = c.PartOfSpeech,
            ExampleSentence = c.ExampleSentence,
            ExampleMeaning = c.ExampleMeaning,
            Synonyms = c.Synonyms,
            ImageUrl = c.ImageUrl,
            UploadedImagePath = c.UploadedImagePath,
            IsStarred = c.IsStarred,
            OrderIndex = c.OrderIndex,
            UserProgresses = progresses
                .Where(p => p.FlashcardId == c.Id)
                .Select(p => new UserProgressSnapshot
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    FlashcardId = p.FlashcardId,
                    IsLearned = p.IsLearned,
                    Status = p.Status,
                    CorrectCount = p.CorrectCount,
                    WrongCount = p.WrongCount,
                    LastReviewed = p.LastReviewed
                })
                .ToList(),
            DictationSessionDetails = details
                .Where(d => d.FlashcardId == c.Id)
                .Select(d => new DictationSessionDetailSnapshot
                {
                    Id = d.Id,
                    StudySessionId = d.StudySessionId,
                    FlashcardId = d.FlashcardId,
                    IsCorrect = d.IsCorrect,
                    AnsweredText = d.AnsweredText,
                    CreatedAt = d.CreatedAt
                })
                .ToList()
        }));

        _context.UserProgresses.RemoveRange(progresses);
        _context.DictationSessionDetails.RemoveRange(details);
        _context.Flashcards.RemoveRange(cards);
        await _context.SaveChangesAsync();
    }

    public async Task UndoAsync()
    {
        var cards = _snapshots.Select(s => new Flashcard
        {
            Id = s.Id,
            FlashcardSetId = s.FlashcardSetId,
            FrontText = s.FrontText,
            BackText = s.BackText,
            Pronunciation = s.Pronunciation,
            PartOfSpeech = s.PartOfSpeech,
            ExampleSentence = s.ExampleSentence,
            ExampleMeaning = s.ExampleMeaning,
            Synonyms = s.Synonyms,
            ImageUrl = s.ImageUrl,
            UploadedImagePath = s.UploadedImagePath,
            IsStarred = s.IsStarred,
            OrderIndex = s.OrderIndex
        }).ToList();
        _context.Flashcards.AddRange(cards);
        if (cards.Count > 0)
            await SaveWithIdentityInsertAsync<Flashcard>();

        var progresses = _snapshots.SelectMany(s =>
            s.UserProgresses.Select(p => new UserProgress
            {
                Id = p.Id,
                UserId = p.UserId,
                FlashcardId = p.FlashcardId,
                IsLearned = p.IsLearned,
                Status = p.Status,
                CorrectCount = p.CorrectCount,
                WrongCount = p.WrongCount,
                LastReviewed = p.LastReviewed
            })).ToList();
        _context.UserProgresses.AddRange(progresses);
        if (progresses.Count > 0)
            await SaveWithIdentityInsertAsync<UserProgress>();

        var details = _snapshots.SelectMany(s =>
            s.DictationSessionDetails.Select(d => new DictationSessionDetail
            {
                Id = d.Id,
                StudySessionId = d.StudySessionId,
                FlashcardId = d.FlashcardId,
                IsCorrect = d.IsCorrect,
                AnsweredText = d.AnsweredText,
                CreatedAt = d.CreatedAt
            })).ToList();
        _context.DictationSessionDetails.AddRange(details);
        if (details.Count > 0)
            await SaveWithIdentityInsertAsync<DictationSessionDetail>();
    }

    public string GetSnapshotJson() => JsonSerializer.Serialize(_snapshots);

    public void LoadSnapshot(string json)
    {
        _snapshots.Clear();
        _snapshots.AddRange(JsonSerializer.Deserialize<List<FlashcardSnapshot>>(json) ?? []);
    }

    private async Task SaveWithIdentityInsertAsync<TEntity>() where TEntity : class
    {
        var provider = _context.Database.ProviderName;
        if (provider?.Contains("SqlServer") == true)
        {
            var tableName = _context.Model.FindEntityType(typeof(TEntity))?.GetTableName()
                ?? throw new InvalidOperationException($"Could not resolve table name for {typeof(TEntity).Name}.");
            await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [{0}] ON", tableName);
            try
            {
                await _context.SaveChangesAsync();
            }
            finally
            {
                await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [{0}] OFF", tableName);
            }
        }
        else
        {
            await _context.SaveChangesAsync();
        }
    }
}
