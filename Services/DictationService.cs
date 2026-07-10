using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Services;

// Kết quả trả về khi kiểm tra một đáp án
public class DictationCheckResult
{
    public bool IsCorrect { get; set; }
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? Hint { get; set; }
    public string? ExampleMeaning { get; set; }
    public List<DictationWordComparison> WordComparison { get; set; } = new();
}

public enum DictationWordStatus
{
    Correct,
    Incorrect,
    Missing,
    Extra
}

public class DictationWordComparison
{
    public DictationWordStatus Status { get; set; }
    public string? AnsweredWord { get; set; }
    public string? CorrectWord { get; set; }
}

// Kết quả tổng kết một phiên nghe chép
public class DictationResult
{
    public int SessionId { get; set; }
    public DictationContentMode ContentMode { get; set; }
    public int TotalCards { get; set; }
    public int CorrectCount { get; set; }
    public int Score { get; set; }
    public List<DictationResultCard> WrongCards { get; set; } = new();
}

// Thông tin một thẻ sai trong màn hình tổng kết
public class DictationResultCard
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Pronunciation { get; set; } = string.Empty;
    public string ExampleSentence { get; set; } = string.Empty;
    public string ExampleMeaning { get; set; } = string.Empty;
}

// Service xử lý nghiệp vụ nghe chép chính tả
public class DictationService
{
    private readonly AppDbContext _context;

    // Inject DbContext qua constructor
    public DictationService(AppDbContext context)
    {
        _context = context;
    }

    // Lấy danh sách thẻ cho bài nghe chép, áp dụng lọc và xáo trộn
    public async Task<List<Flashcard>> GetCardsForDictationAsync(int setId, string userId, UserStudySettings settings)
    {
        var query = _context.Flashcards.Where(f => f.FlashcardSetId == setId);

        if (settings.DictationContentMode == DictationContentMode.ExampleSentence)
        {
            query = query.Where(f => f.ExampleSentence.Trim() != "");
        }

        // Chỉ lấy thẻ đánh dấu sao
        if (settings.StarredOnly)
        {
            query = query.Where(f => f.IsStarred);
        }

        // Chỉ lấy thẻ chưa thuộc
        if (settings.UnlearnedOnly)
        {
            query = query.Where(f => !_context.UserProgresses.Any(p =>
                p.UserId == userId &&
                p.FlashcardId == f.Id &&
                p.IsLearned));
        }

        var cards = await query.OrderBy(f => f.OrderIndex).ToListAsync();

        // Xáo trộn nếu cài đặt bật
        if (settings.DictationShuffle)
        {
            cards = Shuffle(cards);
        }

        return cards;
    }

    // Kiểm tra bộ thẻ có bất kỳ thẻ nào có câu ví dụ không (bỏ qua bộ lọc)
    public async Task<bool> AnyCardHasExampleSentenceAsync(int setId)
    {
        return await _context.Flashcards.AnyAsync(f =>
            f.FlashcardSetId == setId &&
            f.ExampleSentence.Trim() != "");
    }

    // Xáo trộn danh sách bằng thuật toán Fisher-Yates
    private static List<T> Shuffle<T>(List<T> list)
    {
        var random = new Random();
        var result = new List<T>(list);
        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }

