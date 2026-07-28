using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.StudyModes;
using ltwnc.Services.StudyEvents;

namespace ltwnc.Services.Study;

// Kết quả chấm một câu nghe chép (API / JS đọc field này)
public class DictationCheckResult
{
    // User đúng hay sai
    public bool IsCorrect { get; set; }

    // Chuỗi đáp án chuẩn (term hoặc example sentence)
    public string CorrectAnswer { get; set; } = string.Empty;

    // Gợi ý khi sai (IPA / nghĩa); đúng thì null
    public string? Hint { get; set; }

    // Nghĩa câu ví dụ (chỉ mode ExampleSentence)
    public string? ExampleMeaning { get; set; }

    // So từng từ (mode ExampleSentence); mode Vocabulary thường rỗng
    public List<DictationWordComparison> WordComparison { get; set; } = new();
}

// Trạng thái từng từ khi so answered vs correct
public enum DictationWordStatus
{
    // Khớp
    Correct,
    // Cùng vị trí alignment nhưng khác chữ
    Incorrect,
    // Thiếu so với đáp án đúng
    Missing,
    // Thừa so với đáp án đúng
    Extra
}

// Một ô so sánh từ trong alignment
public class DictationWordComparison
{
    public DictationWordStatus Status { get; set; }

    // Từ user gõ (Missing thì null)
    public string? AnsweredWord { get; set; }

    // Từ đáp án (Extra thì null)
    public string? CorrectWord { get; set; }
}

// Tổng kết phiên nghe chép
public class DictationResult
{
    public int SessionId { get; set; }

    // Vocabulary hay ExampleSentence trong phiên
    public DictationContentMode ContentMode { get; set; }

    // Số câu đã trả lời
    public int TotalCards { get; set; }

    // Số câu đúng
    public int CorrectCount { get; set; }

    // Điểm lưu trên StudySession
    public int Score { get; set; }

    // Thẻ trả lời sai (ôn lại)
    public List<DictationResultCard> WrongCards { get; set; } = new();
}

// Một thẻ sai trên màn kết quả
public class DictationResultCard
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Pronunciation { get; set; } = string.Empty;
    public string ExampleSentence { get; set; } = string.Empty;
    public string ExampleMeaning { get; set; } = string.Empty;
}

public class DictationRetryPlan
{
    public DictationContentMode ContentMode { get; set; }
    public List<Flashcard> Cards { get; set; } = new();
}

public class DictationHistoryItem
{
    public int SessionId { get; set; }
    public string PromptText { get; set; } = string.Empty;
    public string AnsweredText { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public DateTime AnsweredAt { get; set; }
}

// Nghiệp vụ nghe chép: lấy thẻ, chấm đáp án, đóng phiên, phát Observer.
// Không tự tính huy hiệu.
public class DictationService : IDictationService
{
    // Session, detail, progress, flashcard
    private readonly AppDbContext _context;

    // Lấy DictationModeStrategy (cùng lọc với Study Hub)
    private readonly IStudyModeStrategyResolver _strategyResolver;

    // Publish sau khi chấm / complete session
    private readonly IStudyEventPublisher _studyEvents;
    private readonly TimeProvider _timeProvider;

