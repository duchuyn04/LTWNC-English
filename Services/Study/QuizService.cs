using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.StudyEvents;
using ltwnc.Services.StudyModes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ltwnc.Services.Study;

public class QuizService : IQuizService
{
    private readonly AppDbContext _context;
    private readonly IStudyModeStrategyResolver _strategyResolver;
    private readonly QuizQuestionFactory _questionFactory;
    private readonly IStudyEventPublisher _studyEvents;

    public QuizService(
        AppDbContext context,
        IStudyModeStrategyResolver strategyResolver,
        QuizQuestionFactory questionFactory,
        IStudyEventPublisher studyEvents)
    {
        _context = context;
        _strategyResolver = strategyResolver;
        _questionFactory = questionFactory;
        _studyEvents = studyEvents;
    }

    public async Task<StudySession> StartOrResumeAsync(
        int setId,
        string userId,
        UserStudySettings settings)
    {
        FlashcardSet? set = await _context.FlashcardSets
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == setId);
        if (set == null)
        {
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        }

        if (set.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");
        }

        StudySession? existingSession = await _context.StudySessions
            .AsNoTracking()
            .Where(session => session.FlashcardSetId == setId
                && session.UserId == userId
                && session.Mode == StudyMode.Quiz
                && session.Score == null)
            .Where(session => _context.QuizSessionQuestions.Any(question =>
                question.StudySessionId == session.Id
                && question.SelectedChoiceIndex == null))
            .OrderByDescending(session => session.Id)
            .FirstOrDefaultAsync();
        if (existingSession != null)
        {
            return existingSession;
        }

        IStudyModeStrategy strategy = _strategyResolver.Resolve(StudyMode.Quiz);
        List<Flashcard> sourceCards = await strategy.GetCardsAsync(setId, settings, userId);
        if (sourceCards.Count == 0)
        {
            throw new QuizUnavailableException(
                "Không có thẻ phù hợp với bộ lọc hiện tại.");
        }

        IDbContextTransaction? transaction = null;
        if (_context.Database.IsRelational())
        {
            transaction = await _context.Database.BeginTransactionAsync();
        }

        try
        {
            var session = new StudySession
            {
                FlashcardSetId = setId,
                UserId = userId,
                Mode = StudyMode.Quiz
            };
            List<QuizSessionQuestion> questions = await _questionFactory.BuildQuestionsAsync(
                setId,
                userId,
                sourceCards);
            foreach (QuizSessionQuestion question in questions)
            {
                question.StudySession = session;
            }

            _context.QuizSessionQuestions.AddRange(questions);
            await _context.SaveChangesAsync();

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            return session;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<QuizQuestionState> GetCurrentQuestionAsync(
        int setId,
        int sessionId,
        string userId)
    {
        StudySession? session = await _context.StudySessions
            .AsNoTracking()
            .Include(row => row.FlashcardSet)
            .FirstOrDefaultAsync(row => row.Id == sessionId);
        if (session == null
            || session.FlashcardSetId != setId
            || session.Mode != StudyMode.Quiz)
        {
            throw new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.");
        }

        if (session.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền xem phiên trắc nghiệm này.");
        }

        IQueryable<QuizSessionQuestion> questions = _context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sessionId);
        int totalQuestions = await questions.CountAsync();
        int answeredCount = await questions.CountAsync(question =>
            question.SelectedChoiceIndex != null);
        int correctCount = await questions.CountAsync(question => question.IsCorrect == true);
        QuizSessionQuestion? currentQuestion = await questions
            .Where(question => question.SelectedChoiceIndex == null)
            .OrderBy(question => question.OrderIndex)
            .FirstOrDefaultAsync();

        return new QuizQuestionState
        {
            SessionId = session.Id,
            SetId = session.FlashcardSetId,
            SetTitle = session.FlashcardSet?.Title ?? string.Empty,
            TotalQuestions = totalQuestions,
            AnsweredCount = answeredCount,
            CorrectCount = correctCount,
            Question = currentQuestion
        };
    }

