using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.StudyModes;
using ltwnc.Services.StudyEvents;

namespace ltwnc.Services;

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

    // Inject DbContext, resolver, publisher
    public DictationService(
        AppDbContext context,
        IStudyModeStrategyResolver strategyResolver,
        IStudyEventPublisher studyEvents)
    {
        _context = context;
        _strategyResolver = strategyResolver;
        _studyEvents = studyEvents;
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
        DictationContentMode contentMode = DictationContentMode.Vocabulary)
    {
        StudySession session = new StudySession
        {
            UserId = userId,
            FlashcardSetId = setId,
            Mode = StudyMode.Dictation,
            DictationContentMode = contentMode,
            CompletedAt = DateTime.UtcNow
        };

        await _context.StudySessions.AddAsync(session);
        await _context.SaveChangesAsync();
        return session;
    }

    // Kiểm tra đáp án của người dùng
    public async Task<DictationCheckResult> CheckAnswerAsync(
        int sessionId,
        int cardId,
        string answeredText,
        string userId,
        bool acceptSynonyms)
    {
        StudySession? session = await _context.StudySessions.FindAsync(sessionId);
        if (session == null)
        {
            throw new KeyNotFoundException("Phiên học không tồn tại.");
        }

        if (session.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền truy cập phiên học này.");
        }

        Flashcard? card = await _context.Flashcards.FindAsync(cardId);
        if (card == null)
        {
            throw new KeyNotFoundException("Thẻ không tồn tại.");
        }

        // Đảm bảo thẻ thuộc về bộ thẻ của phiên học
        if (card.FlashcardSetId != session.FlashcardSetId)
        {
            throw new KeyNotFoundException("Thẻ không thuộc bộ thẻ này.");
        }

        // Đáp án đúng: nội dung ghi chép là câu ví dụ thì nhập lại câu,
        // còn từ vựng thì luôn nhập thuật ngữ tiếng Anh
        string correctAnswer;
        if (session.DictationContentMode == DictationContentMode.ExampleSentence)
        {
            correctAnswer = card.ExampleSentence;
        }
        else
        {
            correctAnswer = card.FrontText;
        }

        // Tập hợp các đáp án được chấp nhận
        List<string> acceptedAnswers = new List<string> { correctAnswer };

        // Nếu chấp nhận từ đồng nghĩa trong chế độ từ vựng
        bool canAcceptSynonyms =
            session.DictationContentMode == DictationContentMode.Vocabulary
            && acceptSynonyms
            && !string.IsNullOrWhiteSpace(card.Synonyms);

        if (canAcceptSynonyms)
        {
            string[] synonymParts = card.Synonyms!.Split(
                new[] { ',', ';' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in synonymParts)
            {
                string trimmedSynonym = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedSynonym))
                {
                    acceptedAnswers.Add(trimmedSynonym);
                }
            }
        }

        // Chuẩn hóa đáp án người dùng
        string normalizedInput = NormalizeAnswer(answeredText);

        // Kiểm tra khớp với bất kỳ đáp án được chấp nhận nào
        bool isCorrect = false;
        foreach (string accepted in acceptedAnswers)
        {
            if (NormalizeAnswer(accepted) == normalizedInput)
            {
                isCorrect = true;
                break;
            }
        }

        // Cập nhật tiến trình học của người dùng
        await UpdateUserProgressAsync(userId, cardId, isCorrect);

        // Lưu chi tiết câu trả lời
        DictationSessionDetail detail = new DictationSessionDetail
        {
            StudySessionId = sessionId,
            FlashcardId = cardId,
            IsCorrect = isCorrect,
            AnsweredText = answeredText ?? string.Empty
        };
        await _context.DictationSessionDetails.AddAsync(detail);
        await _context.SaveChangesAsync();

        // Báo cho observer: user vừa trả lời một câu nghe chép (đúng/sai)
        await _studyEvents.PublishAsync(new DictationAnswerCheckedEvent(
            UserId: userId,
            OccurredAtUtc: DateTime.UtcNow,
            SetId: session.FlashcardSetId,
            SessionId: sessionId,
            FlashcardId: cardId,
            IsCorrect: isCorrect));

        string? hint = null;
        if (!isCorrect)
        {
            hint = BuildHint(card);
        }

        string? exampleMeaning = null;
        List<DictationWordComparison> wordComparison = new List<DictationWordComparison>();

        if (session.DictationContentMode == DictationContentMode.ExampleSentence)
        {
            exampleMeaning = card.ExampleMeaning;
            wordComparison = BuildWordComparison(answeredText, correctAnswer);
        }

        return new DictationCheckResult
        {
            IsCorrect = isCorrect,
            CorrectAnswer = correctAnswer,
            Hint = hint,
            ExampleMeaning = exampleMeaning,
            WordComparison = wordComparison
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
    private static string? BuildHint(Flashcard card)
    {
        List<string> parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(card.Pronunciation))
        {
            parts.Add($"IPA: {card.Pronunciation}");
        }

        if (!string.IsNullOrWhiteSpace(card.BackText))
        {
            parts.Add($"Nghĩa: {card.BackText}");
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

        progress.LastReviewed = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    // Đóng phiên học và lưu điểm
    public async Task<StudySession> CompleteSessionAsync(int sessionId, int score)
    {
        StudySession? session = await _context.StudySessions.FindAsync(sessionId);
        if (session == null)
        {
            throw new KeyNotFoundException("Phiên học không tồn tại.");
        }

        session.Score = score;
        session.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Báo buổi nghe chép đã xong; có thể mở huy hiệu Dictation / điểm 100
        await _studyEvents.PublishAsync(new StudySessionCompletedEvent(
            UserId: session.UserId,
            OccurredAtUtc: DateTime.UtcNow,
            SetId: session.FlashcardSetId,
            SessionId: session.Id,
            Mode: StudyMode.Dictation,
            Score: score));

        return session;
    }

    // Lấy dữ liệu tổng kết phiên học
    public async Task<DictationResult> GetSessionResultAsync(int sessionId, string userId)
    {
        StudySession? session = await _context.StudySessions
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == sessionId);

        if (session == null)
        {
            throw new KeyNotFoundException("Phiên học không tồn tại.");
        }

        if (session.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền xem phiên học này.");
        }

        List<DictationSessionDetail> details = await _context.DictationSessionDetails
            .AsNoTracking()
            .Where(detail => detail.StudySessionId == sessionId)
            .Include(detail => detail.Flashcard)
            .ToListAsync();

        int totalCards = details.Count;
        int correctCount = 0;
        List<DictationResultCard> wrongCards = new List<DictationResultCard>();

        foreach (DictationSessionDetail detail in details)
        {
            if (detail.IsCorrect)
            {
                correctCount++;
                continue;
            }

            if (detail.Flashcard == null)
            {
                continue;
            }

            Flashcard flashcard = detail.Flashcard;
            wrongCards.Add(new DictationResultCard
            {
                Id = flashcard.Id,
                Term = flashcard.FrontText,
                Definition = flashcard.BackText,
                Pronunciation = flashcard.Pronunciation,
                ExampleSentence = flashcard.ExampleSentence,
                ExampleMeaning = flashcard.ExampleMeaning
            });
        }

        int score = 0;
        if (session.Score.HasValue)
        {
            score = session.Score.Value;
        }

        return new DictationResult
        {
            SessionId = sessionId,
            ContentMode = session.DictationContentMode,
            TotalCards = totalCards,
            CorrectCount = correctCount,
            Score = score,
            WrongCards = wrongCards
        };
    }
}