    // Inject DbContext, resolver, publisher
    public DictationService(
        AppDbContext context,
        IStudyModeStrategyResolver strategyResolver,
        IStudyEventPublisher studyEvents,
        TimeProvider? timeProvider = null)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_strategyResolver` để các phương thức khác sử dụng.
        _strategyResolver = strategyResolver;
        // 3. Lưu dependency `_studyEvents` để các phương thức khác sử dụng.
        _studyEvents = studyEvents;
        // 4. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    // Lấy danh sách thẻ cho màn hình Dictation.
    // Không lặp logic lọc: dùng DictationModeStrategy để hub và trang Dictation cùng tập thẻ.
    // Sau đó chỉ xáo trộn theo cài đặt của user.
    public async Task<List<Flashcard>> GetCardsForDictationAsync(
        int setId,
        string userId,
        UserStudySettings settings)
    {
        // 1. Gọi `Resolve` và lưu kết quả vào `strategy`.
        IStudyModeStrategy strategy = _strategyResolver.Resolve(StudyMode.Dictation);
        // 2. Gọi `GetCardsAsync` và lưu kết quả vào `cards`.
        List<Flashcard> cards = await strategy.GetCardsAsync(setId, settings, userId);

        // Xáo trộn nếu user bật DictationShuffle
        // 3. Kiểm tra `settings.DictationShuffle` để chọn nhánh xử lý phù hợp.
        if (settings.DictationShuffle)
        {
            // 4. Cập nhật `cards` bằng giá trị mới.
            cards = Shuffle(cards);
        }

        // 5. Trả `cards` cho nơi gọi.
        return cards;
    }

    // Kiểm tra bộ thẻ có bất kỳ thẻ nào có câu ví dụ không (bỏ qua bộ lọc)
    public async Task<bool> AnyCardHasExampleSentenceAsync(int setId)
    {
        // 1. Gọi `AnyAsync` và lưu kết quả vào `hasExample`.
        bool hasExample = await _context.Flashcards.AnyAsync(flashcard =>
            flashcard.FlashcardSetId == setId
            && flashcard.ExampleSentence.Trim() != "");

        // 2. Trả `hasExample` cho nơi gọi.
        return hasExample;
    }

    // Xáo trộn danh sách bằng thuật toán Fisher-Yates
    private static List<T> Shuffle<T>(List<T> list)
    {
        // 1. Khởi tạo `random` với dữ liệu ban đầu cần thiết.
        Random random = new Random();
        // 2. Khởi tạo `result` với dữ liệu ban đầu cần thiết.
        List<T> result = new List<T>(list);

        // 3. Lặp qua phạm vi dữ liệu cần xử lý.
        for (int i = result.Count - 1; i > 0; i--)
        {
            // 4. Gọi `Next` và lưu kết quả vào `j`.
            int j = random.Next(i + 1);

            // Đổi chỗ hai phần tử
            // 5. Tính giá trị và lưu vào `temp` để dùng ở bước tiếp theo.
            T temp = result[i];
            // 6. Cập nhật `result[i]` bằng giá trị mới.
            result[i] = result[j];
            // 7. Cập nhật `result[j]` bằng giá trị mới.
            result[j] = temp;
        }

        // 8. Trả `result` cho nơi gọi.
        return result;
    }

    // Tạo phiên học Dictation mới
    public async Task<StudySession> CreateSessionAsync(
        string userId,
        int setId,
        DictationContentMode contentMode = DictationContentMode.Vocabulary,
        int plannedItemCount = 0,
        IReadOnlyList<Flashcard>? cards = null)
    {
        // 1. Tính giá trị và lưu vào `itemCount` để dùng ở bước tiếp theo.
        int itemCount = cards?.Count ?? Math.Max(0, plannedItemCount);
        // 2. Khởi tạo `session` với dữ liệu ban đầu cần thiết.
        StudySession session = new StudySession
        {
            UserId = userId,
            FlashcardSetId = setId,
            Mode = StudyMode.Dictation,
            DictationContentMode = contentMode,
            PlannedItemCount = itemCount,
            StartedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        // 3. Gọi `AddAsync` để thực hiện bước nghiệp vụ này.
        await _context.StudySessions.AddAsync(session);

        // 4. Kiểm tra `cards != null` để chọn nhánh xử lý phù hợp.
        if (cards != null)
        {
            // 5. Lặp qua phạm vi dữ liệu cần xử lý.
            for (int index = 0; index < cards.Count; index++)
            {
                // 6. Tính giá trị và lưu vào `card` để dùng ở bước tiếp theo.
                Flashcard card = cards[index];
                // 7. Tính giá trị và lưu vào `promptText` để dùng ở bước tiếp theo.
                string promptText = contentMode == DictationContentMode.ExampleSentence
                    ? card.ExampleSentence
                    : card.FrontText;

                // 8. Gọi `AddAsync` để thực hiện bước nghiệp vụ này.
                await _context.DictationSessionQuestions.AddAsync(new DictationSessionQuestion
                {
                    StudySession = session,
                    FlashcardId = card.Id,
                    OrderIndex = index,
                    PromptText = promptText,
                    CorrectAnswer = promptText,
                    Term = card.FrontText,
                    Definition = card.BackText,
                    Pronunciation = card.Pronunciation,
                    ExampleSentence = card.ExampleSentence,
                    ExampleMeaning = card.ExampleMeaning,
                    Synonyms = card.Synonyms
                });
            }
        }

        // 9. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();
        // 10. Trả `session` cho nơi gọi.
        return session;
    }

    public async Task<DictationRetryPlan> GetRetryPlanAsync(
        int sourceSessionId,
        int setId,
        string userId)
    {
        // 1. Gọi `GetOwnedDictationSessionAsync` và lưu kết quả vào `session`.
        StudySession session = await GetOwnedDictationSessionAsync(
            sourceSessionId,
            setId,
            userId,
            requireCompleted: true);

        // 2. Gọi `ToListAsync` và lưu kết quả vào `cardIds`.
        List<int> cardIds = await _context.DictationSessionQuestions
            .AsNoTracking()
            .Where(question =>
                question.StudySessionId == sourceSessionId
                && question.IsCorrect == false)
            .OrderBy(question => question.OrderIndex)
            .Select(question => question.FlashcardId)
            .ToListAsync();

        // 3. Kiểm tra `cardIds.Count == 0` để chọn nhánh xử lý phù hợp.
        if (cardIds.Count == 0)
        {
            // 4. Cập nhật `cardIds` bằng giá trị mới.
            cardIds = await _context.DictationSessionDetails
                .AsNoTracking()
                .Where(detail =>
                    detail.StudySessionId == sourceSessionId
                    && !detail.IsCorrect)
                .OrderBy(detail => detail.Id)
                .Select(detail => detail.FlashcardId)
                .Distinct()
                .ToListAsync();
        }

        // 5. Gọi `ToDictionaryAsync` và lưu kết quả vào `cardsById`.
        Dictionary<int, Flashcard> cardsById = await _context.Flashcards
            .Where(card =>
                card.FlashcardSetId == setId
                && cardIds.Contains(card.Id))
            .ToDictionaryAsync(card => card.Id);

        // 6. Gọi `ToList` và lưu kết quả vào `cards`.
        List<Flashcard> cards = cardIds
            .Where(cardsById.ContainsKey)
            .Select(cardId => cardsById[cardId])
            .ToList();

        // 7. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new DictationRetryPlan
        {
            ContentMode = session.DictationContentMode,
            Cards = cards
        };
    }

    public async Task<List<DictationHistoryItem>> GetHistoryAsync(
        int setId,
        string userId,
        int limit = 100)
    {
        // 1. Gọi `AnyAsync` và lưu kết quả vào `ownsSet`.
        bool ownsSet = await _context.FlashcardSets
            .AsNoTracking()
            .AnyAsync(set => set.Id == setId && set.UserId == userId);
        // 2. Kiểm tra `!ownsSet` để chọn nhánh xử lý phù hợp.
        if (!ownsSet)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền xem lịch sử bộ thẻ ...`.
            throw new UnauthorizedAccessException("Không có quyền xem lịch sử bộ thẻ này.");
        }

