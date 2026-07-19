using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

// Command xóa nhiều thẻ. Snapshot gồm thẻ + progress + dictation detail để Undo đủ.
public class DeleteCardsCommand : ICardActionCommand
{
    // Query / xóa / restore entity
    private readonly AppDbContext _context;

    // Snapshot từng thẻ sau Execute (hoặc sau LoadSnapshot)
    private readonly List<FlashcardSnapshot> _snapshots = new();

    // Cố định "Delete"
    public string ActionType => "Delete";

    // Bộ thẻ chứa thẻ bị xóa
    public int SetId { get; }

    // User thực hiện
    public string UserId { get; }

    // Id thẻ cần xóa
    public IReadOnlyList<int> CardIds { get; }

    // Tạo command với set, user và danh sách card id
    public DeleteCardsCommand(AppDbContext context, int setId, string userId, IEnumerable<int> cardIds)
    {
        _context = context;
        SetId = setId;
        UserId = userId;
        CardIds = cardIds.ToList().AsReadOnly();
    }

    // Chụp thẻ + progress + dictation detail rồi xóa (FK: xóa con trước)
    public async Task ExecuteAsync()
    {
        List<Flashcard> cards = await _context.Flashcards
            .Where(flashcard =>
                flashcard.FlashcardSetId == SetId
                && CardIds.Contains(flashcard.Id))
            .ToListAsync();

        List<UserProgress> progresses = await _context.UserProgresses
            .Where(progress => CardIds.Contains(progress.FlashcardId))
            .ToListAsync();

        List<DictationSessionDetail> details = await _context.DictationSessionDetails
            .Where(detail => CardIds.Contains(detail.FlashcardId))
            .ToListAsync();

        List<EnglishMissionTargetWord> missionWords = await _context.EnglishMissionTargetWords
            .Where(word => CardIds.Contains(word.FlashcardId))
            .ToListAsync();

        _snapshots.Clear();

        foreach (Flashcard card in cards)
        {
            // Progress thuộc đúng thẻ này
            List<UserProgressSnapshot> progressSnapshots = new List<UserProgressSnapshot>();
            foreach (UserProgress progress in progresses)
            {
                if (progress.FlashcardId != card.Id)
                {
                    continue;
                }

                progressSnapshots.Add(new UserProgressSnapshot
                {
                    Id = progress.Id,
                    UserId = progress.UserId,
                    FlashcardId = progress.FlashcardId,
                    IsLearned = progress.IsLearned,
                    Status = progress.Status,
                    CorrectCount = progress.CorrectCount,
                    WrongCount = progress.WrongCount,
                    LastReviewed = progress.LastReviewed
                });
            }

            // Detail dictation thuộc đúng thẻ này
            List<DictationSessionDetailSnapshot> detailSnapshots = new List<DictationSessionDetailSnapshot>();
            foreach (DictationSessionDetail detail in details)
            {
                if (detail.FlashcardId != card.Id)
                {
                    continue;
                }

                detailSnapshots.Add(new DictationSessionDetailSnapshot
                {
                    Id = detail.Id,
                    StudySessionId = detail.StudySessionId,
                    FlashcardId = detail.FlashcardId,
                    IsCorrect = detail.IsCorrect,
                    AnsweredText = detail.AnsweredText,
                    CreatedAt = detail.CreatedAt
                });
            }

            List<EnglishMissionTargetWordSnapshot> missionWordSnapshots = missionWords
                .Where(word => word.FlashcardId == card.Id)
                .Select(word => new EnglishMissionTargetWordSnapshot
                {
                    Id = word.Id,
                    EnglishMissionId = word.EnglishMissionId,
                    FlashcardId = word.FlashcardId,
                    Term = word.Term,
                    Definition = word.Definition,
                    PartOfSpeech = word.PartOfSpeech,
                    ExampleSentence = word.ExampleSentence,
                    IsUsed = word.IsUsed,
                    FirstUsedTurn = word.FirstUsedTurn
                })
                .ToList();

            _snapshots.Add(new FlashcardSnapshot
            {
                Id = card.Id,
                FlashcardSetId = card.FlashcardSetId,
                FrontText = card.FrontText,
                BackText = card.BackText,
                Pronunciation = card.Pronunciation,
                PartOfSpeech = card.PartOfSpeech,
                ExampleSentence = card.ExampleSentence,
                ExampleMeaning = card.ExampleMeaning,
                Synonyms = card.Synonyms,
                ImageUrl = card.ImageUrl,
                UploadedImagePath = card.UploadedImagePath,
                IsStarred = card.IsStarred,
                OrderIndex = card.OrderIndex,
                UserProgresses = progressSnapshots,
                DictationSessionDetails = detailSnapshots,
                EnglishMissionTargetWords = missionWordSnapshots
            });
        }

        _context.UserProgresses.RemoveRange(progresses);
        _context.DictationSessionDetails.RemoveRange(details);
        _context.EnglishMissionTargetWords.RemoveRange(missionWords);
        _context.Flashcards.RemoveRange(cards);
        await _context.SaveChangesAsync();
    }

