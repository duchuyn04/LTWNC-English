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
        _context = context;
        _strategyResolver = strategyResolver;
        _studyEvents = studyEvents;
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
        IStudyModeStrategy strategy = _strategyResolver.Resolve(StudyMode.Dictation);
        List<Flashcard> cards = await strategy.GetCardsAsync(setId, settings, userId);

        // Xáo trộn nếu user bật DictationShuffle
        if (settings.DictationShuffle)
        {
            cards = Shuffle(cards);
        }

        return cards;
    }

    // Kiểm tra bộ thẻ có bất kỳ thẻ nào có câu ví dụ không (bỏ qua bộ lọc)
    public async Task<bool> AnyCardHasExampleSentenceAsync(int setId)
    {
        bool hasExample = await _context.Flashcards.AnyAsync(flashcard =>
            flashcard.FlashcardSetId == setId
            && flashcard.ExampleSentence.Trim() != "");

        return hasExample;
    }

    // Xáo trộn danh sách bằng thuật toán Fisher-Yates
    private static List<T> Shuffle<T>(List<T> list)
    {
        Random random = new Random();
        List<T> result = new List<T>(list);

        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);

            // Đổi chỗ hai phần tử
            T temp = result[i];
            result[i] = result[j];
            result[j] = temp;
        }

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
        int itemCount = cards?.Count ?? Math.Max(0, plannedItemCount);
        StudySession session = new StudySession
        {
            UserId = userId,
            FlashcardSetId = setId,
            Mode = StudyMode.Dictation,
            DictationContentMode = contentMode,
            PlannedItemCount = itemCount,
            StartedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        await _context.StudySessions.AddAsync(session);

        if (cards != null)
        {
            for (int index = 0; index < cards.Count; index++)
            {
                Flashcard card = cards[index];
                string promptText = contentMode == DictationContentMode.ExampleSentence
                    ? card.ExampleSentence
                    : card.FrontText;

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

        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<DictationRetryPlan> GetRetryPlanAsync(
        int sourceSessionId,
        int setId,
        string userId)
    {
        StudySession session = await GetOwnedDictationSessionAsync(
            sourceSessionId,
            setId,
            userId,
            requireCompleted: true);

        List<int> cardIds = await _context.DictationSessionQuestions
            .AsNoTracking()
            .Where(question =>
                question.StudySessionId == sourceSessionId
                && question.IsCorrect == false)
            .OrderBy(question => question.OrderIndex)
            .Select(question => question.FlashcardId)
            .ToListAsync();

        if (cardIds.Count == 0)
        {
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

        Dictionary<int, Flashcard> cardsById = await _context.Flashcards
            .Where(card =>
                card.FlashcardSetId == setId
                && cardIds.Contains(card.Id))
            .ToDictionaryAsync(card => card.Id);

        List<Flashcard> cards = cardIds
            .Where(cardsById.ContainsKey)
            .Select(cardId => cardsById[cardId])
            .ToList();

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
        bool ownsSet = await _context.FlashcardSets
            .AsNoTracking()
            .AnyAsync(set => set.Id == setId && set.UserId == userId);
        if (!ownsSet)
        {
            throw new UnauthorizedAccessException("Không có quyền xem lịch sử bộ thẻ này.");
        }

        int safeLimit = Math.Clamp(limit, 1, 500);
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

        if (snapshotItems.Count >= safeLimit)
        {
            return snapshotItems;
        }

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
        StudySession session = await GetOwnedDictationSessionAsync(
            sessionId,
            setId,
            userId,
            requireCompleted: false);
        if (session.CompletedAt.HasValue)
        {
            throw new InvalidOperationException("Phiên nghe chép đã hoàn thành.");
        }

        DictationSessionQuestion? question = await _context.DictationSessionQuestions
            .SingleOrDefaultAsync(row =>
                row.StudySessionId == sessionId
                && row.FlashcardId == cardId);
        if (question == null)
        {
            throw new KeyNotFoundException("Thẻ không thuộc phiên nghe chép này.");
        }

        if (question.IsCorrect.HasValue)
        {
            return BuildCheckResult(session, question);
        }

        List<string> acceptedAnswers = new List<string> { question.CorrectAnswer };
        bool canAcceptSynonyms =
            session.DictationContentMode == DictationContentMode.Vocabulary
            && acceptSynonyms
            && !string.IsNullOrWhiteSpace(question.Synonyms);

        if (canAcceptSynonyms)
        {
            foreach (string part in question.Synonyms!.Split(
                new[] { ',', ';' },
                StringSplitOptions.RemoveEmptyEntries))
            {
                string synonym = part.Trim();
                if (!string.IsNullOrWhiteSpace(synonym))
                {
                    acceptedAnswers.Add(synonym);
                }
            }
        }

        string normalizedInput = NormalizeAnswer(answeredText);
        bool isCorrect = acceptedAnswers.Any(answer =>
            NormalizeAnswer(answer) == normalizedInput);
        DateTime answeredAt = _timeProvider.GetUtcNow().UtcDateTime;

        question.AnsweredText = answeredText ?? string.Empty;
        question.IsCorrect = isCorrect;
        question.AnsweredAt = answeredAt;

        await UpdateUserProgressAsync(userId, cardId, isCorrect);
        await _context.DictationSessionDetails.AddAsync(new DictationSessionDetail
        {
            StudySessionId = sessionId,
            FlashcardId = cardId,
            IsCorrect = isCorrect,
            AnsweredText = answeredText ?? string.Empty,
            CreatedAt = answeredAt
        });

        try
        {
            // Một SaveChanges giữ progress, snapshot câu trả lời và detail trong cùng transaction.
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            DictationSessionQuestion savedQuestion = await _context.DictationSessionQuestions
                .AsNoTracking()
                .SingleAsync(row =>
                    row.StudySessionId == sessionId
                    && row.FlashcardId == cardId);
            return BuildCheckResult(session, savedQuestion);
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();
            DictationSessionQuestion? savedQuestion = await _context.DictationSessionQuestions
                .AsNoTracking()
                .SingleOrDefaultAsync(row =>
                    row.StudySessionId == sessionId
                    && row.FlashcardId == cardId
                    && row.IsCorrect.HasValue);
            if (savedQuestion != null)
            {
                return BuildCheckResult(session, savedQuestion);
            }

            throw;
        }

        await _studyEvents.PublishAsync(new DictationAnswerCheckedEvent(
            UserId: userId,
            OccurredAtUtc: answeredAt,
            SetId: session.FlashcardSetId,
            SessionId: sessionId,
            FlashcardId: cardId,
            IsCorrect: isCorrect));

        return BuildCheckResult(session, question);
    }

    private static DictationCheckResult BuildCheckResult(
        StudySession session,
        DictationSessionQuestion question)
    {
        bool isCorrect = question.IsCorrect == true;
        string answeredText = question.AnsweredText ?? string.Empty;

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
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        List<WordToken> tokens = TokenizeWords(input);
        List<string> normalizedWords = new List<string>();

        foreach (WordToken token in tokens)
        {
            normalizedWords.Add(token.Normalized);
        }

        return string.Join(" ", normalizedWords);
    }

    // Một từ: bản gốc (UI) + bản chuẩn hóa (so khớp)
    private sealed record WordToken(string Original, string Normalized);

    private static List<WordToken> TokenizeWords(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new List<WordToken>();
        }

        string[] parts = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        List<WordToken> tokens = new List<WordToken>();

        foreach (string part in parts)
        {
            string normalized = NormalizeWord(part);
            if (normalized.Length > 0)
            {
                tokens.Add(new WordToken(part, normalized));
            }
        }

        return tokens;
    }

    private static string NormalizeWord(string word)
    {
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
        List<WordToken> answeredTokens = TokenizeWords(answeredText);
        List<WordToken> correctTokens = TokenizeWords(correctAnswer);

        // Ma trận khoảng cách chỉnh sửa (Levenshtein theo từ).
        // O(n*m) phù hợp với câu ngắn; chỉ cân nhắc lại nếu nghe chép cả đoạn dài.
        int answeredCount = answeredTokens.Count;
        int correctCount = correctTokens.Count;
        int[,] distance = new int[answeredCount + 1, correctCount + 1];

        for (int i = 0; i <= answeredCount; i++)
        {
            distance[i, 0] = i;
        }

        for (int j = 0; j <= correctCount; j++)
        {
            distance[0, j] = j;
        }

        for (int i = 1; i <= answeredCount; i++)
        {
            for (int j = 1; j <= correctCount; j++)
            {
                int substitutionCost = 1;
                if (answeredTokens[i - 1].Normalized == correctTokens[j - 1].Normalized)
                {
                    substitutionCost = 0;
                }

                int substitution = distance[i - 1, j - 1] + substitutionCost;
                int deletion = distance[i - 1, j] + 1;
                int insertion = distance[i, j - 1] + 1;

                distance[i, j] = Math.Min(substitution, Math.Min(deletion, insertion));
            }
        }

        // Truy vết ngược từ góc dưới-phải để dựng danh sách so sánh từ
        List<DictationWordComparison> result = new List<DictationWordComparison>();
        int answeredIndex = answeredCount;
        int correctIndex = correctCount;

        while (answeredIndex > 0 || correctIndex > 0)
        {
            if (answeredIndex > 0 && correctIndex > 0)
            {
                bool wordsMatch =
                    answeredTokens[answeredIndex - 1].Normalized
                    == correctTokens[correctIndex - 1].Normalized;

                int substitutionCost = wordsMatch ? 0 : 1;
                int substitutionDistance =
                    distance[answeredIndex - 1, correctIndex - 1] + substitutionCost;

                if (distance[answeredIndex, correctIndex] == substitutionDistance)
                {
                    DictationWordStatus status = wordsMatch
                        ? DictationWordStatus.Correct
                        : DictationWordStatus.Incorrect;

                    result.Add(new DictationWordComparison
                    {
                        Status = status,
                        AnsweredWord = answeredTokens[answeredIndex - 1].Original,
                        CorrectWord = correctTokens[correctIndex - 1].Original
                    });

                    answeredIndex--;
                    correctIndex--;
                    continue;
                }
            }

            bool isExtraWord =
                answeredIndex > 0
                && distance[answeredIndex, correctIndex]
                    == distance[answeredIndex - 1, correctIndex] + 1;

            if (isExtraWord)
            {
                result.Add(new DictationWordComparison
                {
                    Status = DictationWordStatus.Extra,
                    AnsweredWord = answeredTokens[answeredIndex - 1].Original
                });
                answeredIndex--;
            }
            else
            {
                result.Add(new DictationWordComparison
                {
                    Status = DictationWordStatus.Missing,
                    CorrectWord = correctTokens[correctIndex - 1].Original
                });
                correctIndex--;
            }
        }

        result.Reverse();
        return result;
    }

    // Tạo gợi ý khi trả lời sai: IPA và nghĩa
    private static string? BuildHint(string? pronunciation, string? definition)
    {
        List<string> parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(pronunciation))
        {
            parts.Add($"IPA: {pronunciation}");
        }

        if (!string.IsNullOrWhiteSpace(definition))
        {
            parts.Add($"Nghĩa: {definition}");
        }

        if (parts.Count == 0)
        {
            return null;
        }

        return string.Join(" | ", parts);
    }

    // Cập nhật UserProgress sau mỗi câu trả lời
    private async Task UpdateUserProgressAsync(string userId, int flashcardId, bool isCorrect)
    {
        UserProgress? progress = await _context.UserProgresses
            .FirstOrDefaultAsync(row => row.UserId == userId && row.FlashcardId == flashcardId);

        if (progress == null)
        {
            progress = new UserProgress
            {
                UserId = userId,
                FlashcardId = flashcardId
            };
            await _context.UserProgresses.AddAsync(progress);
        }

        progress.IsLearned = isCorrect;

        if (isCorrect)
        {
            progress.Status = UserProgressStatus.Mastered;
            progress.CorrectCount++;
        }
        else
        {
            progress.Status = UserProgressStatus.Learning;
            progress.WrongCount++;
        }

        progress.LastReviewed = _timeProvider.GetUtcNow().UtcDateTime;
    }

    // Đóng phiên học và lưu điểm
    public async Task<StudySession> CompleteSessionAsync(int sessionId, int setId, string userId)
    {
        StudySession session = await GetOwnedDictationSessionAsync(
            sessionId,
            setId,
            userId,
            requireCompleted: false);

        if (session.CompletedAt.HasValue)
        {
            return session;
        }

        List<DictationSessionQuestion> questions = await _context.DictationSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sessionId)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();

        int denominator;
        int correctCount;
        if (questions.Count > 0)
        {
            if (questions.Any(question => !question.IsCorrect.HasValue))
            {
                throw new InvalidOperationException(
                    "Bạn cần hoàn thành tất cả câu hỏi trước khi kết thúc phiên.");
            }

            denominator = questions.Count;
            correctCount = questions.Count(question => question.IsCorrect == true);
        }
        else
        {
            // Tương thích các session cũ được tạo trước khi có snapshot câu hỏi.
            List<DictationSessionDetail> details = await _context.DictationSessionDetails
                .AsNoTracking()
                .Where(detail => detail.StudySessionId == sessionId)
                .OrderBy(detail => detail.Id)
                .ToListAsync();
            int answeredCount = details
                .Select(detail => detail.FlashcardId)
                .Distinct()
                .Count();
            denominator = session.PlannedItemCount > 0
                ? session.PlannedItemCount
                : answeredCount;
            correctCount = details
                .GroupBy(detail => detail.FlashcardId)
                .Count(group => group.First().IsCorrect);
        }

        int score = denominator == 0
            ? 0
            : (int)Math.Round(correctCount * 100d / denominator, MidpointRounding.AwayFromZero);
        session.Score = Math.Clamp(score, 0, 100);
        DateTime completedAt = _timeProvider.GetUtcNow().UtcDateTime;
        session.DurationSeconds = StudySessionTiming.CalculateDurationSeconds(
            session.StartedAt,
            completedAt);
        session.CompletedAt = completedAt;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            return await GetOwnedDictationSessionAsync(
                sessionId,
                setId,
                userId,
                requireCompleted: true);
        }

        // Báo buổi nghe chép đã xong; có thể mở huy hiệu Dictation / điểm 100
        await _studyEvents.PublishAsync(new StudySessionCompletedEvent(
            UserId: session.UserId,
            OccurredAtUtc: completedAt,
            SetId: session.FlashcardSetId,
            SessionId: session.Id,
            Mode: StudyMode.Dictation,
            Score: session.Score));

        return session;
    }

    // Lấy dữ liệu tổng kết phiên học
    public async Task<DictationResult> GetSessionResultAsync(
        int sessionId,
        int setId,
        string userId)
    {
        StudySession session = await GetOwnedDictationSessionAsync(
            sessionId,
            setId,
            userId,
            requireCompleted: true);

        List<DictationSessionQuestion> questions = await _context.DictationSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sessionId)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();

        List<DictationResultCard> wrongCards = new List<DictationResultCard>();
        int totalCards;
        int correctCount;
        if (questions.Count > 0)
        {
            totalCards = questions.Count;
            correctCount = questions.Count(question => question.IsCorrect == true);
            foreach (DictationSessionQuestion question in questions.Where(row => row.IsCorrect == false))
            {
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
            List<DictationSessionDetail> details = await _context.DictationSessionDetails
                .AsNoTracking()
                .Where(detail => detail.StudySessionId == sessionId)
                .Include(detail => detail.Flashcard)
                .OrderBy(detail => detail.Id)
                .ToListAsync();
            List<DictationSessionDetail> distinctDetails = details
                .GroupBy(detail => detail.FlashcardId)
                .Select(group => group.First())
                .ToList();

            totalCards = distinctDetails.Count;
            correctCount = distinctDetails.Count(detail => detail.IsCorrect);
            foreach (DictationSessionDetail detail in distinctDetails.Where(row => !row.IsCorrect))
            {
                if (detail.Flashcard != null)
                {
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
        StudySession? session = await _context.StudySessions
            .FirstOrDefaultAsync(row => row.Id == sessionId);
        if (session == null)
        {
            throw new KeyNotFoundException("Phiên học không tồn tại.");
        }

        if (session.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền truy cập phiên học này.");
        }

        if (session.FlashcardSetId != setId || session.Mode != StudyMode.Dictation)
        {
            throw new UnauthorizedAccessException("Phiên nghe chép không thuộc bộ thẻ này.");
        }

        if (requireCompleted && !session.CompletedAt.HasValue)
        {
            throw new InvalidOperationException("Phiên nghe chép chưa hoàn thành.");
        }

        return session;
    }
}