    public async Task<QuizAnswerResult> AnswerAsync(
        int setId,
        int sessionId,
        int questionId,
        int selectedChoiceIndex,
        string userId)
    {
        if (selectedChoiceIndex is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedChoiceIndex));
        }

        StudySession? session = await _context.StudySessions
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == sessionId);
        if (session == null
            || session.FlashcardSetId != setId
            || session.Mode != StudyMode.Quiz)
        {
            throw new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.");
        }

        if (session.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền trả lời phiên trắc nghiệm này.");
        }

        QuizSessionQuestion? question = await _context.QuizSessionQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == questionId);
        if (question == null || question.StudySessionId != sessionId)
        {
            throw new KeyNotFoundException("Câu hỏi trắc nghiệm không tồn tại.");
        }

        bool isCorrect = selectedChoiceIndex == question.CorrectChoiceIndex;
        int affected = await _context.QuizSessionQuestions
            .Where(row => row.Id == questionId && row.SelectedChoiceIndex == null)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(row => row.SelectedChoiceIndex, selectedChoiceIndex)
                .SetProperty(row => row.IsCorrect, isCorrect)
                .SetProperty(row => row.AnsweredAt, DateTime.UtcNow));
        if (affected == 0)
        {
            QuizSessionQuestion storedQuestion = await _context.QuizSessionQuestions
                .AsNoTracking()
                .SingleAsync(row => row.Id == questionId);
            if (storedQuestion.SelectedChoiceIndex == selectedChoiceIndex)
            {
                bool storedIsLastQuestion = !await _context.QuizSessionQuestions.AnyAsync(row =>
                    row.StudySessionId == sessionId
                    && row.SelectedChoiceIndex == null);
                if (storedIsLastQuestion)
                {
                    await CompleteSessionIfNeededAsync(session);
                }

                return new QuizAnswerResult(
                    storedQuestion.IsCorrect == true,
                    storedQuestion.CorrectChoiceIndex,
                    storedIsLastQuestion);
            }

            throw new QuizConflictException("Câu hỏi đã được trả lời bằng lựa chọn khác.");
        }

        bool isLastQuestion = !await _context.QuizSessionQuestions.AnyAsync(row =>
            row.StudySessionId == sessionId
            && row.SelectedChoiceIndex == null);
        if (isLastQuestion)
        {
            await CompleteSessionIfNeededAsync(session);
        }

        return new QuizAnswerResult(
            isCorrect,
            question.CorrectChoiceIndex,
            isLastQuestion);
    }

    private async Task CompleteSessionIfNeededAsync(StudySession session)
    {
        int totalCount = await _context.QuizSessionQuestions.CountAsync(row =>
            row.StudySessionId == session.Id);
        int correctCount = await _context.QuizSessionQuestions.CountAsync(row =>
            row.StudySessionId == session.Id
            && row.IsCorrect == true);
        int score = totalCount == 0
            ? 0
            : (int)Math.Round(
                correctCount * 100.0 / totalCount,
                MidpointRounding.AwayFromZero);
        DateTime completedAt = DateTime.UtcNow;
        int affected = await _context.StudySessions
            .Where(row => row.Id == session.Id
                && row.FlashcardSetId == session.FlashcardSetId
                && row.UserId == session.UserId
                && row.Mode == StudyMode.Quiz
                && row.Score == null)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(row => row.Score, score)
                .SetProperty(row => row.CompletedAt, completedAt));
        if (affected == 1)
        {
            await _studyEvents.PublishAsync(new StudySessionCompletedEvent(
                UserId: session.UserId,
                OccurredAtUtc: completedAt,
                SetId: session.FlashcardSetId,
                SessionId: session.Id,
                Mode: StudyMode.Quiz,
                Score: score));
        }
    }

    public async Task<QuizSessionResult> GetResultAsync(
        int setId,
        int sessionId,
        string userId)
    {
        StudySession? session = await _context.StudySessions
            .AsNoTracking()
            .Include(row => row.FlashcardSet)
            .FirstOrDefaultAsync(row => row.Id == sessionId);
        if (session == null
            || session.FlashcardSetId != setId
            || session.Mode != StudyMode.Quiz)
        {
            throw new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.");
        }

        if (session.UserId != userId)
        {
            throw new UnauthorizedAccessException(
                "Không có quyền xem kết quả phiên trắc nghiệm này.");
        }

        if (session.Score == null)
        {
            throw new QuizConflictException("Phiên trắc nghiệm chưa hoàn thành.");
        }

        List<QuizSessionQuestion> questions = await _context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sessionId)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        List<QuizWrongAnswer> wrongAnswers = questions
            .Where(question => question.IsCorrect == false)
            .Select(question => new QuizWrongAnswer(
                question.FlashcardId,
                question.Direction,
                question.PromptText,
                question.Choices[question.SelectedChoiceIndex!.Value],
                question.Choices[question.CorrectChoiceIndex]))
            .ToList();

        return new QuizSessionResult
        {
            SessionId = session.Id,
            SetId = session.FlashcardSetId,
            SetTitle = session.FlashcardSet?.Title ?? string.Empty,
            TotalQuestions = questions.Count,
            CorrectCount = questions.Count(question => question.IsCorrect == true),
            Score = session.Score.Value,
            WrongAnswers = wrongAnswers
        };
    }

    public async Task<StudySession> RetryWrongAsync(
        int setId,
        int sessionId,
        string userId)
    {
        (StudySession sourceSession, List<QuizSessionQuestion> sourceQuestions) =
            await LoadRetrySourceAsync(setId, sessionId, userId);
        List<QuizSessionQuestion> wrongQuestions = sourceQuestions
            .Where(question => question.IsCorrect == false)
            .ToList();
        if (wrongQuestions.Count == 0)
        {
            throw new QuizConflictException("Phiên trắc nghiệm không có câu trả lời sai.");
        }

        return await CreateRetrySessionAsync(
            sourceSession,
            wrongQuestions,
            preserveDirections: true);
    }

    public async Task<StudySession> RetryAllAsync(
        int setId,
        int sessionId,
        string userId)
    {
        (StudySession sourceSession, List<QuizSessionQuestion> sourceQuestions) =
            await LoadRetrySourceAsync(setId, sessionId, userId);
        return await CreateRetrySessionAsync(
            sourceSession,
            sourceQuestions,
            preserveDirections: false);
    }

    private async Task<(StudySession Session, List<QuizSessionQuestion> Questions)>
        LoadRetrySourceAsync(int setId, int sessionId, string userId)
    {
        StudySession? sourceSession = await _context.StudySessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.Id == sessionId);
        if (sourceSession == null
            || sourceSession.FlashcardSetId != setId
            || sourceSession.Mode != StudyMode.Quiz)
        {
            throw new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.");
        }

        if (sourceSession.UserId != userId)
        {
            throw new UnauthorizedAccessException(
                "Không có quyền tạo lại phiên trắc nghiệm này.");
        }

        if (sourceSession.Score == null)
        {
            throw new QuizConflictException("Phiên trắc nghiệm chưa hoàn thành.");
        }

        List<QuizSessionQuestion> sourceQuestions = await _context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sourceSession.Id)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        return (sourceSession, sourceQuestions);
    }

    private async Task<StudySession> CreateRetrySessionAsync(
        StudySession sourceSession,
        IReadOnlyList<QuizSessionQuestion> sourceQuestions,
        bool preserveDirections)
    {
        IDbContextTransaction? transaction = null;
        if (_context.Database.IsRelational())
        {
            transaction = await _context.Database.BeginTransactionAsync();
        }

        try
        {
            int[] sourceCardIds = sourceQuestions
                .Select(question => question.FlashcardId)
                .Distinct()
                .ToArray();
            List<Flashcard> storedCards = await _context.Flashcards
                .AsNoTracking()
                .Where(card => sourceCardIds.Contains(card.Id))
                .ToListAsync();
            if (storedCards.Count != sourceCardIds.Length)
            {
                throw new QuizUnavailableException(
                    "Một hoặc nhiều thẻ nguồn không còn khả dụng.");
            }

            Dictionary<int, Flashcard> cardsById = storedCards
                .ToDictionary(card => card.Id);
            List<Flashcard> sourceCards = sourceQuestions
                .Select(question => cardsById[question.FlashcardId])
                .ToList();
            IReadOnlyDictionary<int, QuizQuestionDirection>? fixedDirections =
                preserveDirections
                    ? sourceQuestions.ToDictionary(
                        question => question.FlashcardId,
                        question => question.Direction)
                    : null;
            var retrySession = new StudySession
            {
                FlashcardSetId = sourceSession.FlashcardSetId,
                UserId = sourceSession.UserId,
                Mode = StudyMode.Quiz
            };
            List<QuizSessionQuestion> retryQuestions =
                await _questionFactory.BuildQuestionsAsync(
                    sourceSession.FlashcardSetId,
                    sourceSession.UserId,
                    sourceCards,
                    fixedDirections);
            foreach (QuizSessionQuestion question in retryQuestions)
            {
                question.StudySession = retrySession;
            }

            _context.QuizSessionQuestions.AddRange(retryQuestions);
            await _context.SaveChangesAsync();

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            return retrySession;
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}
