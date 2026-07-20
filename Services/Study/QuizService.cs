using System.Data;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.StudyEvents;
using ltwnc.Services.StudyModes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ltwnc.Services.Study;

public class QuizService : IQuizService
{
    public const int DefaultQuizMinutes = 10;
    public const int MinimumQuizMinutes = 1;
    public const int MaximumQuizMinutes = 120;

    private readonly AppDbContext _context;
    private readonly IStudyModeStrategyResolver _strategyResolver;
    private readonly QuizQuestionFactory _questionFactory;
    private readonly IStudyEventPublisher _studyEvents;
    private readonly TimeProvider _timeProvider;

    public QuizService(
        AppDbContext context,
        IStudyModeStrategyResolver strategyResolver,
        QuizQuestionFactory questionFactory,
        IStudyEventPublisher studyEvents,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _strategyResolver = strategyResolver;
        _questionFactory = questionFactory;
        _studyEvents = studyEvents;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<QuizSetupState> GetSetupAsync(int setId, string userId)
    {
        FlashcardSet set = await GetOwnedSetAsync(setId, userId);
        StudySession? activeSession = await _context.StudySessions
            .AsNoTracking()
            .Where(session => session.FlashcardSetId == setId
                && session.UserId == userId
                && session.Mode == StudyMode.Quiz
                && session.Score == null
                && session.CompletedAt == null)
            .OrderByDescending(session => session.Id)
            .FirstOrDefaultAsync();

        return new QuizSetupState
        {
            SetId = set.Id,
            SetTitle = set.Title,
            ActiveSession = activeSession
        };
    }

    public async Task<StudySession> StartNewAsync(
        int setId,
        string userId,
        UserStudySettings settings,
        int? timeLimitMinutes)
    {
        if (timeLimitMinutes.HasValue
            && timeLimitMinutes is < MinimumQuizMinutes or > MaximumQuizMinutes)
        {
            throw new ArgumentOutOfRangeException(nameof(timeLimitMinutes));
        }

        await GetOwnedSetAsync(setId, userId);
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
            DateTime now = GetUtcNow();
            await _context.StudySessions
                .Where(session => session.FlashcardSetId == setId
                    && session.UserId == userId
                    && session.Mode == StudyMode.Quiz
                    && session.Score == null
                    && session.CompletedAt == null)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(session => session.CompletedAt, now));

            var session = new StudySession
            {
                FlashcardSetId = setId,
                UserId = userId,
                Mode = StudyMode.Quiz,
                CompletedAt = null,
                QuizStartedAtUtc = timeLimitMinutes.HasValue ? now : null,
                QuizTimeLimitSeconds = timeLimitMinutes.HasValue
                    ? timeLimitMinutes.Value * 60
                    : null
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
        catch (DbUpdateException exception) when (IsActiveQuizUniqueConflict(exception))
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            _context.ChangeTracker.Clear();
            StudySession? winningSession = await _context.StudySessions
                .AsNoTracking()
                .Where(session => session.FlashcardSetId == setId
                    && session.UserId == userId
                    && session.Mode == StudyMode.Quiz
                    && session.Score == null
                    && session.CompletedAt == null)
                .OrderByDescending(session => session.Id)
                .FirstOrDefaultAsync();
            if (winningSession != null)
            {
                return winningSession;
            }

            throw;
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
                && session.Score == null
                && session.CompletedAt == null)
            .OrderByDescending(session => session.Id)
            .FirstOrDefaultAsync();
        if (existingSession != null)
        {
            bool hasUnansweredQuestion = await _context.QuizSessionQuestions.AnyAsync(question =>
                question.StudySessionId == existingSession.Id
                && question.IsCorrect == null);
            if (!hasUnansweredQuestion)
            {
                await RecoverCompletedSessionIfNeededAsync(existingSession);
            }

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
                Mode = StudyMode.Quiz,
                CompletedAt = null
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
        catch (DbUpdateException exception) when (IsActiveQuizUniqueConflict(exception))
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            _context.ChangeTracker.Clear();
            StudySession? winner = await _context.StudySessions
                .AsNoTracking()
                .Where(row => row.FlashcardSetId == setId
                    && row.UserId == userId
                    && row.Mode == StudyMode.Quiz
                    && row.Score == null
                    && row.CompletedAt == null)
                .OrderByDescending(row => row.Id)
                .FirstOrDefaultAsync();
            if (winner != null)
            {
                return winner;
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

    public async Task<QuizQuestionState> GetCurrentQuestionAsync(
        int setId,
        int sessionId,
        string userId,
        int? questionId = null)
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

        if (IsAbandoned(session))
        {
            throw await CreateAbandonedExceptionAsync(session);
        }

        DateTime now = GetUtcNow();
        if (IsExpired(session, now))
        {
            await CompleteExpiredAsync(setId, sessionId, userId);
            throw new QuizExpiredException();
        }

        List<QuizSessionQuestion> questions = await _context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sessionId)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        StudySession authoritativeSession = await _context.StudySessions
            .AsNoTracking()
            .SingleAsync(row => row.Id == sessionId);
        if (IsAbandoned(authoritativeSession))
        {
            throw await CreateAbandonedExceptionAsync(authoritativeSession);
        }

        int totalQuestions = questions.Count;
        int answeredCount = questions.Count(question => question.IsCorrect != null);
        int correctCount = questions.Count(question => question.IsCorrect == true);
        QuizSessionQuestion? pendingQuestion = questions
            .FirstOrDefault(question => question.IsCorrect == null);
        QuizSessionQuestion? currentQuestion = questionId.HasValue
            ? questions.SingleOrDefault(question => question.Id == questionId.Value)
            : pendingQuestion;
        if (questionId.HasValue && currentQuestion == null)
        {
            throw new KeyNotFoundException("Câu hỏi trắc nghiệm không tồn tại.");
        }

        if (currentQuestion == null && session.Score == null)
        {
            await RecoverCompletedSessionIfNeededAsync(session);
        }

        bool isReviewOnly = currentQuestion?.IsCorrect != null;
        int currentQuestionIndex = currentQuestion == null
            ? -1
            : questions.FindIndex(question => question.Id == currentQuestion.Id);

        return new QuizQuestionState
        {
            SessionId = session.Id,
            SetId = session.FlashcardSetId,
            SetTitle = session.FlashcardSet?.Title ?? string.Empty,
            TotalQuestions = totalQuestions,
            AnsweredCount = answeredCount,
            CorrectCount = correctCount,
            DeadlineUtc = GetDeadlineUtc(authoritativeSession),
            RemainingSeconds = GetRemainingSeconds(authoritativeSession, now),
            Question = currentQuestion,
            IsReviewOnly = isReviewOnly,
            SelectedChoiceIndex = isReviewOnly ? currentQuestion!.SelectedChoiceIndex : null,
            CorrectChoiceIndex = isReviewOnly ? currentQuestion!.CorrectChoiceIndex : null,
            IsCorrect = isReviewOnly ? currentQuestion!.IsCorrect : null,
            PreviousQuestionId = currentQuestionIndex > 0
                ? questions[currentQuestionIndex - 1].Id
                : null,
            NextQuestionId = currentQuestionIndex >= 0
                && currentQuestionIndex < questions.Count - 1
                    ? questions[currentQuestionIndex + 1].Id
                    : null,
            CurrentPendingQuestionId = pendingQuestion?.Id
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

        if (IsAbandoned(session))
        {
            throw await CreateAbandonedExceptionAsync(session);
        }

        DateTime now = GetUtcNow();
        if (IsExpired(session, now))
        {
            await CompleteExpiredAsync(setId, sessionId, userId);
            throw new QuizExpiredException();
        }

        QuizSessionQuestion? question = await _context.QuizSessionQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == questionId);
        if (question == null || question.StudySessionId != sessionId)
        {
            throw new KeyNotFoundException("Câu hỏi trắc nghiệm không tồn tại.");
        }

        IDbContextTransaction? transaction = null;
        if (_context.Database.IsRelational())
        {
            transaction = await _context.Database.BeginTransactionAsync();
        }

        StudySessionCompletedEvent? completionEvent = null;
        QuizAnswerResult answerResult;
        try
        {
            StudySession persistedSession = await _context.StudySessions
                .AsNoTracking()
                .SingleAsync(row => row.Id == sessionId);
            DateTime writeNow = GetUtcNow();
            if (IsAbandoned(persistedSession))
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                    await transaction.DisposeAsync();
                    transaction = null;
                }

                throw await CreateAbandonedExceptionAsync(persistedSession);
            }

            if (IsExpired(persistedSession, writeNow))
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                    await transaction.DisposeAsync();
                    transaction = null;
                }

