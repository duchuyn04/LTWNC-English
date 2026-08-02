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

    // Chụp thẻ + progress + dictation detail rồi xóa (FK: xóa con trước)
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
                EnglishMissionTargetWords = missionWordSnapshots
            });
        }

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

        return new CardActionMemento(JsonSerializer.Serialize(snapshots));
    }

    // Restore thẻ / progress / detail với đúng Id cũ (SQL Server: IDENTITY_INSERT)
    public async Task UndoAsync(CardActionMemento memento)
    {
        // Đọc và kiểm tra toàn bộ Memento trước khi bắt đầu khôi phục dữ liệu.
        List<FlashcardSnapshot> snapshots = CardActionMemento.Restore<List<FlashcardSnapshot>>(memento);
        ValidateSnapshots(snapshots);

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

    // Kiểm tra cấu trúc và quan hệ trong snapshot trước khi ghi bất kỳ bản ghi nào.
    private void ValidateSnapshots(List<FlashcardSnapshot> snapshots)
    {
        HashSet<int> expectedCardIds = CardIds.ToHashSet();
        HashSet<int> snapshotCardIds = new();
        HashSet<int> progressIds = new();
        HashSet<int> detailIds = new();
        HashSet<int> missionWordIds = new();

        if (snapshots.Count == 0 || expectedCardIds.Count == 0)
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

            bool invalidProgress = progresses.Any(progress =>
                progress is null
                || progress.Id <= 0
                || !progressIds.Add(progress.Id)
                || progress.UserId is null
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
            if (invalidProgress || invalidDetail || invalidMissionWord)
            {
                throw CardActionMemento.InvalidMemento();
            }
        }

        if (!snapshotCardIds.SetEquals(expectedCardIds))
        {
            throw CardActionMemento.InvalidMemento();
        }
    }

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