    // Tạo phiên học Dictation mới
    public async Task<StudySession> CreateSessionAsync(
        string userId,
        int setId,
        DictationContentMode contentMode = DictationContentMode.Vocabulary)
    {
        var session = new StudySession
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

    // Kiểm tra đáp án của ngườ dùng
    public async Task<DictationCheckResult> CheckAnswerAsync(
        int sessionId,
        int cardId,
        string answeredText,
        string userId,
        DictationAnswerMode mode,
        bool acceptSynonyms)
    {
        var session = await _context.StudySessions.FindAsync(sessionId);
        if (session == null)
            throw new KeyNotFoundException("Phiên học không tồn tại.");

        if (session.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền truy cập phiên học này.");

        var card = await _context.Flashcards.FindAsync(cardId);
        if (card == null)
            throw new KeyNotFoundException("Thẻ không tồn tại.");

        // Đảm bảo thẻ thuộc về bộ thẻ của phiên học
        if (card.FlashcardSetId != session.FlashcardSetId)
            throw new KeyNotFoundException("Thẻ không thuộc bộ thẻ này.");

        // Đáp án đúng tùy theo chế độ trả lờ
        var correctAnswer = session.DictationContentMode == DictationContentMode.ExampleSentence
            ? card.ExampleSentence
            : mode == DictationAnswerMode.Definition
                ? card.BackText
                : card.FrontText;

        // Tập hợp các đáp án được chấp nhận
        var acceptedAnswers = new List<string> { correctAnswer };

        // Nếu chấp nhận từ đồng nghĩa và đang ở chế độ thuật ngữ
        if (session.DictationContentMode == DictationContentMode.Vocabulary &&
            acceptSynonyms &&
            mode == DictationAnswerMode.Term &&
            !string.IsNullOrWhiteSpace(card.Synonyms))
        {
            var synonyms = card.Synonyms
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s));
            acceptedAnswers.AddRange(synonyms);
        }

        // Chuẩn hóa đáp án ngườ dùng
        var normalizedInput = NormalizeAnswer(answeredText);

        // Kiểm tra khớp
        var isCorrect = acceptedAnswers.Any(a => NormalizeAnswer(a) == normalizedInput);

        // Cập nhật tiến trình học của ngườ dùng
        await UpdateUserProgressAsync(userId, cardId, isCorrect);

        // Lưu chi tiết câu trả lờ
        var detail = new DictationSessionDetail
        {
            StudySessionId = sessionId,
            FlashcardId = cardId,
            IsCorrect = isCorrect,
            AnsweredText = answeredText ?? string.Empty
        };
        await _context.DictationSessionDetails.AddAsync(detail);
        await _context.SaveChangesAsync();

        return new DictationCheckResult
        {
            IsCorrect = isCorrect,
            CorrectAnswer = correctAnswer,
            Hint = isCorrect ? null : BuildHint(card),
            ExampleMeaning = session.DictationContentMode == DictationContentMode.ExampleSentence
                ? card.ExampleMeaning
                : null,
            WordComparison = session.DictationContentMode == DictationContentMode.ExampleSentence
                ? BuildWordComparison(answeredText, correctAnswer)
                : new()
        };
    }

    // Chuẩn hóa chuỗi đáp án để so sánh
    private static string NormalizeAnswer(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return string.Join(" ", TokenizeWords(input).Select(word => word.Normalized));
    }

    private sealed record WordToken(string Original, string Normalized);

    private static List<WordToken> TokenizeWords(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new();

        return input
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => new WordToken(word, NormalizeWord(word)))
            .Where(word => word.Normalized.Length > 0)
            .ToList();
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

    private static List<DictationWordComparison> BuildWordComparison(string? answeredText, string correctAnswer)
    {
        var answered = TokenizeWords(answeredText);
        var correct = TokenizeWords(correctAnswer);
        // ponytail: O(n*m) alignment is appropriate for sentences; revisit only for paragraph dictation.
        var distance = new int[answered.Count + 1, correct.Count + 1];

        for (var i = 0; i <= answered.Count; i++) distance[i, 0] = i;
        for (var j = 0; j <= correct.Count; j++) distance[0, j] = j;

        for (var i = 1; i <= answered.Count; i++)
        {
            for (var j = 1; j <= correct.Count; j++)
            {
                var substitution = distance[i - 1, j - 1] +
                    (answered[i - 1].Normalized == correct[j - 1].Normalized ? 0 : 1);
                distance[i, j] = Math.Min(substitution,
                    Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1));
            }
        }

        var result = new List<DictationWordComparison>();
        var answeredIndex = answered.Count;
        var correctIndex = correct.Count;

