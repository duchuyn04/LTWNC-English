using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

// Command xóa nhiều thẻ. Snapshot gồm thẻ và dữ liệu học liên quan để Undo đủ.
public class DeleteCardsCommand : ICardActionCommand
{
    // Query / xóa / restore entity
    private readonly AppDbContext _context;

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
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `SetId` để các phương thức khác sử dụng.
        SetId = setId;
        // 3. Lưu dependency `UserId` để các phương thức khác sử dụng.
        UserId = userId;
        // 4. Lưu dependency `CardIds` để các phương thức khác sử dụng.
        CardIds = cardIds.ToList().AsReadOnly();
    }

    // Chụp thẻ và dữ liệu học liên quan rồi xóa theo thứ tự FK.
    public async Task<CardActionMemento> ExecuteAsync()
    {
        // 1. Gọi `ToListAsync` và lưu kết quả vào `cards`.
        List<Flashcard> cards = await CardActionTargetValidator.ValidateAsync(
            _context,
            SetId,
            UserId,
            CardIds);
        HashSet<int> validatedCardIds = cards.Select(card => card.Id).ToHashSet();

        // 2. Gọi `ToListAsync` và lưu kết quả vào `progresses`.
        List<UserProgress> progresses = await _context.UserProgresses
            .Where(progress => validatedCardIds.Contains(progress.FlashcardId))
            .ToListAsync();

        // 3. Gọi `ToListAsync` và lưu kết quả vào `details`.
        List<DictationSessionDetail> details = await _context.DictationSessionDetails
            .Where(detail => validatedCardIds.Contains(detail.FlashcardId))
            .ToListAsync();

        // 4. Gọi `ToListAsync` và lưu kết quả vào `missionWords`.
        List<EnglishMissionTargetWord> missionWords = await _context.EnglishMissionTargetWords
            .Where(word => validatedCardIds.Contains(word.FlashcardId))
            .ToListAsync();

        List<ReviewProgress> reviewProgresses = await _context.ReviewProgresses
            .Where(progress => validatedCardIds.Contains(progress.FlashcardId))
            .ToListAsync();
        List<ReviewSessionItem> reviewSessionItems = await _context.ReviewSessionItems
            .Where(item => validatedCardIds.Contains(item.FlashcardId))
            .ToListAsync();
        int[] affectedSessionIds = reviewSessionItems
            .Select(item => item.ReviewSessionId)
            .Distinct()
            .ToArray();
        List<ReviewSession> affectedSessions = affectedSessionIds.Length == 0
            ? []
            : await _context.ReviewSessions
                .Include(session => session.Items)
                .Where(session => affectedSessionIds.Contains(session.Id))
                .ToListAsync();
        if (affectedSessions.Count != affectedSessionIds.Length)
        {
            throw new InvalidOperationException("Dữ liệu Review hiện tại không nhất quán.");
        }

        List<ReviewSession> removedSessions = affectedSessions
            .Where(session =>
                session.CompletedAtUtc == null
                && session.EndedAtUtc == null
                && !session.Items.Any(item => !validatedCardIds.Contains(item.FlashcardId)))
            .ToList();
        HashSet<int> removedSessionIds = removedSessions
            .Select(session => session.Id)
            .ToHashSet();
        List<ReviewSessionSnapshot> reviewSessionSnapshots = affectedSessions
            .Select(session => new ReviewSessionSnapshot
            {
                WasRemoved = removedSessionIds.Contains(session.Id),
                Id = session.Id,
                UserId = session.UserId,
                FlashcardSetId = session.FlashcardSetId,
                SettingsSnapshotJson = session.SettingsSnapshotJson,
                StartedAtUtc = session.StartedAtUtc,
                CompletedAtUtc = session.CompletedAtUtc,
                EndedAtUtc = session.EndedAtUtc
            })
            .ToList();

        // Memento giữ snapshot cục bộ, command không lưu trạng thái Undo tạm thời.
        List<FlashcardSnapshot> snapshots = new();

        // 6. Duyệt từng `card` trong `cards` để xử lý lần lượt.
        foreach (Flashcard card in cards)
        {
            // Progress thuộc đúng thẻ này
            // 7. Khởi tạo `progressSnapshots` với dữ liệu ban đầu cần thiết.
            List<UserProgressSnapshot> progressSnapshots = new List<UserProgressSnapshot>();
            // 8. Duyệt từng `progress` trong `progresses` để xử lý lần lượt.
            foreach (UserProgress progress in progresses)
            {
                // 9. Kiểm tra `progress.FlashcardId != card.Id` để chọn nhánh xử lý phù hợp.
                if (progress.FlashcardId != card.Id)
                {
                    // 10. Bỏ qua phần còn lại và chuyển sang lần lặp tiếp theo.
                    continue;
                }

                // 11. Gọi `Add` để thực hiện bước nghiệp vụ này.
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
            // 12. Khởi tạo `detailSnapshots` với dữ liệu ban đầu cần thiết.
            List<DictationSessionDetailSnapshot> detailSnapshots = new List<DictationSessionDetailSnapshot>();
            // 13. Duyệt từng `detail` trong `details` để xử lý lần lượt.
            foreach (DictationSessionDetail detail in details)
            {
                // 14. Kiểm tra `detail.FlashcardId != card.Id` để chọn nhánh xử lý phù hợp.
                if (detail.FlashcardId != card.Id)
                {
                    // 15. Bỏ qua phần còn lại và chuyển sang lần lặp tiếp theo.
                    continue;
                }

                // 16. Gọi `Add` để thực hiện bước nghiệp vụ này.
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

            // 17. Gọi `ToList` và lưu kết quả vào `missionWordSnapshots`.
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

            List<ReviewProgressSnapshot> reviewProgressSnapshots = reviewProgresses
                .Where(progress => progress.FlashcardId == card.Id)
                .Select(progress => new ReviewProgressSnapshot
                {
                    Id = progress.Id,
                    UserId = progress.UserId,
                    FlashcardId = progress.FlashcardId,
                    Stage = progress.Stage,
                    NextReviewAtUtc = progress.NextReviewAtUtc,
                    LongTermIntervalDays = progress.LongTermIntervalDays,
                    LastRatedAtUtc = progress.LastRatedAtUtc
                })
                .ToList();
            List<ReviewSessionItemSnapshot> reviewSessionItemSnapshots = reviewSessionItems
                .Where(item => item.FlashcardId == card.Id)
                .Select(item => new ReviewSessionItemSnapshot
                {
                    Id = item.Id,
                    ReviewSessionId = item.ReviewSessionId,
                    FlashcardId = item.FlashcardId,
                    OrderIndex = item.OrderIndex,
                    IsNewCardAtAssignment = item.IsNewCardAtAssignment,
                    NewCardAssignedDate = item.NewCardAssignedDate,
                    Rating = item.Rating,
                    RatedAtUtc = item.RatedAtUtc,
                    PreviousStage = item.PreviousStage,
                    NextStage = item.NextStage,
                    PreviousNextReviewAtUtc = item.PreviousNextReviewAtUtc,
                    NextReviewAtUtc = item.NextReviewAtUtc,
                    PreviousLongTermIntervalDays = item.PreviousLongTermIntervalDays,
                    NextLongTermIntervalDays = item.NextLongTermIntervalDays
                })
                .ToList();

            // 18. Gọi `Add` để thực hiện bước nghiệp vụ này.
            snapshots.Add(new FlashcardSnapshot
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
                EnglishMissionTargetWords = missionWordSnapshots,
                ReviewProgresses = reviewProgressSnapshots,
                ReviewSessionItems = reviewSessionItemSnapshots
            });
        }

        _context.ReviewProgresses.RemoveRange(reviewProgresses);
        _context.ReviewSessionItems.RemoveRange(reviewSessionItems);
        _context.ReviewSessions.RemoveRange(removedSessions);

        // 19. Gọi `RemoveRange` để thực hiện bước nghiệp vụ này.
        _context.UserProgresses.RemoveRange(progresses);
        // 20. Gọi `RemoveRange` để thực hiện bước nghiệp vụ này.
        _context.DictationSessionDetails.RemoveRange(details);
        // 21. Gọi `RemoveRange` để thực hiện bước nghiệp vụ này.
        _context.EnglishMissionTargetWords.RemoveRange(missionWords);
        // 22. Gọi `RemoveRange` để thực hiện bước nghiệp vụ này.
        _context.Flashcards.RemoveRange(cards);
        // 23. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();

        return new CardActionMemento(JsonSerializer.Serialize(new DeleteCardsSnapshot
        {
            Cards = snapshots,
            ReviewSessions = reviewSessionSnapshots
        }));
    }

    // Restore thẻ và dữ liệu liên quan với đúng Id cũ (SQL Server: IDENTITY_INSERT)
    public async Task UndoAsync(CardActionMemento memento)
    {
        // Đọc và kiểm tra toàn bộ Memento trước khi bắt đầu khôi phục dữ liệu.
        DeleteCardsSnapshot state = RestoreSnapshot(memento);
        ValidateSnapshots(state);
        await ValidateRestoreConflictsAsync(state);
        List<FlashcardSnapshot> snapshots = state.Cards;

        // 1. Khởi tạo `cards` với dữ liệu ban đầu cần thiết.
        List<Flashcard> cards = new List<Flashcard>();
        // 2. Duyệt từng `snapshot` trong Memento để xử lý lần lượt.
        foreach (FlashcardSnapshot snapshot in snapshots)
        {
            // 3. Gọi `Add` để thực hiện bước nghiệp vụ này.
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

        // 4. Gọi `AddRange` để thực hiện bước nghiệp vụ này.
        _context.Flashcards.AddRange(cards);
        // 5. Kiểm tra `cards.Count > 0` để chọn nhánh xử lý phù hợp.
        if (cards.Count > 0)
        {
            // 6. Gọi `SaveWithIdentityInsertAsync` để thực hiện bước nghiệp vụ này.
            await SaveWithIdentityInsertAsync<Flashcard>();
        }

        List<ReviewSession> reviewSessions = state.ReviewSessions
            .Where(session => session.WasRemoved)
            .Select(session => new ReviewSession
            {
                Id = session.Id,
                UserId = session.UserId,
                FlashcardSetId = session.FlashcardSetId,
                SettingsSnapshotJson = session.SettingsSnapshotJson,
                StartedAtUtc = session.StartedAtUtc,
                CompletedAtUtc = session.CompletedAtUtc,
                EndedAtUtc = session.EndedAtUtc
            })
            .ToList();
        _context.ReviewSessions.AddRange(reviewSessions);
        if (reviewSessions.Count > 0)
        {
            await SaveWithIdentityInsertAsync<ReviewSession>();
        }

        // 7. Khởi tạo `progresses` với dữ liệu ban đầu cần thiết.
        List<UserProgress> progresses = new List<UserProgress>();
        // 8. Duyệt từng `snapshot` trong Memento để xử lý lần lượt.
        foreach (FlashcardSnapshot snapshot in snapshots)
        {
            // 9. Duyệt từng `progressSnapshot` trong `snapshot.UserProgresses` để xử lý lần lượt.
            foreach (UserProgressSnapshot progressSnapshot in snapshot.UserProgresses)
            {
                // 10. Gọi `Add` để thực hiện bước nghiệp vụ này.
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

        // 11. Gọi `AddRange` để thực hiện bước nghiệp vụ này.
        _context.UserProgresses.AddRange(progresses);
        // 12. Kiểm tra `progresses.Count > 0` để chọn nhánh xử lý phù hợp.
        if (progresses.Count > 0)
        {
            // 13. Gọi `SaveWithIdentityInsertAsync` để thực hiện bước nghiệp vụ này.
            await SaveWithIdentityInsertAsync<UserProgress>();
        }

        List<ReviewProgress> reviewProgresses = state.Cards
            .SelectMany(snapshot => snapshot.ReviewProgresses)
            .Select(progress => new ReviewProgress
            {
                Id = progress.Id,
                UserId = progress.UserId,
                FlashcardId = progress.FlashcardId,
                Stage = progress.Stage,
                NextReviewAtUtc = progress.NextReviewAtUtc,
                LongTermIntervalDays = progress.LongTermIntervalDays,
                LastRatedAtUtc = progress.LastRatedAtUtc
            })
            .ToList();
        _context.ReviewProgresses.AddRange(reviewProgresses);
        if (reviewProgresses.Count > 0)
        {
            await SaveWithIdentityInsertAsync<ReviewProgress>();
        }

        List<ReviewSessionItem> reviewSessionItems = state.Cards
            .SelectMany(snapshot => snapshot.ReviewSessionItems)
            .Select(item => new ReviewSessionItem
            {
                Id = item.Id,
                ReviewSessionId = item.ReviewSessionId,
                FlashcardId = item.FlashcardId,
                OrderIndex = item.OrderIndex,
                IsNewCardAtAssignment = item.IsNewCardAtAssignment,
                NewCardAssignedDate = item.NewCardAssignedDate,
                Rating = item.Rating,
                RatedAtUtc = item.RatedAtUtc,
                PreviousStage = item.PreviousStage,
                NextStage = item.NextStage,
                PreviousNextReviewAtUtc = item.PreviousNextReviewAtUtc,
                NextReviewAtUtc = item.NextReviewAtUtc,
                PreviousLongTermIntervalDays = item.PreviousLongTermIntervalDays,
                NextLongTermIntervalDays = item.NextLongTermIntervalDays
            })
            .ToList();
        _context.ReviewSessionItems.AddRange(reviewSessionItems);
        if (reviewSessionItems.Count > 0)
        {
            await SaveWithIdentityInsertAsync<ReviewSessionItem>();
        }

        // 14. Khởi tạo `details` với dữ liệu ban đầu cần thiết.
        List<DictationSessionDetail> details = new List<DictationSessionDetail>();
        // 15. Duyệt từng `snapshot` trong Memento để xử lý lần lượt.
        foreach (FlashcardSnapshot snapshot in snapshots)
        {
            // 16. Duyệt từng `detailSnapshot` trong `snapshot.DictationSessionDetails` để xử lý lần lượt.
            foreach (DictationSessionDetailSnapshot detailSnapshot in snapshot.DictationSessionDetails)
            {
                // 17. Gọi `Add` để thực hiện bước nghiệp vụ này.
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

        // 18. Gọi `AddRange` để thực hiện bước nghiệp vụ này.
        _context.DictationSessionDetails.AddRange(details);
        // 19. Kiểm tra `details.Count > 0` để chọn nhánh xử lý phù hợp.
        if (details.Count > 0)
        {
            // 20. Gọi `SaveWithIdentityInsertAsync` để thực hiện bước nghiệp vụ này.
            await SaveWithIdentityInsertAsync<DictationSessionDetail>();
        }

        // 21. Khởi tạo `missionWords` với dữ liệu ban đầu cần thiết.
        List<EnglishMissionTargetWord> missionWords = new List<EnglishMissionTargetWord>();
        // 22. Duyệt từng `snapshot` trong Memento để xử lý lần lượt.
        foreach (FlashcardSnapshot snapshot in snapshots)
        {
            // 23. Duyệt từng `wordSnapshot` trong `snapshot.EnglishMissionTargetWords` để xử lý lần lượt.
            foreach (EnglishMissionTargetWordSnapshot wordSnapshot in snapshot.EnglishMissionTargetWords)
            {
                // 24. Gọi `Add` để thực hiện bước nghiệp vụ này.
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

        // 25. Gọi `AddRange` để thực hiện bước nghiệp vụ này.
        _context.EnglishMissionTargetWords.AddRange(missionWords);
        // 26. Kiểm tra `missionWords.Count > 0` để chọn nhánh xử lý phù hợp.
        if (missionWords.Count > 0)
        {
            // 27. Gọi `SaveWithIdentityInsertAsync` để thực hiện bước nghiệp vụ này.
            await SaveWithIdentityInsertAsync<EnglishMissionTargetWord>();
        }
    }

    private static DeleteCardsSnapshot RestoreSnapshot(CardActionMemento? memento)
    {
        string json = memento?.StateJson?.TrimStart() ?? string.Empty;
        if (json.StartsWith("[", StringComparison.Ordinal))
        {
            return new DeleteCardsSnapshot
            {
                Cards = CardActionMemento.Restore<List<FlashcardSnapshot>>(memento)
            };
        }

        return CardActionMemento.Restore<DeleteCardsSnapshot>(memento);
    }

    // Kiểm tra cấu trúc và quan hệ trong snapshot trước khi ghi bất kỳ bản ghi nào.
    private void ValidateSnapshots(DeleteCardsSnapshot state)
    {
        List<FlashcardSnapshot> snapshots = state.Cards
            ?? throw CardActionMemento.InvalidMemento();
        List<ReviewSessionSnapshot> reviewSessions = state.ReviewSessions
            ?? throw CardActionMemento.InvalidMemento();
        HashSet<int> expectedCardIds = CardIds.ToHashSet();
        HashSet<int> snapshotCardIds = new();
        HashSet<int> progressIds = new();
        HashSet<int> detailIds = new();
        HashSet<int> missionWordIds = new();
        HashSet<int> reviewProgressIds = new();
        HashSet<(string UserId, int FlashcardId)> reviewProgressKeys = new();
        HashSet<int> reviewSessionItemIds = new();
        HashSet<(int SessionId, int FlashcardId)> reviewSessionItemCardKeys = new();
        HashSet<(int SessionId, int OrderIndex)> reviewSessionItemOrderKeys = new();
        HashSet<int> reviewSessionIds = new();
        HashSet<string> activeSessionUsers = new();
        HashSet<int> itemSessionIds = new();

        if (snapshots.Count == 0
            || expectedCardIds.Count == 0
            || CardIds.Count != expectedCardIds.Count)
        {
            throw CardActionMemento.InvalidMemento();
        }

        foreach (FlashcardSnapshot snapshot in snapshots)
        {
            if (snapshot is null)
            {
                throw CardActionMemento.InvalidMemento();
            }

            bool invalidCard = snapshot.Id <= 0
                || !snapshotCardIds.Add(snapshot.Id)
                || snapshot.FlashcardSetId != SetId
                || snapshot.FrontText is null
                || snapshot.BackText is null
                || snapshot.Pronunciation is null
                || snapshot.PartOfSpeech is null
                || snapshot.ExampleSentence is null
                || snapshot.ExampleMeaning is null;
            if (invalidCard)
            {
                throw CardActionMemento.InvalidMemento();
            }

            List<UserProgressSnapshot> progresses = snapshot.UserProgresses
                ?? throw CardActionMemento.InvalidMemento();
            List<DictationSessionDetailSnapshot> details = snapshot.DictationSessionDetails
                ?? throw CardActionMemento.InvalidMemento();
            List<EnglishMissionTargetWordSnapshot> missionWords = snapshot.EnglishMissionTargetWords
                ?? throw CardActionMemento.InvalidMemento();
            List<ReviewProgressSnapshot> reviewProgresses = snapshot.ReviewProgresses
                ?? throw CardActionMemento.InvalidMemento();
            List<ReviewSessionItemSnapshot> reviewItems = snapshot.ReviewSessionItems
                ?? throw CardActionMemento.InvalidMemento();

            bool invalidProgress = progresses.Any(progress =>
                progress is null
                || progress.Id <= 0
                || !progressIds.Add(progress.Id)
                || string.IsNullOrWhiteSpace(progress.UserId)
                || progress.FlashcardId != snapshot.Id);
            bool invalidDetail = details.Any(detail =>
                detail is null
                || detail.Id <= 0
                || !detailIds.Add(detail.Id)
                || detail.StudySessionId <= 0
                || detail.AnsweredText is null
                || detail.FlashcardId != snapshot.Id);
            bool invalidMissionWord = missionWords.Any(word =>
                word is null
                || word.Id <= 0
                || !missionWordIds.Add(word.Id)
                || word.EnglishMissionId <= 0
                || word.Term is null
                || word.Definition is null
                || word.FlashcardId != snapshot.Id);
            bool invalidReviewProgress = reviewProgresses.Any(progress =>
                progress is null
                || progress.Id <= 0
                || !reviewProgressIds.Add(progress.Id)
                || string.IsNullOrWhiteSpace(progress.UserId)
                || progress.FlashcardId != snapshot.Id
                || !Enum.IsDefined(progress.Stage)
                || !reviewProgressKeys.Add((progress.UserId, progress.FlashcardId)));
            bool invalidReviewItem = reviewItems.Any(item =>
                item is null
                || item.Id <= 0
                || !reviewSessionItemIds.Add(item.Id)
                || item.ReviewSessionId <= 0
                || item.FlashcardId != snapshot.Id
                || item.OrderIndex < 0
                || !Enum.IsDefined(item.PreviousStage)
                || !Enum.IsDefined(item.NextStage)
                || (item.Rating.HasValue && !Enum.IsDefined(item.Rating.Value))
                || !reviewSessionItemCardKeys.Add((item.ReviewSessionId, item.FlashcardId))
                || !reviewSessionItemOrderKeys.Add((item.ReviewSessionId, item.OrderIndex)));
            if (invalidProgress
                || invalidDetail
                || invalidMissionWord
                || invalidReviewProgress
                || invalidReviewItem)
            {
                throw CardActionMemento.InvalidMemento();
            }

            foreach (ReviewSessionItemSnapshot item in reviewItems)
            {
                itemSessionIds.Add(item.ReviewSessionId);
            }
        }

        foreach (ReviewSessionSnapshot session in reviewSessions)
        {
            bool invalidSession = session is null
                || session.Id <= 0
                || !reviewSessionIds.Add(session.Id)
                || string.IsNullOrWhiteSpace(session.UserId)
                || (session.FlashcardSetId.HasValue && session.FlashcardSetId != SetId)
                || (session.WasRemoved
                    && (session.CompletedAtUtc.HasValue || session.EndedAtUtc.HasValue))
                || (session.CompletedAtUtc == null
                    && session.EndedAtUtc == null
                    && !activeSessionUsers.Add(session.UserId))
                || !itemSessionIds.Contains(session.Id);
            if (invalidSession)
            {
                throw CardActionMemento.InvalidMemento();
            }
        }

        if (!snapshotCardIds.SetEquals(expectedCardIds)
            || itemSessionIds.Any(sessionId => !reviewSessionIds.Contains(sessionId)))
        {
            throw CardActionMemento.InvalidMemento();
        }
    }

    private async Task ValidateRestoreConflictsAsync(DeleteCardsSnapshot state)
    {
        HashSet<int> cardIds = state.Cards.Select(snapshot => snapshot.Id).ToHashSet();
        HashSet<int> progressIds = state.Cards
            .SelectMany(snapshot => snapshot.UserProgresses)
            .Select(progress => progress.Id)
            .ToHashSet();
        HashSet<int> detailIds = state.Cards
            .SelectMany(snapshot => snapshot.DictationSessionDetails)
            .Select(detail => detail.Id)
            .ToHashSet();
        HashSet<int> missionWordIds = state.Cards
            .SelectMany(snapshot => snapshot.EnglishMissionTargetWords)
            .Select(word => word.Id)
            .ToHashSet();
        HashSet<int> reviewProgressIds = state.Cards
            .SelectMany(snapshot => snapshot.ReviewProgresses)
            .Select(progress => progress.Id)
            .ToHashSet();
        HashSet<int> reviewSessionItemIds = state.Cards
            .SelectMany(snapshot => snapshot.ReviewSessionItems)
            .Select(item => item.Id)
            .ToHashSet();
        HashSet<int> deletedReviewSessionIds = state.ReviewSessions
            .Where(session => session.WasRemoved)
            .Select(session => session.Id)
            .ToHashSet();
        HashSet<int> retainedReviewSessionIds = state.ReviewSessions
            .Where(session => !session.WasRemoved)
            .Select(session => session.Id)
            .ToHashSet();

        await EnsureNoExistingAsync(
            _context.Flashcards.Where(card => cardIds.Contains(card.Id)),
            cardIds);
        await EnsureNoExistingAsync(
            _context.UserProgresses.Where(progress => progressIds.Contains(progress.Id)),
            progressIds);
        await EnsureNoExistingAsync(
            _context.DictationSessionDetails.Where(detail => detailIds.Contains(detail.Id)),
            detailIds);
        await EnsureNoExistingAsync(
            _context.EnglishMissionTargetWords.Where(word => missionWordIds.Contains(word.Id)),
            missionWordIds);
        await EnsureNoExistingAsync(
            _context.ReviewProgresses.Where(progress => reviewProgressIds.Contains(progress.Id)),
            reviewProgressIds);
        await EnsureNoExistingAsync(
            _context.ReviewSessionItems.Where(item => reviewSessionItemIds.Contains(item.Id)),
            reviewSessionItemIds);
        await EnsureNoExistingAsync(
            _context.ReviewSessions.Where(session => deletedReviewSessionIds.Contains(session.Id)),
            deletedReviewSessionIds);

        if (retainedReviewSessionIds.Count > 0)
        {
            List<ReviewSession> currentRetainedSessions = await _context.ReviewSessions
                .Where(session => retainedReviewSessionIds.Contains(session.Id))
                .ToListAsync();
            if (!currentRetainedSessions
                .Select(session => session.Id)
                .ToHashSet()
                .SetEquals(retainedReviewSessionIds))
            {
                throw CardActionMemento.InvalidMemento();
            }

            Dictionary<int, ReviewSession> currentSessionsById = currentRetainedSessions
                .ToDictionary(session => session.Id);
            foreach (ReviewSessionSnapshot snapshot in state.ReviewSessions.Where(session => !session.WasRemoved))
            {
                ReviewSession current = currentSessionsById[snapshot.Id];
                if (current.UserId != snapshot.UserId
                    || current.FlashcardSetId != snapshot.FlashcardSetId
                    || current.SettingsSnapshotJson != snapshot.SettingsSnapshotJson
                    || current.StartedAtUtc != snapshot.StartedAtUtc
                    || current.CompletedAtUtc != snapshot.CompletedAtUtc
                    || current.EndedAtUtc != snapshot.EndedAtUtc)
                {
                    throw RestoreConflict();
                }
            }
        }

        HashSet<(int SessionId, int FlashcardId)> restoredItemCardKeys = state.Cards
            .SelectMany(snapshot => snapshot.ReviewSessionItems)
            .Select(item => (item.ReviewSessionId, item.FlashcardId))
            .ToHashSet();
        HashSet<(int SessionId, int OrderIndex)> restoredItemOrderKeys = state.Cards
            .SelectMany(snapshot => snapshot.ReviewSessionItems)
            .Select(item => (item.ReviewSessionId, item.OrderIndex))
            .ToHashSet();
        HashSet<int> affectedSessionIds = state.Cards
            .SelectMany(snapshot => snapshot.ReviewSessionItems)
            .Select(item => item.ReviewSessionId)
            .ToHashSet();
        if (affectedSessionIds.Count > 0)
        {
            List<ReviewSessionItem> currentItems = await _context.ReviewSessionItems
                .Where(item => affectedSessionIds.Contains(item.ReviewSessionId))
                .ToListAsync();
            if (currentItems.Any(item =>
                !reviewSessionItemIds.Contains(item.Id)
                && (restoredItemCardKeys.Contains((item.ReviewSessionId, item.FlashcardId))
                    || restoredItemOrderKeys.Contains((item.ReviewSessionId, item.OrderIndex)))))
            {
                throw RestoreConflict();
            }
        }

        foreach (ReviewSessionSnapshot session in state.ReviewSessions.Where(session => session.WasRemoved))
        {
            bool hasNewActiveSession = await _context.ReviewSessions.AnyAsync(current =>
                current.Id != session.Id
                && current.UserId == session.UserId
                && current.CompletedAtUtc == null
                && current.EndedAtUtc == null);
            if (hasNewActiveSession)
            {
                throw RestoreConflict();
            }
        }
    }

    private static async Task EnsureNoExistingAsync<TEntity>(
        IQueryable<TEntity> query,
        IReadOnlyCollection<int> ids)
        where TEntity : class
    {
        if (ids.Count > 0 && await query.AnyAsync())
        {
            throw RestoreConflict();
        }
    }

    private static InvalidOperationException RestoreConflict()
        => new("Không thể hoàn tác vì dữ liệu hiện tại đã thay đổi.");

    // SQL Server: bật IDENTITY_INSERT theo bảng entity rồi SaveChanges, tắt trong finally.
    // Provider khác: SaveChanges thường (test SQLite).
    private async Task SaveWithIdentityInsertAsync<TEntity>() where TEntity : class
    {
        // 1. Tính giá trị và lưu vào `provider` để dùng ở bước tiếp theo.
        string? provider = _context.Database.ProviderName;
        // 2. Tính giá trị và lưu vào `isSqlServer` để dùng ở bước tiếp theo.
        bool isSqlServer = provider != null && provider.Contains("SqlServer");

        // 3. Kiểm tra `!isSqlServer` để chọn nhánh xử lý phù hợp.
        if (!isSqlServer)
        {
            // 4. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync();
            // 5. Kết thúc phương thức sau khi hoàn tất xử lý.
            return;
        }

        // 6. Khai báo `tableName` để lưu dữ liệu dùng ở các bước sau.
        string tableName;
        // 7. Tính giá trị và lưu vào `entityName` để dùng ở bước tiếp theo.
        string entityName = typeof(TEntity).Name;

        // 8. Kiểm tra `entityName == nameof(Flashcard)` để chọn nhánh xử lý phù hợp.
        if (entityName == nameof(Flashcard))
        {
            // 9. Cập nhật `tableName` bằng giá trị mới.
            tableName = "Flashcards";
        }
        else if (entityName == nameof(UserProgress))
        {
            // 10. Cập nhật `tableName` bằng giá trị mới.
            tableName = "UserProgresses";
        }
        else if (entityName == nameof(DictationSessionDetail))
        {
            // 11. Cập nhật `tableName` bằng giá trị mới.
            tableName = "DictationSessionDetails";
        }
        else if (entityName == nameof(EnglishMissionTargetWord))
        {
            // 12. Cập nhật `tableName` bằng giá trị mới.
            tableName = "EnglishMissionTargetWords";
        }
        else if (entityName == nameof(ReviewProgress))
        {
            tableName = "ReviewProgresses";
        }
        else if (entityName == nameof(ReviewSession))
        {
            tableName = "ReviewSessions";
        }
        else if (entityName == nameof(ReviewSessionItem))
        {
            tableName = "ReviewSessionItems";
        }
        else
        {
            // 13. Dừng xử lý và phát sinh lỗi `new InvalidOperationException($"Unknown entity type {entityName}.")`.
            throw new InvalidOperationException($"Unknown entity type {entityName}.");
        }

#pragma warning disable EF1002 // tableName chỉ map từ tên entity cố định, không nhận input user
        // 14. Gọi `ExecuteSqlRawAsync` để thực hiện bước nghiệp vụ này.
        await _context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{tableName}] ON");
#pragma warning restore EF1002
        // 15. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 16. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync();
        }
        finally
        {
#pragma warning disable EF1002
            // 17. Gọi `ExecuteSqlRawAsync` để thực hiện bước nghiệp vụ này.
            await _context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [{tableName}] OFF");
#pragma warning restore EF1002
        }
    }
}