    // Restore thẻ / progress / detail với đúng Id cũ (SQL Server: IDENTITY_INSERT)
    public async Task UndoAsync()
    {
        List<Flashcard> cards = new List<Flashcard>();
        foreach (FlashcardSnapshot snapshot in _snapshots)
        {
            cards.Add(new Flashcard
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

        _context.Flashcards.AddRange(cards);
        if (cards.Count > 0)
        {
            await SaveWithIdentityInsertAsync<Flashcard>();
        }

        List<UserProgress> progresses = new List<UserProgress>();
        foreach (FlashcardSnapshot snapshot in _snapshots)
        {
            foreach (UserProgressSnapshot progressSnapshot in snapshot.UserProgresses)
            {
                progresses.Add(new UserProgress
                {
                    Id = progressSnapshot.Id,
                    UserId = progressSnapshot.UserId,
                    FlashcardId = progressSnapshot.FlashcardId,
                    IsLearned = progressSnapshot.IsLearned,
                    Status = progressSnapshot.Status,
                    CorrectCount = progressSnapshot.CorrectCount,
                    WrongCount = progressSnapshot.WrongCount,
                    LastReviewed = progressSnapshot.LastReviewed
                });
            }
        }

        _context.UserProgresses.AddRange(progresses);
        if (progresses.Count > 0)
        {
            await SaveWithIdentityInsertAsync<UserProgress>();
        }

        List<DictationSessionDetail> details = new List<DictationSessionDetail>();
        foreach (FlashcardSnapshot snapshot in _snapshots)
        {
            foreach (DictationSessionDetailSnapshot detailSnapshot in snapshot.DictationSessionDetails)
            {
                details.Add(new DictationSessionDetail
                {
                    Id = detailSnapshot.Id,
                    StudySessionId = detailSnapshot.StudySessionId,
                    FlashcardId = detailSnapshot.FlashcardId,
                    IsCorrect = detailSnapshot.IsCorrect,
                    AnsweredText = detailSnapshot.AnsweredText,
                    CreatedAt = detailSnapshot.CreatedAt
                });
            }
        }

        _context.DictationSessionDetails.AddRange(details);
        if (details.Count > 0)
        {
            await SaveWithIdentityInsertAsync<DictationSessionDetail>();
        }

        List<EnglishMissionTargetWord> missionWords = new List<EnglishMissionTargetWord>();
        foreach (FlashcardSnapshot snapshot in _snapshots)
        {
            foreach (EnglishMissionTargetWordSnapshot wordSnapshot in snapshot.EnglishMissionTargetWords)
            {
                missionWords.Add(new EnglishMissionTargetWord
                {
                    Id = wordSnapshot.Id,
                    EnglishMissionId = wordSnapshot.EnglishMissionId,
                    FlashcardId = wordSnapshot.FlashcardId,
                    Term = wordSnapshot.Term,
                    Definition = wordSnapshot.Definition,
                    PartOfSpeech = wordSnapshot.PartOfSpeech,
                    ExampleSentence = wordSnapshot.ExampleSentence,
                    IsUsed = wordSnapshot.IsUsed,
                    FirstUsedTurn = wordSnapshot.FirstUsedTurn
                });
            }
        }

        _context.EnglishMissionTargetWords.AddRange(missionWords);
        if (missionWords.Count > 0)
        {
            await SaveWithIdentityInsertAsync<EnglishMissionTargetWord>();
        }
    }

    // Serialize list FlashcardSnapshot
    public string GetSnapshotJson()
    {
        return JsonSerializer.Serialize(_snapshots);
    }

    // Deserialize list FlashcardSnapshot vào _snapshots
    public void LoadSnapshot(string json)
    {
        _snapshots.Clear();

        List<FlashcardSnapshot>? loaded =
            JsonSerializer.Deserialize<List<FlashcardSnapshot>>(json);

        if (loaded != null)
        {
            _snapshots.AddRange(loaded);
        }
    }

    // SQL Server: bật IDENTITY_INSERT theo bảng entity rồi SaveChanges, tắt trong finally.
    // Provider khác: SaveChanges thường (test SQLite).
    private async Task SaveWithIdentityInsertAsync<TEntity>() where TEntity : class
    {
        string? provider = _context.Database.ProviderName;
        bool isSqlServer = provider != null && provider.Contains("SqlServer");

        if (!isSqlServer)
        {
            await _context.SaveChangesAsync();
            return;
        }

        string tableName;
        string entityName = typeof(TEntity).Name;

        if (entityName == nameof(Flashcard))
        {
            tableName = "Flashcards";
        }
        else if (entityName == nameof(UserProgress))
        {
            tableName = "UserProgresses";
        }
        else if (entityName == nameof(DictationSessionDetail))
        {
            tableName = "DictationSessionDetails";
        }
        else if (entityName == nameof(EnglishMissionTargetWord))
        {
            tableName = "EnglishMissionTargetWords";
        }
        else
        {
            throw new InvalidOperationException($"Unknown entity type {entityName}.");
        }

#pragma warning disable EF1002 // tableName chỉ map từ tên entity cố định, không nhận input user
        await _context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{tableName}] ON");
#pragma warning restore EF1002
        try
        {
            await _context.SaveChangesAsync();
        }
        finally
        {
#pragma warning disable EF1002
            await _context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{tableName}] OFF");
#pragma warning restore EF1002
        }
    }
}