        // 4. Gọi `Clamp` và lưu kết quả vào `safeLimit`.
        int safeLimit = Math.Clamp(limit, 1, 500);
        // 5. Gọi `ToListAsync` và lưu kết quả vào `snapshotItems`.
        List<DictationHistoryItem> snapshotItems = await _context.DictationSessionQuestions
            .AsNoTracking()
            .Where(question =>
                question.StudySession != null
                && question.StudySession.UserId == userId
                && question.StudySession.FlashcardSetId == setId
                && question.StudySession.Mode == StudyMode.Dictation
                && question.IsCorrect == false
                && question.AnsweredAt.HasValue)
            .OrderByDescending(question => question.AnsweredAt)
            .Take(safeLimit)
            .Select(question => new DictationHistoryItem
            {
                SessionId = question.StudySessionId,
                PromptText = question.PromptText,
                AnsweredText = question.AnsweredText ?? string.Empty,
                CorrectAnswer = question.CorrectAnswer,
                Definition = question.Definition,
                AnsweredAt = question.AnsweredAt!.Value
            })
            .ToListAsync();

        // 6. Kiểm tra `snapshotItems.Count >= safeLimit` để chọn nhánh xử lý phù hợp.
        if (snapshotItems.Count >= safeLimit)
        {
            // 7. Trả `snapshotItems` cho nơi gọi.
            return snapshotItems;
        }

        // 8. Gọi `ToListAsync` và lưu kết quả vào `legacyItems`.
        List<DictationHistoryItem> legacyItems = await _context.DictationSessionDetails
            .AsNoTracking()
            .Where(detail =>
                detail.StudySession != null
                && detail.StudySession.UserId == userId
                && detail.StudySession.FlashcardSetId == setId
                && detail.StudySession.Mode == StudyMode.Dictation
                && !detail.IsCorrect
                && !_context.DictationSessionQuestions.Any(question =>
                    question.StudySessionId == detail.StudySessionId))
            .Include(detail => detail.Flashcard)
            .OrderByDescending(detail => detail.CreatedAt)
            .Take(safeLimit - snapshotItems.Count)
            .Select(detail => new DictationHistoryItem
            {
                SessionId = detail.StudySessionId,
                PromptText = detail.Flashcard != null
                    ? detail.Flashcard.FrontText
                    : string.Empty,
                AnsweredText = detail.AnsweredText,
                CorrectAnswer = detail.Flashcard != null
                    ? detail.Flashcard.FrontText
                    : string.Empty,
                Definition = detail.Flashcard != null
                    ? detail.Flashcard.BackText
                    : string.Empty,
                AnsweredAt = detail.CreatedAt
            })
            .ToListAsync();

