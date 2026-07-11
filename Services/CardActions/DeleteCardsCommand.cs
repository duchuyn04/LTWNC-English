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

        await _context.UserProgresses
            .Where(p => CardIds.Contains(p.FlashcardId))
            .ExecuteDeleteAsync();
        _context.DictationSessionDetails.RemoveRange(details);
        _context.Flashcards.RemoveRange(cards);
        await _context.SaveChangesAsync();
    }

    public async Task UndoAsync()
    {
        foreach (var snapshot in _snapshots)
        {
            _context.Flashcards.Add(new Flashcard
            {
                Id = snapshot.Id,
                FlashcardSetId = snapshot.FlashcardSetId,
                FrontText = snapshot.FrontText,
                BackText = snapshot.BackText,
                Pronunciation = snapshot.Pronunciation,
                PartOfSpeech = snapshot.PartOfSpeech,
                ExampleSentence = snapshot.ExampleSentence,
                ExampleMeaning = snapshot.ExampleMeaning,
                Synonyms = snapshot.Synonyms,
                ImageUrl = snapshot.ImageUrl,
                UploadedImagePath = snapshot.UploadedImagePath,
                IsStarred = snapshot.IsStarred,
                OrderIndex = snapshot.OrderIndex
            });
        }

        if (_snapshots.Count > 0)
            await SaveFlashcardsWithIdentityAsync();

        var details = _snapshots.SelectMany(snapshot =>
            snapshot.DictationSessionDetails.Select(detail => new DictationSessionDetail
            {
                Id = detail.Id,
                StudySessionId = detail.StudySessionId,
                FlashcardId = detail.FlashcardId,
                IsCorrect = detail.IsCorrect,
                AnsweredText = detail.AnsweredText,
                CreatedAt = detail.CreatedAt
            }))
            .ToList();
        _context.DictationSessionDetails.AddRange(details);
        if (details.Count > 0)
            await SaveDictationDetailsWithIdentityAsync();
    }

    public string GetSnapshotJson() => JsonSerializer.Serialize(_snapshots);

    public void LoadSnapshot(string json)
    {
        _snapshots.Clear();
        _snapshots.AddRange(JsonSerializer.Deserialize<List<FlashcardSnapshot>>(json) ?? []);
    }

    private async Task SaveFlashcardsWithIdentityAsync()
    {
        await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Flashcards] ON");
        try
        {
            await _context.SaveChangesAsync();
        }
        finally
        {
            await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Flashcards] OFF");
        }
    }

    private async Task SaveDictationDetailsWithIdentityAsync()
    {
        await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [DictationSessionDetails] ON");
        try
        {
            await _context.SaveChangesAsync();
        }
        finally
        {
            await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [DictationSessionDetails] OFF");
        }
    }
}