                await CompleteExpiredAsync(setId, sessionId, userId);
                throw new QuizExpiredException();
            }

            bool isCorrect = selectedChoiceIndex == question.CorrectChoiceIndex;
            int affected = await _context.QuizSessionQuestions
                .Where(row => row.Id == questionId
                    && row.IsCorrect == null
                    && row.StudySession!.Score == null
                    && row.StudySession.CompletedAt == null)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(row => row.SelectedChoiceIndex, selectedChoiceIndex)
                    .SetProperty(row => row.IsCorrect, isCorrect)
                    .SetProperty(row => row.AnsweredAt, writeNow));
            if (affected == 0)
            {
                StudySession currentSession = await _context.StudySessions
                    .AsNoTracking()
                    .SingleAsync(row => row.Id == sessionId);
                if (IsAbandoned(currentSession))
                {
                    throw await CreateAbandonedExceptionAsync(currentSession);
                }

                QuizSessionQuestion storedQuestion = await _context.QuizSessionQuestions
                    .AsNoTracking()
                    .SingleAsync(row => row.Id == questionId);
                if (storedQuestion.SelectedChoiceIndex != selectedChoiceIndex)
                {
                    throw new QuizConflictException(
                        "Câu hỏi đã được trả lời bằng lựa chọn khác.");
                }

                bool storedIsLastQuestion = !await _context.QuizSessionQuestions.AnyAsync(row =>
                    row.StudySessionId == sessionId
                    && row.IsCorrect == null);
                if (storedIsLastQuestion)
                {
                    completionEvent = await CompleteSessionIfEligibleAsync(session);
                }

                answerResult = new QuizAnswerResult(
                    storedQuestion.IsCorrect == true,
                    storedQuestion.CorrectChoiceIndex,
                    storedIsLastQuestion);
            }
            else
            {
                bool isLastQuestion = !await _context.QuizSessionQuestions.AnyAsync(row =>
                    row.StudySessionId == sessionId
                    && row.IsCorrect == null);
                if (isLastQuestion)
                {
                    completionEvent = await CompleteSessionIfEligibleAsync(session);
                }

                answerResult = new QuizAnswerResult(
                    isCorrect,
                    question.CorrectChoiceIndex,
                    isLastQuestion);
            }

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
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

        if (completionEvent != null)
        {
            await _studyEvents.PublishAsync(completionEvent);
        }

        return answerResult;
    }

    public async Task CompleteExpiredAsync(int setId, int sessionId, string userId)
    {
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
            throw new UnauthorizedAccessException("Không có quyền hoàn thành phiên trắc nghiệm này.");
        }

        if (IsAbandoned(session))
        {
            throw await CreateAbandonedExceptionAsync(session);
        }

        DateTime now = GetUtcNow();
        StudySession authoritativeSession = await _context.StudySessions
            .AsNoTracking()
            .SingleAsync(row => row.Id == sessionId);
        if (IsAbandoned(authoritativeSession))
        {
            throw await CreateAbandonedExceptionAsync(authoritativeSession);
        }

        if (!IsExpired(authoritativeSession, now))
        {
            throw new QuizNotExpiredException(GetRemainingSeconds(authoritativeSession, now) ?? 0);
        }

        await CompleteExpiredSessionAsync(authoritativeSession, now);

        StudySession completedSession = await _context.StudySessions
            .AsNoTracking()
            .SingleAsync(row => row.Id == sessionId);
        if (IsAbandoned(completedSession))
        {
            throw await CreateAbandonedExceptionAsync(completedSession);
        }

        if (completedSession.Score == null)
        {
            throw new QuizConflictException(
                "Không thể hoàn thành phiên trắc nghiệm vì trạng thái phiên đã thay đổi.");
        }
    }

    private async Task CompleteExpiredSessionAsync(StudySession session, DateTime now)
    {
        IDbContextTransaction? transaction = null;
        if (_context.Database.IsRelational())
        {
            transaction = await _context.Database.BeginTransactionAsync();
        }

        StudySessionCompletedEvent? completionEvent;
        try
        {
            await _context.QuizSessionQuestions
                .Where(question => question.StudySessionId == session.Id
                    && question.IsCorrect == null
                    && question.StudySession!.Score == null
                    && question.StudySession.CompletedAt == null)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(question => question.IsCorrect, false)
                    .SetProperty(question => question.AnsweredAt, now));
            completionEvent = await CompleteSessionIfEligibleAsync(session, now);

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
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

        if (completionEvent != null)
        {
            await _studyEvents.PublishAsync(completionEvent);
        }
    }

    private async Task<StudySessionCompletedEvent?> CompleteSessionIfEligibleAsync(
        StudySession session,
        DateTime? completedAtUtc = null)
    {
        bool hasUnansweredQuestion = await _context.QuizSessionQuestions.AnyAsync(row =>
            row.StudySessionId == session.Id
            && row.IsCorrect == null);
        if (hasUnansweredQuestion)
        {
            return null;
        }

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
        DateTime completedAt = completedAtUtc ?? GetUtcNow();
        int affected = await _context.StudySessions
            .Where(row => row.Id == session.Id
                && row.FlashcardSetId == session.FlashcardSetId
                && row.UserId == session.UserId
                && row.Mode == StudyMode.Quiz
                && row.Score == null
                && row.CompletedAt == null)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(row => row.Score, score)
                .SetProperty(row => row.CompletedAt, completedAt));
        if (affected == 1)
        {
            return new StudySessionCompletedEvent(
                UserId: session.UserId,
                OccurredAtUtc: completedAt,
                SetId: session.FlashcardSetId,
                SessionId: session.Id,
                Mode: StudyMode.Quiz,
                Score: score);
        }

        return null;
    }

    private DateTime GetUtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static bool IsExpired(StudySession session, DateTime now)
    {
        DateTime? deadline = GetDeadlineUtc(session);
        return deadline.HasValue && now >= deadline.Value;
    }

    private static DateTime? GetDeadlineUtc(StudySession session)
    {
        if (session.QuizStartedAtUtc is not DateTime startedAtUtc
            || session.QuizTimeLimitSeconds is not int timeLimitSeconds
            || timeLimitSeconds <= 0)
        {
            return null;
        }

        return DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Utc)
            .AddSeconds(timeLimitSeconds);
    }

    private static int? GetRemainingSeconds(StudySession session, DateTime now)
    {
        DateTime? deadline = GetDeadlineUtc(session);
        if (!deadline.HasValue)
        {
            return null;
        }

        return Math.Max(0, (int)Math.Ceiling((deadline.Value - now).TotalSeconds));
    }

    private async Task RecoverCompletedSessionIfNeededAsync(StudySession session)
    {
        IDbContextTransaction? transaction = null;
        if (_context.Database.IsRelational())
        {
            transaction = await _context.Database.BeginTransactionAsync();
        }

        StudySessionCompletedEvent? completionEvent;
        try
        {
            completionEvent = await CompleteSessionIfEligibleAsync(session);
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
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

        if (completionEvent != null)
        {
            await _studyEvents.PublishAsync(completionEvent);
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
                question.SelectedChoiceIndex is int selectedChoiceIndex
                    ? question.Choices[selectedChoiceIndex]
                    : "Chưa trả lời",
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
            await LoadQuizSourceAsync(setId, sessionId, userId, requireCompleted: true);

        List<QuizSessionQuestion> wrongQuestions = sourceQuestions
            .Where(question => question.IsCorrect == false)
            .ToList();
        if (wrongQuestions.Count == 0)
        {
            throw new QuizConflictException("Phiên trắc nghiệm không có câu trả lời sai.");
        }

        return await CreateReplacementSessionAsync(
            sourceSession,
            wrongQuestions,
            preserveDirections: true,
            reuseMatchingActiveSession: true,
            retryKind: QuizRetryKind.Wrong);
    }

    public async Task<StudySession> RetryAllAsync(
        int setId,
        int sessionId,
        string userId)
    {
        (StudySession sourceSession, List<QuizSessionQuestion> sourceQuestions) =
            await LoadQuizSourceAsync(setId, sessionId, userId, requireCompleted: true);

        return await CreateReplacementSessionAsync(
            sourceSession,
            sourceQuestions,
            preserveDirections: false,
            reuseMatchingActiveSession: true,
            retryKind: QuizRetryKind.All);
    }

    public async Task<StudySession> RestartAsync(
        int setId,
        int sessionId,
        string userId)
    {
        (StudySession sourceSession, List<QuizSessionQuestion> sourceQuestions) =
            await LoadQuizSourceAsync(setId, sessionId, userId, requireCompleted: false);

        DateTime restartNow = GetUtcNow();
        if (IsExpired(sourceSession, restartNow))
        {
            await CompleteExpiredSessionAsync(sourceSession, restartNow);
            throw new QuizExpiredException();
        }

        return await CreateReplacementSessionAsync(
            sourceSession,
            sourceQuestions,
            preserveDirections: false,
            reuseMatchingActiveSession: false,
            retryKind: null);
    }

    private async Task<(StudySession Session, List<QuizSessionQuestion> Questions)>
        LoadQuizSourceAsync(
            int setId,
            int sessionId,
            string userId,
            bool requireCompleted)
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

        if (requireCompleted && sourceSession.Score == null)
        {
            throw new QuizConflictException("Phiên trắc nghiệm chưa hoàn thành.");
        }

        if (!requireCompleted && IsAbandoned(sourceSession))
        {
            throw await CreateAbandonedExceptionAsync(sourceSession);
        }

        if (!requireCompleted && sourceSession.Score != null)
        {
            throw new QuizConflictException("Phiên trắc nghiệm không còn đang làm.");
        }

        List<QuizSessionQuestion> sourceQuestions = await _context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sourceSession.Id)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        return (sourceSession, sourceQuestions);
    }

    private async Task<StudySession> CreateReplacementSessionAsync(
        StudySession sourceSession,
        IReadOnlyList<QuizSessionQuestion> sourceQuestions,
        bool preserveDirections,
        bool reuseMatchingActiveSession,
        QuizRetryKind? retryKind)
    {
        if (reuseMatchingActiveSession)
        {
            StudySession? activeSession = await FindActiveQuizSessionAsync(sourceSession);
            if (activeSession != null
                && await MatchesRequestedRetryAsync(
                    activeSession,
                    sourceQuestions,
                    preserveDirections,
                    sourceSession,
                    retryKind))
            {
                return activeSession;
            }
        }

        IDbContextTransaction? transaction = null;
        if (_context.Database.IsRelational())
        {
            transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);
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
            if (reuseMatchingActiveSession)
            {
                StudySession? activeSession = await FindActiveQuizSessionAsync(sourceSession);
                if (activeSession != null
                    && await MatchesRequestedRetryAsync(
                        activeSession,
                        sourceQuestions,
                        preserveDirections,
                        sourceSession,
                        retryKind))
                {
                    if (transaction != null)
                    {
                        await transaction.CommitAsync();
                    }

                    return activeSession;
                }
            }

            DateTime now = GetUtcNow();
            if (retryKind is null && IsExpired(sourceSession, now))
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                    await transaction.DisposeAsync();
                    transaction = null;
                }

                await CompleteExpiredSessionAsync(sourceSession, now);
                throw new QuizExpiredException();
            }

            await _context.StudySessions
                .Where(session => session.FlashcardSetId == sourceSession.FlashcardSetId
                    && session.UserId == sourceSession.UserId
                    && session.Mode == StudyMode.Quiz
                    && session.Score == null
                    && session.CompletedAt == null)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(session => session.CompletedAt, now));

            var replacementSession = new StudySession
            {
                FlashcardSetId = sourceSession.FlashcardSetId,
                UserId = sourceSession.UserId,
                Mode = StudyMode.Quiz,
                CompletedAt = null,
                QuizStartedAtUtc = sourceSession.QuizTimeLimitSeconds.HasValue ? now : null,
                QuizTimeLimitSeconds = sourceSession.QuizTimeLimitSeconds,
                QuizRetrySourceSessionId = retryKind.HasValue ? sourceSession.Id : null,
                QuizRetryKind = retryKind
            };
            List<QuizSessionQuestion> replacementQuestions =
                await _questionFactory.BuildQuestionsAsync(
                    sourceSession.FlashcardSetId,
                    sourceSession.UserId,
                    sourceCards,
                    fixedDirections);
            foreach (QuizSessionQuestion question in replacementQuestions)
            {
                question.StudySession = replacementSession;
            }

            _context.QuizSessionQuestions.AddRange(replacementQuestions);
            await _context.SaveChangesAsync();

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            return replacementSession;
        }
        catch (DbUpdateException exception) when (IsActiveQuizUniqueConflict(exception))
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            _context.ChangeTracker.Clear();
            StudySession? activeSession = await FindActiveQuizSessionAsync(sourceSession);
            if (reuseMatchingActiveSession
                && activeSession != null
                && await MatchesRequestedRetryAsync(
                    activeSession,
                    sourceQuestions,
                    preserveDirections,
                    sourceSession,
                    retryKind))
            {
                return activeSession;
            }

            throw;
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

    private Task<StudySession?> FindActiveQuizSessionAsync(StudySession sourceSession) =>
        _context.StudySessions
            .AsNoTracking()
            .Where(session => session.FlashcardSetId == sourceSession.FlashcardSetId
                && session.UserId == sourceSession.UserId
                && session.Mode == StudyMode.Quiz
                && session.Score == null
                && session.CompletedAt == null)
            .OrderByDescending(session => session.Id)
            .FirstOrDefaultAsync();

    private static bool IsAbandoned(StudySession session) =>
        session.Score == null && session.CompletedAt != null;

    private async Task<QuizSessionAbandonedException> CreateAbandonedExceptionAsync(
        StudySession session)
    {
        StudySession? activeSession = await FindActiveQuizSessionAsync(session);
        return new QuizSessionAbandonedException(activeSession?.Id);
    }

    private async Task<bool> MatchesRequestedRetryAsync(
        StudySession activeSession,
        IReadOnlyList<QuizSessionQuestion> sourceQuestions,
        bool preserveDirections,
        StudySession sourceSession,
        QuizRetryKind? retryKind)
    {
        if (retryKind is null
            || activeSession.QuizRetrySourceSessionId != sourceSession.Id
            || activeSession.QuizRetryKind != retryKind
            || activeSession.QuizTimeLimitSeconds != sourceSession.QuizTimeLimitSeconds)
        {
            return false;
        }

        List<QuizSessionQuestion> activeQuestions = await _context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == activeSession.Id)
            .ToListAsync();
        if (activeQuestions.Count != sourceQuestions.Count)
        {
            return false;
        }

        Dictionary<int, QuizSessionQuestion> sourceByCardId = sourceQuestions
            .ToDictionary(question => question.FlashcardId);
        return activeQuestions.All(question => sourceByCardId.TryGetValue(
                question.FlashcardId,
                out QuizSessionQuestion? sourceQuestion)
            && (!preserveDirections || question.Direction == sourceQuestion.Direction));
    }

    private async Task<FlashcardSet> GetOwnedSetAsync(int setId, string userId)
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

        return set;
    }

    private static bool IsActiveQuizUniqueConflict(DbUpdateException exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current.Message.Contains(
                    "IX_StudySessions_UserId_FlashcardSetId_Mode",
                    StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains(
                    "StudySessions.UserId, StudySessions.FlashcardSetId, StudySessions.Mode",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