        // 9. Trả kết quả từ `ToList` cho nơi gọi.
        return snapshotItems
            .Concat(legacyItems)
            .OrderByDescending(item => item.AnsweredAt)
            .Take(safeLimit)
            .ToList();
    }

    // Kiểm tra đáp án của người dùng
    public async Task<DictationCheckResult> CheckAnswerAsync(
        int sessionId,
        int setId,
        int cardId,
        string answeredText,
        string userId,
        bool acceptSynonyms)
    {
        // 1. Gọi `GetOwnedDictationSessionAsync` và lưu kết quả vào `session`.
        StudySession session = await GetOwnedDictationSessionAsync(
            sessionId,
            setId,
            userId,
            requireCompleted: false);
        // 2. Kiểm tra `session.CompletedAt.HasValue` để chọn nhánh xử lý phù hợp.
        if (session.CompletedAt.HasValue)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new InvalidOperationException("Phiên nghe chép đã hoàn thành.")`.
            throw new InvalidOperationException("Phiên nghe chép đã hoàn thành.");
        }

        // 4. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `question`.
        DictationSessionQuestion? question = await _context.DictationSessionQuestions
            .SingleOrDefaultAsync(row =>
                row.StudySessionId == sessionId
                && row.FlashcardId == cardId);
        // 5. Kiểm tra `question == null` để chọn nhánh xử lý phù hợp.
        if (question == null)
        {
            // 6. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Thẻ không thuộc phiên nghe chép này.")`.
            throw new KeyNotFoundException("Thẻ không thuộc phiên nghe chép này.");
        }

        // 7. Kiểm tra `question.IsCorrect.HasValue` để chọn nhánh xử lý phù hợp.
        if (question.IsCorrect.HasValue)
        {
            // 8. Trả kết quả từ `BuildCheckResult` cho nơi gọi.
            return BuildCheckResult(session, question);
        }

        // 9. Khởi tạo `acceptedAnswers` với dữ liệu ban đầu cần thiết.
        List<string> acceptedAnswers = new List<string> { question.CorrectAnswer };
        // 10. Tính giá trị và lưu vào `canAcceptSynonyms` để dùng ở bước tiếp theo.
        bool canAcceptSynonyms =
            session.DictationContentMode == DictationContentMode.Vocabulary
            && acceptSynonyms
            && !string.IsNullOrWhiteSpace(question.Synonyms);

        // 11. Kiểm tra `canAcceptSynonyms` để chọn nhánh xử lý phù hợp.
        if (canAcceptSynonyms)
        {
            // 12. Duyệt từng `part` trong `question.Synonyms!.Split( new[] { ',', ';' }, StringSplitOptions.Re...` để xử lý lần lượt.
            foreach (string part in question.Synonyms!.Split(
                new[] { ',', ';' },
                StringSplitOptions.RemoveEmptyEntries))
            {
                // 13. Gọi `Trim` và lưu kết quả vào `synonym`.
                string synonym = part.Trim();
                // 14. Kiểm tra `!string.IsNullOrWhiteSpace(synonym)` để chọn nhánh xử lý phù hợp.
                if (!string.IsNullOrWhiteSpace(synonym))
                {
                    // 15. Gọi `Add` để thực hiện bước nghiệp vụ này.
                    acceptedAnswers.Add(synonym);
                }
            }
        }

        // 16. Gọi `NormalizeAnswer` và lưu kết quả vào `normalizedInput`.
        string normalizedInput = NormalizeAnswer(answeredText);
        // 17. Gọi `Any` và lưu kết quả vào `isCorrect`.
        bool isCorrect = acceptedAnswers.Any(answer =>
            NormalizeAnswer(answer) == normalizedInput);
        // 18. Tính giá trị và lưu vào `answeredAt` để dùng ở bước tiếp theo.
        DateTime answeredAt = _timeProvider.GetUtcNow().UtcDateTime;

        // 19. Cập nhật `question.AnsweredText` bằng giá trị mới.
        question.AnsweredText = answeredText ?? string.Empty;
        // 20. Cập nhật `question.IsCorrect` bằng giá trị mới.
        question.IsCorrect = isCorrect;
        // 21. Cập nhật `question.AnsweredAt` bằng giá trị mới.
        question.AnsweredAt = answeredAt;

        // 22. Gọi `UpdateUserProgressAsync` để thực hiện bước nghiệp vụ này.
        await UpdateUserProgressAsync(userId, cardId, isCorrect);
        // 23. Gọi `AddAsync` để thực hiện bước nghiệp vụ này.
        await _context.DictationSessionDetails.AddAsync(new DictationSessionDetail
        {
            StudySessionId = sessionId,
            FlashcardId = cardId,
            IsCorrect = isCorrect,
            AnsweredText = answeredText ?? string.Empty,
            CreatedAt = answeredAt
        });

        // 24. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // Một SaveChanges giữ progress, snapshot câu trả lời và detail trong cùng transaction.
            // 25. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // 26. Gọi `Clear` để thực hiện bước nghiệp vụ này.
            _context.ChangeTracker.Clear();
            // 27. Gọi `SingleAsync` và lưu kết quả vào `savedQuestion`.
            DictationSessionQuestion savedQuestion = await _context.DictationSessionQuestions
                .AsNoTracking()
                .SingleAsync(row =>
                    row.StudySessionId == sessionId
                    && row.FlashcardId == cardId);
            // 28. Trả kết quả từ `BuildCheckResult` cho nơi gọi.
            return BuildCheckResult(session, savedQuestion);
        }
        catch (DbUpdateException)
        {
            // 29. Gọi `Clear` để thực hiện bước nghiệp vụ này.
            _context.ChangeTracker.Clear();
            // 30. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `savedQuestion`.
            DictationSessionQuestion? savedQuestion = await _context.DictationSessionQuestions
                .AsNoTracking()
                .SingleOrDefaultAsync(row =>
                    row.StudySessionId == sessionId
                    && row.FlashcardId == cardId
                    && row.IsCorrect.HasValue);
            // 31. Kiểm tra `savedQuestion != null` để chọn nhánh xử lý phù hợp.
            if (savedQuestion != null)
            {
                // 32. Trả kết quả từ `BuildCheckResult` cho nơi gọi.
                return BuildCheckResult(session, savedQuestion);
            }

            // 33. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }

        // 34. Gọi `PublishAsync` để thực hiện bước nghiệp vụ này.
        await _studyEvents.PublishAsync(new DictationAnswerCheckedEvent(
            UserId: userId,
            OccurredAtUtc: answeredAt,
            SetId: session.FlashcardSetId,
            SessionId: sessionId,
            FlashcardId: cardId,
            IsCorrect: isCorrect));

        // 35. Trả kết quả từ `BuildCheckResult` cho nơi gọi.
        return BuildCheckResult(session, question);
    }

    private static DictationCheckResult BuildCheckResult(
        StudySession session,
        DictationSessionQuestion question)
    {
        // 1. Tính giá trị và lưu vào `isCorrect` để dùng ở bước tiếp theo.
        bool isCorrect = question.IsCorrect == true;
        // 2. Tính giá trị và lưu vào `answeredText` để dùng ở bước tiếp theo.
        string answeredText = question.AnsweredText ?? string.Empty;

        // 3. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new DictationCheckResult
        {
            IsCorrect = isCorrect,
            CorrectAnswer = question.CorrectAnswer,
            Hint = isCorrect
                ? null
                : BuildHint(question.Pronunciation, question.Definition),
            ExampleMeaning = session.DictationContentMode == DictationContentMode.ExampleSentence
                ? question.ExampleMeaning
                : null,
            WordComparison = session.DictationContentMode == DictationContentMode.ExampleSentence
                ? BuildWordComparison(answeredText, question.CorrectAnswer)
                : new List<DictationWordComparison>()
        };
    }

    // Chuẩn hóa chuỗi đáp án để so sánh
    private static string NormalizeAnswer(string? input)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(input)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(input))
        {
            // 2. Trả `string.Empty` cho nơi gọi.
            return string.Empty;
        }

        // 3. Gọi `TokenizeWords` và lưu kết quả vào `tokens`.
        List<WordToken> tokens = TokenizeWords(input);
        // 4. Khởi tạo `normalizedWords` với dữ liệu ban đầu cần thiết.
        List<string> normalizedWords = new List<string>();

        // 5. Duyệt từng `token` trong `tokens` để xử lý lần lượt.
        foreach (WordToken token in tokens)
        {
            // 6. Gọi `Add` để thực hiện bước nghiệp vụ này.
            normalizedWords.Add(token.Normalized);
        }

        // 7. Trả kết quả từ `Join` cho nơi gọi.
        return string.Join(" ", normalizedWords);
    }

    // Một từ: bản gốc (UI) + bản chuẩn hóa (so khớp)
    private sealed record WordToken(string Original, string Normalized);

    private static List<WordToken> TokenizeWords(string? input)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(input)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(input))
        {
            // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new List<WordToken>();
        }

        // 3. Gọi `Split` và lưu kết quả vào `parts`.
        string[] parts = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        // 4. Khởi tạo `tokens` với dữ liệu ban đầu cần thiết.
        List<WordToken> tokens = new List<WordToken>();

        // 5. Duyệt từng `part` trong `parts` để xử lý lần lượt.
        foreach (string part in parts)
        {
            // 6. Gọi `NormalizeWord` và lưu kết quả vào `normalized`.
            string normalized = NormalizeWord(part);
            // 7. Kiểm tra `normalized.Length > 0` để chọn nhánh xử lý phù hợp.
            if (normalized.Length > 0)
            {
                // 8. Gọi `Add` để thực hiện bước nghiệp vụ này.
                tokens.Add(new WordToken(part, normalized));
            }
        }

        // 9. Trả `tokens` cho nơi gọi.
        return tokens;
    }

    private static string NormalizeWord(string word)
    {
        // 1. Trả kết quả từ `Replace` cho nơi gọi.
        return word
            .ToLowerInvariant()
            .Replace(",", "")
            .Replace(".", "")
            .Replace("!", "")
            .Replace("?", "")
            .Replace(";", "");
    }

    private static List<DictationWordComparison> BuildWordComparison(
        string? answeredText,
        string correctAnswer)
    {
        // 1. Gọi `TokenizeWords` và lưu kết quả vào `answeredTokens`.
        List<WordToken> answeredTokens = TokenizeWords(answeredText);
        // 2. Gọi `TokenizeWords` và lưu kết quả vào `correctTokens`.
        List<WordToken> correctTokens = TokenizeWords(correctAnswer);

        // Ma trận khoảng cách chỉnh sửa (Levenshtein theo từ).
        // O(n*m) phù hợp với câu ngắn; chỉ cân nhắc lại nếu nghe chép cả đoạn dài.
        // 3. Tính giá trị và lưu vào `answeredCount` để dùng ở bước tiếp theo.
        int answeredCount = answeredTokens.Count;
        // 4. Tính giá trị và lưu vào `correctCount` để dùng ở bước tiếp theo.
        int correctCount = correctTokens.Count;
        // 5. Khởi tạo `distance` với dữ liệu ban đầu cần thiết.
        int[,] distance = new int[answeredCount + 1, correctCount + 1];

        // 6. Lặp qua phạm vi dữ liệu cần xử lý.
        for (int i = 0; i <= answeredCount; i++)
        {
            // 7. Cập nhật `distance[i, 0]` bằng giá trị mới.
            distance[i, 0] = i;
        }

        // 8. Lặp qua phạm vi dữ liệu cần xử lý.
        for (int j = 0; j <= correctCount; j++)
        {
            // 9. Cập nhật `distance[0, j]` bằng giá trị mới.
            distance[0, j] = j;
        }

        // 10. Lặp qua phạm vi dữ liệu cần xử lý.
        for (int i = 1; i <= answeredCount; i++)
        {
            // 11. Lặp qua phạm vi dữ liệu cần xử lý.
            for (int j = 1; j <= correctCount; j++)
            {
                // 12. Tính giá trị và lưu vào `substitutionCost` để dùng ở bước tiếp theo.
                int substitutionCost = 1;
                // 13. Kiểm tra `answeredTokens[i - 1].Normalized == correctTokens[j - 1].Normalized` để chọn nhánh xử lý phù hợp.
                if (answeredTokens[i - 1].Normalized == correctTokens[j - 1].Normalized)
                {
                    // 14. Cập nhật `substitutionCost` bằng giá trị mới.
                    substitutionCost = 0;
                }

                // 15. Tính giá trị và lưu vào `substitution` để dùng ở bước tiếp theo.
                int substitution = distance[i - 1, j - 1] + substitutionCost;
                // 16. Tính giá trị và lưu vào `deletion` để dùng ở bước tiếp theo.
                int deletion = distance[i - 1, j] + 1;
                // 17. Tính giá trị và lưu vào `insertion` để dùng ở bước tiếp theo.
                int insertion = distance[i, j - 1] + 1;

                // 18. Cập nhật `distance[i, j]` bằng giá trị mới.
                distance[i, j] = Math.Min(substitution, Math.Min(deletion, insertion));
            }
        }

        // Truy vết ngược từ góc dưới-phải để dựng danh sách so sánh từ
        // 19. Khởi tạo `result` với dữ liệu ban đầu cần thiết.
        List<DictationWordComparison> result = new List<DictationWordComparison>();
        // 20. Tính giá trị và lưu vào `answeredIndex` để dùng ở bước tiếp theo.
        int answeredIndex = answeredCount;
        // 21. Tính giá trị và lưu vào `correctIndex` để dùng ở bước tiếp theo.
        int correctIndex = correctCount;

        // 22. Tiếp tục lặp khi `answeredIndex > 0 || correctIndex > 0` còn đúng.
        while (answeredIndex > 0 || correctIndex > 0)
        {
            // 23. Kiểm tra `answeredIndex > 0 && correctIndex > 0` để chọn nhánh xử lý phù hợp.
            if (answeredIndex > 0 && correctIndex > 0)
            {
                // 24. Tính giá trị và lưu vào `wordsMatch` để dùng ở bước tiếp theo.
                bool wordsMatch =
                    answeredTokens[answeredIndex - 1].Normalized
                    == correctTokens[correctIndex - 1].Normalized;

                // 25. Tính giá trị và lưu vào `substitutionCost` để dùng ở bước tiếp theo.
                int substitutionCost = wordsMatch ? 0 : 1;
                // 26. Tính giá trị và lưu vào `substitutionDistance` để dùng ở bước tiếp theo.
                int substitutionDistance =
                    distance[answeredIndex - 1, correctIndex - 1] + substitutionCost;

                // 27. Kiểm tra `distance[answeredIndex, correctIndex] == substitutionDistance` để chọn nhánh xử lý phù hợp.
                if (distance[answeredIndex, correctIndex] == substitutionDistance)
                {
                    // 28. Tính giá trị và lưu vào `status` để dùng ở bước tiếp theo.
                    DictationWordStatus status = wordsMatch
                        ? DictationWordStatus.Correct
                        : DictationWordStatus.Incorrect;

                    // 29. Gọi `Add` để thực hiện bước nghiệp vụ này.
                    result.Add(new DictationWordComparison
                    {
                        Status = status,
                        AnsweredWord = answeredTokens[answeredIndex - 1].Original,
                        CorrectWord = correctTokens[correctIndex - 1].Original
                    });

                    // 30. Cập nhật bộ đếm hoặc trạng thái `answeredIndex`.
                    answeredIndex--;
                    // 31. Cập nhật bộ đếm hoặc trạng thái `correctIndex`.
                    correctIndex--;
                    // 32. Bỏ qua phần còn lại và chuyển sang lần lặp tiếp theo.
                    continue;
                }
            }

            // 33. Tính giá trị và lưu vào `isExtraWord` để dùng ở bước tiếp theo.
            bool isExtraWord =
                answeredIndex > 0
                && distance[answeredIndex, correctIndex]
                    == distance[answeredIndex - 1, correctIndex] + 1;

            // 34. Kiểm tra `isExtraWord` để chọn nhánh xử lý phù hợp.
            if (isExtraWord)
            {
                // 35. Gọi `Add` để thực hiện bước nghiệp vụ này.
                result.Add(new DictationWordComparison
                {
                    Status = DictationWordStatus.Extra,
                    AnsweredWord = answeredTokens[answeredIndex - 1].Original
                });
                // 36. Cập nhật bộ đếm hoặc trạng thái `answeredIndex`.
                answeredIndex--;
            }
            else
            {
                // 37. Gọi `Add` để thực hiện bước nghiệp vụ này.
                result.Add(new DictationWordComparison
                {
                    Status = DictationWordStatus.Missing,
                    CorrectWord = correctTokens[correctIndex - 1].Original
                });
                // 38. Cập nhật bộ đếm hoặc trạng thái `correctIndex`.
                correctIndex--;
            }
        }

        // 39. Gọi `Reverse` để thực hiện bước nghiệp vụ này.
        result.Reverse();
        // 40. Trả `result` cho nơi gọi.
        return result;
    }

    // Tạo gợi ý khi trả lời sai: IPA và nghĩa
    private static string? BuildHint(string? pronunciation, string? definition)
    {
        // 1. Khởi tạo `parts` với dữ liệu ban đầu cần thiết.
        List<string> parts = new List<string>();

        // 2. Kiểm tra `!string.IsNullOrWhiteSpace(pronunciation)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(pronunciation))
        {
            // 3. Gọi `Add` để thực hiện bước nghiệp vụ này.
            parts.Add($"IPA: {pronunciation}");
        }

        // 4. Kiểm tra `!string.IsNullOrWhiteSpace(definition)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(definition))
        {
            // 5. Gọi `Add` để thực hiện bước nghiệp vụ này.
            parts.Add($"Nghĩa: {definition}");
        }

        // 6. Kiểm tra `parts.Count == 0` để chọn nhánh xử lý phù hợp.
        if (parts.Count == 0)
        {
            // 7. Trả `null` cho nơi gọi.
            return null;
        }

        // 8. Trả kết quả từ `Join` cho nơi gọi.
        return string.Join(" | ", parts);
    }

    // Cập nhật UserProgress sau mỗi câu trả lời
    private async Task UpdateUserProgressAsync(string userId, int flashcardId, bool isCorrect)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `progress`.
        UserProgress? progress = await _context.UserProgresses
            .FirstOrDefaultAsync(row => row.UserId == userId && row.FlashcardId == flashcardId);

        // 2. Kiểm tra `progress == null` để chọn nhánh xử lý phù hợp.
        if (progress == null)
        {
            // 3. Cập nhật `progress` bằng giá trị mới.
            progress = new UserProgress
            {
                UserId = userId,
                FlashcardId = flashcardId
            };
            // 4. Gọi `AddAsync` để thực hiện bước nghiệp vụ này.
            await _context.UserProgresses.AddAsync(progress);
        }

        // 5. Cập nhật `progress.IsLearned` bằng giá trị mới.
        progress.IsLearned = isCorrect;

        // 6. Kiểm tra `isCorrect` để chọn nhánh xử lý phù hợp.
        if (isCorrect)
        {
            // 7. Cập nhật `progress.Status` bằng giá trị mới.
            progress.Status = UserProgressStatus.Mastered;
            // 8. Cập nhật bộ đếm hoặc trạng thái `progress.CorrectCount`.
            progress.CorrectCount++;
        }
        else
        {
            // 9. Cập nhật `progress.Status` bằng giá trị mới.
            progress.Status = UserProgressStatus.Learning;
            // 10. Cập nhật bộ đếm hoặc trạng thái `progress.WrongCount`.
            progress.WrongCount++;
        }

        // 11. Cập nhật `progress.LastReviewed` bằng giá trị mới.
        progress.LastReviewed = _timeProvider.GetUtcNow().UtcDateTime;
    }

    // Đóng phiên học và lưu điểm
    public async Task<StudySession> CompleteSessionAsync(int sessionId, int setId, string userId)
    {
        // 1. Gọi `GetOwnedDictationSessionAsync` và lưu kết quả vào `session`.
        StudySession session = await GetOwnedDictationSessionAsync(
            sessionId,
            setId,
            userId,
            requireCompleted: false);

        // 2. Kiểm tra `session.CompletedAt.HasValue` để chọn nhánh xử lý phù hợp.
        if (session.CompletedAt.HasValue)
        {
            // 3. Trả `session` cho nơi gọi.
            return session;
        }

        // 4. Gọi `ToListAsync` và lưu kết quả vào `questions`.
        List<DictationSessionQuestion> questions = await _context.DictationSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sessionId)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();

        // 5. Khai báo `denominator` để lưu dữ liệu dùng ở các bước sau.
        int denominator;
        // 6. Khai báo `correctCount` để lưu dữ liệu dùng ở các bước sau.
        int correctCount;
        // 7. Kiểm tra `questions.Count > 0` để chọn nhánh xử lý phù hợp.
        if (questions.Count > 0)
        {
            // 8. Kiểm tra `questions.Any(question => !question.IsCorrect.HasValue)` để chọn nhánh xử lý phù hợp.
            if (questions.Any(question => !question.IsCorrect.HasValue))
            {
                // 9. Dừng xử lý và phát sinh lỗi `new InvalidOperationException( "Bạn cần hoàn thành tất cả câu hỏi t...`.
                throw new InvalidOperationException(
                    "Bạn cần hoàn thành tất cả câu hỏi trước khi kết thúc phiên.");
            }

            // 10. Cập nhật `denominator` bằng giá trị mới.
            denominator = questions.Count;
            // 11. Cập nhật `correctCount` bằng giá trị mới.
            correctCount = questions.Count(question => question.IsCorrect == true);
        }
        else
        {
            // Tương thích các session cũ được tạo trước khi có snapshot câu hỏi.
            // 12. Gọi `ToListAsync` và lưu kết quả vào `details`.
            List<DictationSessionDetail> details = await _context.DictationSessionDetails
                .AsNoTracking()
                .Where(detail => detail.StudySessionId == sessionId)
                .OrderBy(detail => detail.Id)
                .ToListAsync();
            // 13. Gọi `Count` và lưu kết quả vào `answeredCount`.
            int answeredCount = details
                .Select(detail => detail.FlashcardId)
                .Distinct()
                .Count();
            // 14. Cập nhật `denominator` bằng giá trị mới.
            denominator = session.PlannedItemCount > 0
                ? session.PlannedItemCount
                : answeredCount;
            // 15. Cập nhật `correctCount` bằng giá trị mới.
            correctCount = details
                .GroupBy(detail => detail.FlashcardId)
                .Count(group => group.First().IsCorrect);
        }

        // 16. Tính giá trị và lưu vào `score` để dùng ở bước tiếp theo.
        int score = denominator == 0
            ? 0
            : (int)Math.Round(correctCount * 100d / denominator, MidpointRounding.AwayFromZero);
        // 17. Cập nhật `session.Score` bằng giá trị mới.
        session.Score = Math.Clamp(score, 0, 100);
        // 18. Tính giá trị và lưu vào `completedAt` để dùng ở bước tiếp theo.
        DateTime completedAt = _timeProvider.GetUtcNow().UtcDateTime;
        // 19. Cập nhật `session.DurationSeconds` bằng giá trị mới.
        session.DurationSeconds = StudySessionTiming.CalculateDurationSeconds(
            session.StartedAt,
            completedAt);
        // 20. Cập nhật `session.CompletedAt` bằng giá trị mới.
        session.CompletedAt = completedAt;

        // 21. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 22. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // 23. Gọi `Clear` để thực hiện bước nghiệp vụ này.
            _context.ChangeTracker.Clear();
            // 24. Trả kết quả từ `GetOwnedDictationSessionAsync` cho nơi gọi.
            return await GetOwnedDictationSessionAsync(
                sessionId,
                setId,
                userId,
                requireCompleted: true);
        }

        // Báo buổi nghe chép đã xong; có thể mở huy hiệu Dictation / điểm 100
        // 25. Gọi `PublishAsync` để thực hiện bước nghiệp vụ này.
        await _studyEvents.PublishAsync(new StudySessionCompletedEvent(
            UserId: session.UserId,
            OccurredAtUtc: completedAt,
            SetId: session.FlashcardSetId,
            SessionId: session.Id,
            Mode: StudyMode.Dictation,
            Score: session.Score));

        // 26. Trả `session` cho nơi gọi.
        return session;
    }

    // Lấy dữ liệu tổng kết phiên học
    public async Task<DictationResult> GetSessionResultAsync(
        int sessionId,
        int setId,
        string userId)
    {
        // 1. Gọi `GetOwnedDictationSessionAsync` và lưu kết quả vào `session`.
        StudySession session = await GetOwnedDictationSessionAsync(
            sessionId,
            setId,
            userId,
            requireCompleted: true);

        // 2. Gọi `ToListAsync` và lưu kết quả vào `questions`.
        List<DictationSessionQuestion> questions = await _context.DictationSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sessionId)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();

        // 3. Khởi tạo `wrongCards` với dữ liệu ban đầu cần thiết.
        List<DictationResultCard> wrongCards = new List<DictationResultCard>();
        // 4. Khai báo `totalCards` để lưu dữ liệu dùng ở các bước sau.
        int totalCards;
        // 5. Khai báo `correctCount` để lưu dữ liệu dùng ở các bước sau.
        int correctCount;
        // 6. Kiểm tra `questions.Count > 0` để chọn nhánh xử lý phù hợp.
        if (questions.Count > 0)
        {
            // 7. Cập nhật `totalCards` bằng giá trị mới.
            totalCards = questions.Count;
            // 8. Cập nhật `correctCount` bằng giá trị mới.
            correctCount = questions.Count(question => question.IsCorrect == true);
            // 9. Duyệt từng `question` trong `questions.Where(row => row.IsCorrect == false)` để xử lý lần lượt.
            foreach (DictationSessionQuestion question in questions.Where(row => row.IsCorrect == false))
            {
                // 10. Gọi `Add` để thực hiện bước nghiệp vụ này.
                wrongCards.Add(new DictationResultCard
                {
                    Id = question.FlashcardId,
                    Term = question.Term,
                    Definition = question.Definition,
                    Pronunciation = question.Pronunciation,
                    ExampleSentence = question.ExampleSentence,
                    ExampleMeaning = question.ExampleMeaning
                });
            }
        }
        else
        {
            // 11. Gọi `ToListAsync` và lưu kết quả vào `details`.
            List<DictationSessionDetail> details = await _context.DictationSessionDetails
                .AsNoTracking()
                .Where(detail => detail.StudySessionId == sessionId)
                .Include(detail => detail.Flashcard)
                .OrderBy(detail => detail.Id)
                .ToListAsync();
            // 12. Gọi `ToList` và lưu kết quả vào `distinctDetails`.
            List<DictationSessionDetail> distinctDetails = details
                .GroupBy(detail => detail.FlashcardId)
                .Select(group => group.First())
                .ToList();

            // 13. Cập nhật `totalCards` bằng giá trị mới.
            totalCards = distinctDetails.Count;
            // 14. Cập nhật `correctCount` bằng giá trị mới.
            correctCount = distinctDetails.Count(detail => detail.IsCorrect);
            // 15. Duyệt từng `detail` trong `distinctDetails.Where(row => !row.IsCorrect)` để xử lý lần lượt.
            foreach (DictationSessionDetail detail in distinctDetails.Where(row => !row.IsCorrect))
            {
                // 16. Kiểm tra `detail.Flashcard != null` để chọn nhánh xử lý phù hợp.
                if (detail.Flashcard != null)
                {
                    // 17. Gọi `Add` để thực hiện bước nghiệp vụ này.
                    wrongCards.Add(new DictationResultCard
                    {
                        Id = detail.Flashcard.Id,
                        Term = detail.Flashcard.FrontText,
                        Definition = detail.Flashcard.BackText,
                        Pronunciation = detail.Flashcard.Pronunciation,
                        ExampleSentence = detail.Flashcard.ExampleSentence,
                        ExampleMeaning = detail.Flashcard.ExampleMeaning
                    });
                }
            }
        }

        // 18. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new DictationResult
        {
            SessionId = sessionId,
            ContentMode = session.DictationContentMode,
            TotalCards = totalCards,
            CorrectCount = correctCount,
            Score = session.Score ?? 0,
            WrongCards = wrongCards
        };
    }

    private async Task<StudySession> GetOwnedDictationSessionAsync(
        int sessionId,
        int setId,
        string userId,
        bool requireCompleted)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `session`.
        StudySession? session = await _context.StudySessions
            .FirstOrDefaultAsync(row => row.Id == sessionId);
        // 2. Kiểm tra `session == null` để chọn nhánh xử lý phù hợp.
        if (session == null)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Phiên học không tồn tại.")`.
            throw new KeyNotFoundException("Phiên học không tồn tại.");
        }

        // 4. Kiểm tra `session.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (session.UserId != userId)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền truy cập phiên học ...`.
            throw new UnauthorizedAccessException("Không có quyền truy cập phiên học này.");
        }

        // 6. Kiểm tra `session.FlashcardSetId != setId || session.Mode != StudyMode.Dictation` để chọn nhánh xử lý phù hợp.
        if (session.FlashcardSetId != setId || session.Mode != StudyMode.Dictation)
        {
            // 7. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Phiên nghe chép không thuộc bộ thẻ...`.
            throw new UnauthorizedAccessException("Phiên nghe chép không thuộc bộ thẻ này.");
        }

        // 8. Kiểm tra `requireCompleted && !session.CompletedAt.HasValue` để chọn nhánh xử lý phù hợp.
        if (requireCompleted && !session.CompletedAt.HasValue)
        {
            // 9. Dừng xử lý và phát sinh lỗi `new InvalidOperationException("Phiên nghe chép chưa hoàn thành.")`.
            throw new InvalidOperationException("Phiên nghe chép chưa hoàn thành.");
        }

        // 10. Trả `session` cho nơi gọi.
        return session;
    }
}