        while (answeredIndex > 0 || correctIndex > 0)
        {
            if (answeredIndex > 0 && correctIndex > 0)
            {
                var matches = answered[answeredIndex - 1].Normalized == correct[correctIndex - 1].Normalized;
                var substitution = distance[answeredIndex - 1, correctIndex - 1] + (matches ? 0 : 1);
                if (distance[answeredIndex, correctIndex] == substitution)
                {
                    result.Add(new DictationWordComparison
                    {
                        Status = matches ? DictationWordStatus.Correct : DictationWordStatus.Incorrect,
                        AnsweredWord = answered[answeredIndex - 1].Original,
                        CorrectWord = correct[correctIndex - 1].Original
                    });
                    answeredIndex--;
                    correctIndex--;
                    continue;
                }
            }

            if (answeredIndex > 0 && distance[answeredIndex, correctIndex] == distance[answeredIndex - 1, correctIndex] + 1)
            {
                result.Add(new DictationWordComparison
                {
                    Status = DictationWordStatus.Extra,
                    AnsweredWord = answered[answeredIndex - 1].Original
                });
                answeredIndex--;
            }
            else
            {
                result.Add(new DictationWordComparison
                {
                    Status = DictationWordStatus.Missing,
                    CorrectWord = correct[correctIndex - 1].Original
                });
                correctIndex--;
            }
        }

        result.Reverse();
        return result;
    }

    // Tạo gợi ý khi trả lờ sai: IPA và nghĩa
    private static string? BuildHint(Flashcard card)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(card.Pronunciation))
            parts.Add($"IPA: {card.Pronunciation}");
        if (!string.IsNullOrWhiteSpace(card.BackText))
            parts.Add($"Nghĩa: {card.BackText}");
        return parts.Count > 0 ? string.Join(" | ", parts) : null;
    }

    // Cập nhật UserProgress sau mỗi câu trả lờ
    private async Task UpdateUserProgressAsync(string userId, int flashcardId, bool isCorrect)
    {
        var progress = await _context.UserProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId && p.FlashcardId == flashcardId);

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
        progress.Status = isCorrect ? UserProgressStatus.Mastered : UserProgressStatus.Learning;

        if (isCorrect)
            progress.CorrectCount++;
        else
            progress.WrongCount++;

        progress.LastReviewed = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    // Đóng phiên học và lưu điểm
    public async Task<StudySession> CompleteSessionAsync(int sessionId, int score)
    {
        var session = await _context.StudySessions.FindAsync(sessionId);
        if (session == null)
            throw new KeyNotFoundException("Phiên học không tồn tại.");

        session.Score = score;
        session.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return session;
    }

    // Lấy dữ liệu tổng kết phiên học
    public async Task<DictationResult> GetSessionResultAsync(int sessionId, string userId)
    {
        var session = await _context.StudySessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            throw new KeyNotFoundException("Phiên học không tồn tại.");

        if (session.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền xem phiên học này.");

        var details = await _context.DictationSessionDetails
            .AsNoTracking()
            .Where(d => d.StudySessionId == sessionId)
            .Include(d => d.Flashcard)
            .ToListAsync();

        var total = details.Count;
        var correct = details.Count(d => d.IsCorrect);

        var wrongCards = details
            .Where(d => !d.IsCorrect && d.Flashcard != null)
            .Select(d => new DictationResultCard
            {
                Id = d.Flashcard!.Id,
                Term = d.Flashcard.FrontText,
                Definition = d.Flashcard.BackText,
                Pronunciation = d.Flashcard.Pronunciation,
                ExampleSentence = d.Flashcard.ExampleSentence,
                ExampleMeaning = d.Flashcard.ExampleMeaning
            })
            .ToList();

        return new DictationResult
        {
            SessionId = sessionId,
            ContentMode = session.DictationContentMode,
            TotalCards = total,
            CorrectCount = correct,
            Score = session.Score ?? 0,
            WrongCards = wrongCards
        };
    }
}
