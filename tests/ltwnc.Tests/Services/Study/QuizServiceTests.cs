using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Study;
using ltwnc.Services.StudyEvents;
using ltwnc.Services.StudyModes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ltwnc.Tests.Services;

public class QuizServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public async Task StartNew_rejects_duration_outside_supported_range(int timeLimitMinutes)
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            timeLimitMinutes));
    }

    [Fact]
    public async Task StartNew_abandons_active_attempt_and_creates_fresh_timed_session()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession oldSession = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());

        StudySession newSession = await service.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            15);

        database.Context.ChangeTracker.Clear();
        StudySession storedOldSession = await database.Context.StudySessions.SingleAsync(
            session => session.Id == oldSession.Id);
        Assert.NotNull(storedOldSession.CompletedAt);
        Assert.Null(storedOldSession.Score);
        Assert.Equal(15 * 60, newSession.QuizTimeLimitSeconds);
        Assert.NotNull(newSession.QuizStartedAtUtc);
        Assert.NotEqual(oldSession.Id, newSession.Id);
    }

    [Fact]
    public async Task StartNew_with_null_duration_creates_untimed_session()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(
            database.Context,
            new RecordingStudyEventPublisher());

        StudySession session = await service.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            null);

        Assert.Null(session.QuizStartedAtUtc);
        Assert.Null(session.QuizTimeLimitSeconds);

        QuizQuestionState state = await service.GetCurrentQuestionAsync(
            set.Id,
            session.Id,
            set.UserId);
        Assert.Null(state.DeadlineUtc);
        Assert.Null(state.RemainingSeconds);
    }

    [Fact]
    public async Task RetryAll_from_untimed_source_creates_untimed_session()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(
            database.Context,
            new RecordingStudyEventPublisher());
        StudySession source = await service.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            null);
        await CompleteQuizAsync(database.Context, service, set, source);

        StudySession retry = await service.RetryAllAsync(
            set.Id,
            source.Id,
            set.UserId);

        Assert.Null(retry.QuizStartedAtUtc);
        Assert.Null(retry.QuizTimeLimitSeconds);
    }

    [Fact]
    public async Task StartOrResume_creates_session_and_persisted_questions()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        var publisher = new RecordingStudyEventPublisher();
        QuizService service = CreateService(database.Context, publisher);

        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());

        Assert.Equal(StudyMode.Quiz, session.Mode);
        Assert.Equal(set.Id, session.FlashcardSetId);
        Assert.Equal(set.UserId, session.UserId);
        Assert.Null(session.Score);

        List<QuizSessionQuestion> questions = await database.Context.QuizSessionQuestions
            .Where(question => question.StudySessionId == session.Id)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        Assert.Equal(4, questions.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, questions.Select(question => question.OrderIndex));
        Assert.All(questions, question => Assert.Equal(4, question.Choices.Count));
    }

    [Fact]
    public async Task StartOrResume_returns_existing_incomplete_session()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession first = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());

        StudySession resumed = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());

        Assert.Equal(first.Id, resumed.Id);
        Assert.Equal(1, await database.Context.StudySessions.CountAsync());
        Assert.Equal(4, await database.Context.QuizSessionQuestions.CountAsync());
    }

    [Fact]
    public async Task StartOrResume_concurrent_requests_return_one_active_quiz_session()
    {
        await using var database = await SharedQuizTestDatabase.CreateAsync();
        int setId;
        string userId;
        await using (AppDbContext setupContext = await database.CreateContextAsync())
        {
            FlashcardSet set = await SeedQuestionPoolAsync(setupContext);
            setId = set.Id;
            userId = set.UserId;
        }

        await using AppDbContext firstContext = await database.CreateContextAsync();
        await using AppDbContext secondContext = await database.CreateContextAsync();
        QuizService firstService = CreateService(
            firstContext,
            new RecordingStudyEventPublisher());
        QuizService secondService = CreateService(
            secondContext,
            new RecordingStudyEventPublisher());
        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<StudySession> StartAsync(QuizService service)
        {
            await startGate.Task;
            return await service.StartOrResumeAsync(
                setId,
                userId,
                new UserStudySettings());
        }

        Task<StudySession> firstRequest = StartAsync(firstService);
        Task<StudySession> secondRequest = StartAsync(secondService);
        startGate.SetResult();
        StudySession[] sessions = await Task.WhenAll(firstRequest, secondRequest);

        Assert.Equal(sessions[0].Id, sessions[1].Id);
        await using AppDbContext verificationContext = await database.CreateContextAsync();
        Assert.Equal(1, await verificationContext.StudySessions.CountAsync(row =>
            row.FlashcardSetId == setId
            && row.UserId == userId
            && row.Mode == StudyMode.Quiz
            && row.Score == null));
        Assert.Equal(4, await verificationContext.QuizSessionQuestions.CountAsync());
    }

    [Fact]
    public async Task StartNew_concurrent_requests_leave_one_active_quiz_session()
    {
        await using var database = await SharedQuizTestDatabase.CreateAsync();
        int setId;
        string userId;
        await using (AppDbContext setupContext = await database.CreateContextAsync())
        {
            FlashcardSet set = await SeedQuestionPoolAsync(setupContext);
            setId = set.Id;
            userId = set.UserId;
        }

        await using AppDbContext firstContext = await database.CreateContextAsync();
        await using AppDbContext secondContext = await database.CreateContextAsync();
        QuizService firstService = CreateService(firstContext, new RecordingStudyEventPublisher());
        QuizService secondService = CreateService(secondContext, new RecordingStudyEventPublisher());
        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<StudySession> StartAsync(QuizService service)
        {
            await startGate.Task;
            return await service.StartNewAsync(setId, userId, new UserStudySettings(), 10);
        }

        Task<StudySession> firstRequest = StartAsync(firstService);
        Task<StudySession> secondRequest = StartAsync(secondService);
        startGate.SetResult();
        StudySession[] sessions = await Task.WhenAll(firstRequest, secondRequest);

        Assert.All(sessions, session => Assert.True(session.Id > 0));
        await using AppDbContext verificationContext = await database.CreateContextAsync();
        Assert.Equal(1, await verificationContext.StudySessions.CountAsync(row =>
            row.FlashcardSetId == setId
            && row.UserId == userId
            && row.Mode == StudyMode.Quiz
            && row.Score == null
            && row.CompletedAt == null));
    }

    [Fact]
    public async Task GetCurrentQuestion_returns_first_unanswered_with_session_counts()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion first = await database.Context.QuizSessionQuestions
            .OrderBy(question => question.OrderIndex)
            .FirstAsync();
        first.SelectedChoiceIndex = first.CorrectChoiceIndex;
        first.IsCorrect = true;
        first.AnsweredAt = DateTime.UtcNow;
        await database.Context.SaveChangesAsync();

        QuizQuestionState state = await service.GetCurrentQuestionAsync(
            set.Id,
            session.Id,
            set.UserId);

        Assert.Equal(session.Id, state.SessionId);
        Assert.Equal(set.Id, state.SetId);
        Assert.Equal(set.Title, state.SetTitle);
        Assert.Equal(4, state.TotalQuestions);
        Assert.Equal(1, state.AnsweredCount);
        Assert.Equal(1, state.CorrectCount);
        Assert.NotNull(state.Question);
        Assert.Equal(1, state.Question.OrderIndex);
        Assert.False(state.IsComplete);
    }

    [Fact]
    public async Task Abandoned_session_is_rejected_by_reads_answers_and_timeout_with_replacement()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession abandoned = await service.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            15);
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .Where(row => row.StudySessionId == abandoned.Id)
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();
        StudySession replacement = await service.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            10);
        database.Context.ChangeTracker.Clear();

        QuizSessionAbandonedException readException = await Assert.ThrowsAsync<QuizSessionAbandonedException>(
            () => service.GetCurrentQuestionAsync(set.Id, abandoned.Id, set.UserId));
        QuizSessionAbandonedException answerException = await Assert.ThrowsAsync<QuizSessionAbandonedException>(
            () => service.AnswerAsync(
                set.Id,
                abandoned.Id,
                question.Id,
                question.CorrectChoiceIndex,
                set.UserId));
        QuizSessionAbandonedException timeoutException = await Assert.ThrowsAsync<QuizSessionAbandonedException>(
            () => service.CompleteExpiredAsync(set.Id, abandoned.Id, set.UserId));

        Assert.Equal(replacement.Id, readException.ActiveSessionId);
        Assert.Equal(replacement.Id, answerException.ActiveSessionId);
        Assert.Equal(replacement.Id, timeoutException.ActiveSessionId);
        QuizSessionQuestion storedQuestion = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .SingleAsync(row => row.Id == question.Id);
        Assert.Null(storedQuestion.SelectedChoiceIndex);
        Assert.Null(storedQuestion.IsCorrect);
    }

    [Fact]
    public async Task GetCurrentQuestion_race_rejects_source_replaced_after_initial_activity_read()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        DateTimeOffset startedAt = new(2026, 7, 19, 8, 0, 0, TimeSpan.Zero);
        QuizService setupService = CreateService(
            database.Context,
            new RecordingStudyEventPublisher(),
            new FixedTimeProvider(startedAt));
        StudySession source = await setupService.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            15);
        int replacementId = 0;
        var replacingClock = new CallbackTimeProvider(
            startedAt.AddMinutes(1),
            () => replacementId = ReplaceActiveSession(database.Context, source, startedAt.AddMinutes(1)));
        QuizService raceService = CreateService(
            database.Context,
            new RecordingStudyEventPublisher(),
            replacingClock);

        QuizSessionAbandonedException exception = await Assert.ThrowsAsync<QuizSessionAbandonedException>(
            () => raceService.GetCurrentQuestionAsync(set.Id, source.Id, set.UserId));

        Assert.Equal(replacementId, exception.ActiveSessionId);
        Assert.Equal(replacementId, await database.Context.StudySessions
            .Where(row => row.FlashcardSetId == set.Id
                && row.UserId == set.UserId
                && row.Mode == StudyMode.Quiz
                && row.Score == null
                && row.CompletedAt == null)
            .Select(row => row.Id)
            .SingleAsync());
    }

    [Fact]
    public async Task GetCurrentQuestion_expired_race_navigates_to_replacement_not_abandoned_result()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        DateTimeOffset startedAt = new(2026, 7, 19, 8, 0, 0, TimeSpan.Zero);
        QuizService setupService = CreateService(
            database.Context,
            new RecordingStudyEventPublisher(),
            new FixedTimeProvider(startedAt));
        StudySession source = await setupService.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            1);
        int replacementId = 0;
        var replacingClock = new CallbackTimeProvider(
            startedAt.AddMinutes(2),
            () => replacementId = ReplaceActiveSession(database.Context, source, startedAt.AddMinutes(2)));
        QuizService raceService = CreateService(
            database.Context,
            new RecordingStudyEventPublisher(),
            replacingClock);

        QuizSessionAbandonedException exception = await Assert.ThrowsAsync<QuizSessionAbandonedException>(
            () => raceService.GetCurrentQuestionAsync(set.Id, source.Id, set.UserId));

        Assert.Equal(replacementId, exception.ActiveSessionId);
        Assert.All(
            await database.Context.QuizSessionQuestions
                .AsNoTracking()
                .Where(row => row.StudySessionId == source.Id)
                .ToListAsync(),
            question => Assert.Null(question.IsCorrect));
    }

    [Fact]
    public async Task CompleteExpired_race_rejects_source_replaced_after_initial_activity_read()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        DateTimeOffset startedAt = new(2026, 7, 19, 8, 0, 0, TimeSpan.Zero);
        QuizService setupService = CreateService(
            database.Context,
            new RecordingStudyEventPublisher(),
            new FixedTimeProvider(startedAt));
        StudySession source = await setupService.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            1);
        int replacementId = 0;
        var replacingClock = new CallbackTimeProvider(
            startedAt.AddMinutes(2),
            () => replacementId = ReplaceActiveSession(database.Context, source, startedAt.AddMinutes(2)));
        QuizService raceService = CreateService(
            database.Context,
            new RecordingStudyEventPublisher(),
            replacingClock);

        QuizSessionAbandonedException exception = await Assert.ThrowsAsync<QuizSessionAbandonedException>(
            () => raceService.CompleteExpiredAsync(set.Id, source.Id, set.UserId));

        Assert.Equal(replacementId, exception.ActiveSessionId);
        Assert.All(
            await database.Context.QuizSessionQuestions
                .AsNoTracking()
                .Where(row => row.StudySessionId == source.Id)
                .ToListAsync(),
            question => Assert.Null(question.IsCorrect));
    }

    [Fact]
    public async Task Restart_expired_attempt_completes_once_and_does_not_replace_it()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        var publisher = new RecordingStudyEventPublisher();
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(
            2026, 7, 19, 8, 0, 0, TimeSpan.Zero));
        QuizService service = CreateService(database.Context, publisher, timeProvider);
        StudySession source = await service.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            1);
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<QuizExpiredException>(() => service.RestartAsync(
            set.Id,
            source.Id,
            set.UserId));

        database.Context.ChangeTracker.Clear();
        StudySession stored = await database.Context.StudySessions
            .AsNoTracking()
            .SingleAsync(row => row.Id == source.Id);
        Assert.Equal(0, stored.Score);
        Assert.Equal(timeProvider.GetUtcNow().UtcDateTime, stored.CompletedAt);
        Assert.All(
            await database.Context.QuizSessionQuestions
                .AsNoTracking()
                .Where(row => row.StudySessionId == source.Id)
                .ToListAsync(),
            question => Assert.False(question.IsCorrect));
        Assert.Single(publisher.Events);
        Assert.Equal(1, await database.Context.StudySessions.CountAsync());
    }

    [Fact]
    public async Task Restart_crossing_deadline_during_replacement_completes_source_instead()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        DateTimeOffset startedAt = new(2026, 7, 19, 8, 0, 0, TimeSpan.Zero);
        QuizService setupService = CreateService(
            database.Context,
            new RecordingStudyEventPublisher(),
            new FixedTimeProvider(startedAt));
        StudySession source = await setupService.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            1);
        var publisher = new RecordingStudyEventPublisher();
        QuizService restartService = CreateService(
            database.Context,
            publisher,
            new SteppingTimeProvider(startedAt.AddSeconds(59), startedAt.AddSeconds(60)));

        await Assert.ThrowsAsync<QuizExpiredException>(() => restartService.RestartAsync(
            set.Id,
            source.Id,
            set.UserId));

        database.Context.ChangeTracker.Clear();
        Assert.Equal(0, await database.Context.StudySessions
            .Where(row => row.Id == source.Id)
            .Select(row => row.Score)
            .SingleAsync());
        Assert.Equal(1, await database.Context.StudySessions.CountAsync());
        Assert.Single(publisher.Events);
    }

    [Fact]
    public async Task Answer_race_rejects_write_when_parent_is_replaced_before_question_update()
    {
        var interceptor = new ReplaceSessionBeforeAnswerInterceptor();
        await using var database = await QuizTestDatabase.CreateAsync(interceptor);
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession source = await service.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            15);
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .Where(row => row.StudySessionId == source.Id)
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();
        interceptor.SourceSessionId = source.Id;
        interceptor.SetId = set.Id;
        interceptor.UserId = set.UserId;
        interceptor.Armed = true;

        QuizSessionAbandonedException exception = await Assert.ThrowsAsync<QuizSessionAbandonedException>(
            () => service.AnswerAsync(
                set.Id,
                source.Id,
                question.Id,
                question.CorrectChoiceIndex,
                set.UserId));

        database.Context.ChangeTracker.Clear();
        QuizSessionQuestion storedQuestion = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .SingleAsync(row => row.Id == question.Id);
        Assert.NotNull(exception.ActiveSessionId);
        Assert.True(exception.ActiveSessionId.Value > 0);
        Assert.Null(storedQuestion.SelectedChoiceIndex);
        Assert.Null(storedQuestion.IsCorrect);
    }

    [Fact]
    public async Task GetCurrentQuestion_Review_requested_answered_question_returns_read_only_review_navigation()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        List<QuizSessionQuestion> questions = await database.Context.QuizSessionQuestions
            .Where(question => question.StudySessionId == session.Id)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();

        await service.AnswerAsync(
            set.Id,
            session.Id,
            questions[0].Id,
            questions[0].CorrectChoiceIndex,
            set.UserId);
        await service.AnswerAsync(
            set.Id,
            session.Id,
            questions[1].Id,
            (questions[1].CorrectChoiceIndex + 1) % 4,
            set.UserId);

        QuizQuestionState state = await service.GetCurrentQuestionAsync(
            set.Id,
            session.Id,
            set.UserId,
            questions[0].Id);

        Assert.True(state.IsReviewOnly);
        Assert.Equal(questions[0].CorrectChoiceIndex, state.CorrectChoiceIndex);
        Assert.Equal(questions[0].CorrectChoiceIndex, state.SelectedChoiceIndex);
        Assert.Null(state.PreviousQuestionId);
        Assert.Equal(questions[1].Id, state.NextQuestionId);
        Assert.Equal(questions[2].Id, state.CurrentPendingQuestionId);
        Assert.Equal(questions[0].Id, state.Question!.Id);

        QuizSessionQuestion stored = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .SingleAsync(question => question.Id == questions[0].Id);
        Assert.Equal(questions[0].CorrectChoiceIndex, stored.SelectedChoiceIndex);
        Assert.True(stored.IsCorrect);
    }

    [Fact]
    public async Task GetCurrentQuestion_rejects_question_from_another_session()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession firstSession = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        int foreignQuestionId = await database.Context.QuizSessionQuestions
            .Where(question => question.StudySessionId == firstSession.Id)
            .OrderBy(question => question.OrderIndex)
            .Select(question => question.Id)
            .FirstAsync();
        StudySession secondSession = await service.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            QuizService.DefaultQuizMinutes);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetCurrentQuestionAsync(
            set.Id,
            secondSession.Id,
            set.UserId,
            foreignQuestionId));
    }

    [Fact]
    public async Task GetCurrentQuestion_expired_attempt_completes_pending_questions_as_wrong()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        var publisher = new RecordingStudyEventPublisher();
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(
            2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        QuizService service = CreateService(database.Context, publisher, timeProvider);
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        session.QuizStartedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        session.QuizTimeLimitSeconds = 60;
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        timeProvider.Advance(TimeSpan.FromSeconds(61));

        await service.CompleteExpiredAsync(
            set.Id,
            session.Id,
            set.UserId);
        await service.CompleteExpiredAsync(
            set.Id,
            session.Id,
            set.UserId);

        StudySession storedSession = await database.Context.StudySessions
            .AsNoTracking()
            .SingleAsync(row => row.Id == session.Id);
        List<QuizSessionQuestion> questions = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .Where(row => row.StudySessionId == session.Id)
            .ToListAsync();
        Assert.Equal(0, storedSession.Score);
        Assert.Equal(timeProvider.GetUtcNow().UtcDateTime, storedSession.CompletedAt);
        Assert.All(questions, question =>
        {
            Assert.Null(question.SelectedChoiceIndex);
            Assert.False(question.IsCorrect);
            Assert.NotNull(question.AnsweredAt);
        });
        QuizSessionResult result = await service.GetResultAsync(set.Id, session.Id, set.UserId);
        Assert.All(result.WrongAnswers, answer =>
            Assert.Equal("Chưa trả lời", answer.SelectedAnswer));
        Assert.Single(publisher.Events);
    }

    [Fact]
    public async Task GetCurrentQuestion_expired_attempt_completes_and_throws_expired_exception()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(
            2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        QuizService service = CreateService(
            database.Context,
            new RecordingStudyEventPublisher(),
            timeProvider);
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        session.QuizStartedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        session.QuizTimeLimitSeconds = 60;
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        timeProvider.Advance(TimeSpan.FromSeconds(61));

        await Assert.ThrowsAsync<QuizExpiredException>(() => service.GetCurrentQuestionAsync(
            set.Id,
            session.Id,
            set.UserId));

        Assert.Equal(0, await database.Context.StudySessions
            .Where(row => row.Id == session.Id)
            .Select(row => row.Score)
            .SingleAsync());
    }

    [Fact]
    public async Task Answer_expired_attempt_completes_before_accepting_the_answer()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(
            2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        QuizService service = CreateService(
            database.Context,
            new RecordingStudyEventPublisher(),
            timeProvider);
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();
        session.QuizStartedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        session.QuizTimeLimitSeconds = 60;
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        timeProvider.Advance(TimeSpan.FromSeconds(61));

        await Assert.ThrowsAsync<QuizExpiredException>(() => service.AnswerAsync(
            set.Id,
            session.Id,
            question.Id,
            question.CorrectChoiceIndex,
            set.UserId));

        QuizSessionQuestion storedQuestion = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .SingleAsync(row => row.Id == question.Id);
        Assert.Null(storedQuestion.SelectedChoiceIndex);
        Assert.False(storedQuestion.IsCorrect);
    }

    [Fact]
    public async Task Answer_crossing_deadline_before_its_transactional_write_completes_as_expired()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        DateTimeOffset startedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new SteppingTimeProvider(
            startedAt.AddSeconds(59),
            startedAt.AddSeconds(60));
        QuizService service = CreateService(
            database.Context,
            new RecordingStudyEventPublisher(),
            timeProvider);
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();
        session.QuizStartedAtUtc = startedAt.UtcDateTime;
        session.QuizTimeLimitSeconds = 60;
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<QuizExpiredException>(() => service.AnswerAsync(
            set.Id,
            session.Id,
            question.Id,
            question.CorrectChoiceIndex,
            set.UserId));

        QuizSessionQuestion storedQuestion = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .SingleAsync(row => row.Id == question.Id);
        Assert.Null(storedQuestion.SelectedChoiceIndex);
        Assert.False(storedQuestion.IsCorrect);
    }

    [Fact]
    public async Task GetCurrentQuestion_timed_attempt_exposes_server_deadline_and_remaining_seconds()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(
            2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        QuizService service = CreateService(
            database.Context,
            new RecordingStudyEventPublisher(),
            timeProvider);
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        session.QuizStartedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        session.QuizTimeLimitSeconds = 60;
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        timeProvider.Advance(TimeSpan.FromSeconds(17));

        QuizQuestionState state = await service.GetCurrentQuestionAsync(
            set.Id,
            session.Id,
            set.UserId);

        Assert.Equal(new DateTime(2026, 1, 1, 0, 1, 0, DateTimeKind.Utc), state.DeadlineUtc);
        Assert.Equal(DateTimeKind.Utc, state.DeadlineUtc!.Value.Kind);
        Assert.Equal(43, state.RemainingSeconds);
    }

    [Fact]
    public async Task GetCurrentQuestion_recovers_answered_orphan_and_publishes_completion_once()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        var publisher = new RecordingStudyEventPublisher();
        QuizService service = CreateService(database.Context, publisher);
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        await AnswerAllDirectlyAsync(database.Context, session.Id);

        QuizQuestionState state = await service.GetCurrentQuestionAsync(
            set.Id,
            session.Id,
            set.UserId);
        QuizSessionResult result = await service.GetResultAsync(
            set.Id,
            session.Id,
            set.UserId);

        Assert.True(state.IsComplete);
        Assert.Equal(100, result.Score);
        Assert.Single(publisher.Events.OfType<StudySessionCompletedEvent>());
        database.Context.ChangeTracker.Clear();
        StudySession stored = await database.Context.StudySessions.SingleAsync(row =>
            row.Id == session.Id);
        Assert.NotNull(stored.CompletedAt);
    }

    [Fact]
    public async Task StartOrResume_recovers_answered_orphan_instead_of_creating_replacement()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        var publisher = new RecordingStudyEventPublisher();
        QuizService service = CreateService(database.Context, publisher);
        StudySession orphan = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        await AnswerAllDirectlyAsync(database.Context, orphan.Id);

        StudySession recovered = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionResult result = await service.GetResultAsync(
            set.Id,
            recovered.Id,
            set.UserId);

        Assert.Equal(orphan.Id, recovered.Id);
        Assert.Equal(100, result.Score);
        Assert.Equal(1, await database.Context.StudySessions.CountAsync());
        Assert.Single(publisher.Events.OfType<StudySessionCompletedEvent>());
    }

    [Fact]
    public async Task Answer_saves_correct_result_and_does_not_create_progress()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();

        QuizAnswerResult result = await service.AnswerAsync(
            set.Id,
            session.Id,
            question.Id,
            question.CorrectChoiceIndex,
            set.UserId);

        database.Context.ChangeTracker.Clear();
        QuizSessionQuestion stored = await database.Context.QuizSessionQuestions
            .SingleAsync(row => row.Id == question.Id);
        Assert.True(result.IsCorrect);
        Assert.Equal(question.CorrectChoiceIndex, result.CorrectChoiceIndex);
        Assert.False(result.IsLastQuestion);
        Assert.Equal(question.CorrectChoiceIndex, stored.SelectedChoiceIndex);
        Assert.True(stored.IsCorrect);
        Assert.NotNull(stored.AnsweredAt);
        Assert.Empty(await database.Context.UserProgresses.ToListAsync());
    }

    [Fact]
    public async Task Answer_same_choice_is_idempotent()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();
        int selectedChoice = question.CorrectChoiceIndex == 0 ? 1 : 0;

        QuizAnswerResult first = await service.AnswerAsync(
            set.Id, session.Id, question.Id, selectedChoice, set.UserId);
        QuizAnswerResult repeated = await service.AnswerAsync(
            set.Id, session.Id, question.Id, selectedChoice, set.UserId);

        Assert.Equal(first, repeated);
        Assert.False(repeated.IsCorrect);
        Assert.Equal(question.CorrectChoiceIndex, repeated.CorrectChoiceIndex);
    }

    [Fact]
    public async Task Answer_different_choice_throws_conflict()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();
        int firstChoice = question.CorrectChoiceIndex;
        int differentChoice = firstChoice == 0 ? 1 : 0;
        await service.AnswerAsync(
            set.Id, session.Id, question.Id, firstChoice, set.UserId);

        await Assert.ThrowsAsync<QuizConflictException>(() => service.AnswerAsync(
            set.Id,
            session.Id,
            question.Id,
            differentChoice,
            set.UserId));
    }

    [Fact]
    public async Task Answer_rejects_question_from_another_session()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession firstSession = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();
        var anotherSession = new StudySession
        {
            FlashcardSetId = set.Id,
            UserId = set.UserId,
            Mode = StudyMode.Quiz,
            Score = 100,
            CompletedAt = DateTime.UtcNow
        };
        database.Context.StudySessions.Add(anotherSession);
        await database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AnswerAsync(
            set.Id,
            anotherSession.Id,
            question.Id,
            question.CorrectChoiceIndex,
            set.UserId));

        database.Context.ChangeTracker.Clear();
        Assert.Null((await database.Context.QuizSessionQuestions
            .SingleAsync(row => row.Id == question.Id)).SelectedChoiceIndex);
        Assert.NotEqual(firstSession.Id, anotherSession.Id);
    }

    [Fact]
    public async Task Answer_rejects_another_user()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AnswerAsync(
            set.Id,
            session.Id,
            question.Id,
            question.CorrectChoiceIndex,
            "another-user"));

        database.Context.ChangeTracker.Clear();
        Assert.Null((await database.Context.QuizSessionQuestions
            .SingleAsync(row => row.Id == question.Id)).SelectedChoiceIndex);
    }

    [Fact]
    public async Task Last_answer_calculates_score_and_publishes_once()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context, cardCount: 8);
        var publisher = new RecordingStudyEventPublisher();
        QuizService service = CreateService(database.Context, publisher);
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        List<QuizSessionQuestion> questions = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .OrderBy(row => row.OrderIndex)
            .ToListAsync();

        for (int index = 0; index < questions.Count - 1; index++)
        {
            QuizSessionQuestion question = questions[index];
            int selectedChoice = index == 0
                ? question.CorrectChoiceIndex
                : DifferentChoice(question.CorrectChoiceIndex);
            await service.AnswerAsync(
                set.Id, session.Id, question.Id, selectedChoice, set.UserId);
        }

        QuizSessionQuestion lastQuestion = questions[^1];
        int lastWrongChoice = DifferentChoice(lastQuestion.CorrectChoiceIndex);
        QuizAnswerResult result = await service.AnswerAsync(
            set.Id, session.Id, lastQuestion.Id, lastWrongChoice, set.UserId);
        QuizAnswerResult repeated = await service.AnswerAsync(
            set.Id, session.Id, lastQuestion.Id, lastWrongChoice, set.UserId);

        database.Context.ChangeTracker.Clear();
        StudySession storedSession = await database.Context.StudySessions
            .SingleAsync(row => row.Id == session.Id);
        Assert.True(result.IsLastQuestion);
        Assert.Equal(result, repeated);
        Assert.Equal(13, storedSession.Score);
        StudySessionCompletedEvent completion = Assert.Single(
            publisher.Events.OfType<StudySessionCompletedEvent>());
        Assert.Equal(StudyMode.Quiz, completion.Mode);
        Assert.Equal(13, completion.Score);
        Assert.Equal(set.Id, completion.SetId);
        Assert.Equal(session.Id, completion.SessionId);
        Assert.Equal(set.UserId, completion.UserId);
    }

    [Fact]
    public async Task Last_answer_rolls_back_answer_when_session_completion_update_fails()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        var publisher = new RecordingStudyEventPublisher();
        QuizService service = CreateService(database.Context, publisher);
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        List<QuizSessionQuestion> questions = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .OrderBy(row => row.OrderIndex)
            .ToListAsync();
        foreach (QuizSessionQuestion question in questions.Take(questions.Count - 1))
        {
            await service.AnswerAsync(
                set.Id,
                session.Id,
                question.Id,
                question.CorrectChoiceIndex,
                set.UserId);
        }

        await database.Context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER FailQuizCompletion
            BEFORE UPDATE OF Score ON StudySessions
            WHEN NEW.Score IS NOT NULL
            BEGIN
                SELECT RAISE(ABORT, 'completion failed');
            END;
            """);
        QuizSessionQuestion last = questions[^1];

        await Assert.ThrowsAsync<SqliteException>(() => service.AnswerAsync(
            set.Id,
            session.Id,
            last.Id,
            last.CorrectChoiceIndex,
            set.UserId));

        database.Context.ChangeTracker.Clear();
        Assert.Null((await database.Context.QuizSessionQuestions.SingleAsync(row =>
            row.Id == last.Id)).SelectedChoiceIndex);
        Assert.Null((await database.Context.StudySessions.SingleAsync(row =>
            row.Id == session.Id)).Score);
        Assert.Empty(publisher.Events);
    }

    [Fact]
    public async Task Last_answer_concurrent_requests_persist_score_and_publish_once()
    {
        await using var database = await SharedQuizTestDatabase.CreateAsync();
        var publisher = new RecordingStudyEventPublisher();
        int setId;
        int sessionId;
        int questionId;
        int selectedChoiceIndex;
        string userId;

        await using (AppDbContext setupContext = await database.CreateContextAsync())
        {
            FlashcardSet set = await SeedQuestionPoolAsync(setupContext);
            QuizService setupService = CreateService(setupContext, publisher);
            StudySession session = await setupService.StartOrResumeAsync(
                set.Id,
                set.UserId,
                new UserStudySettings());
            List<QuizSessionQuestion> questions = await setupContext.QuizSessionQuestions
                .AsNoTracking()
                .OrderBy(row => row.OrderIndex)
                .ToListAsync();
            foreach (QuizSessionQuestion question in questions.Take(questions.Count - 1))
            {
                await setupService.AnswerAsync(
                    set.Id,
                    session.Id,
                    question.Id,
                    question.CorrectChoiceIndex,
                    set.UserId);
            }

            QuizSessionQuestion lastQuestion = questions[^1];
            setId = set.Id;
            sessionId = session.Id;
            questionId = lastQuestion.Id;
            selectedChoiceIndex = lastQuestion.CorrectChoiceIndex;
            userId = set.UserId;
        }

        await using AppDbContext firstContext = await database.CreateContextAsync();
        await using AppDbContext secondContext = await database.CreateContextAsync();
        QuizService firstService = CreateService(firstContext, publisher);
        QuizService secondService = CreateService(secondContext, publisher);
        var bothReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int readyCount = 0;

        async Task<QuizAnswerResult> SubmitLastAnswerAsync(QuizService service)
        {
            if (Interlocked.Increment(ref readyCount) == 2)
            {
                bothReady.SetResult();
            }

            await startGate.Task;
            return await service.AnswerAsync(
                setId,
                sessionId,
                questionId,
                selectedChoiceIndex,
                userId);
        }

        Task<QuizAnswerResult> firstRequest = SubmitLastAnswerAsync(firstService);
        Task<QuizAnswerResult> secondRequest = SubmitLastAnswerAsync(secondService);
        await bothReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        startGate.SetResult();
        QuizAnswerResult[] results = await Task.WhenAll(firstRequest, secondRequest);

        await using AppDbContext verificationContext = await database.CreateContextAsync();
        QuizSessionQuestion storedQuestion = await verificationContext.QuizSessionQuestions
            .SingleAsync(row => row.Id == questionId);
        StudySession storedSession = await verificationContext.StudySessions
            .SingleAsync(row => row.Id == sessionId);
        Assert.All(results, result => Assert.True(result.IsLastQuestion));
        Assert.Equal(selectedChoiceIndex, storedQuestion.SelectedChoiceIndex);
        Assert.True(storedQuestion.IsCorrect);
        Assert.NotNull(storedQuestion.AnsweredAt);
        Assert.Equal(4, await verificationContext.QuizSessionQuestions.CountAsync(row =>
            row.StudySessionId == sessionId && row.SelectedChoiceIndex != null));
        Assert.Equal(100, storedSession.Score);
        Assert.Single(publisher.Events.OfType<StudySessionCompletedEvent>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public async Task Answer_rejects_choice_index_outside_range(int selectedChoiceIndex)
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.AnswerAsync(
            set.Id,
            session.Id,
            question.Id,
            selectedChoiceIndex,
            set.UserId));
    }

    [Fact]
    public async Task GetResult_returns_score_and_wrong_answer_snapshots()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        List<QuizSessionQuestion> questions = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        QuizSessionQuestion wrongQuestion = questions[0];
        int wrongChoiceIndex = DifferentChoice(wrongQuestion.CorrectChoiceIndex);

        foreach (QuizSessionQuestion question in questions)
        {
            int selectedChoiceIndex = question.Id == wrongQuestion.Id
                ? wrongChoiceIndex
                : question.CorrectChoiceIndex;
            await service.AnswerAsync(
                set.Id,
                session.Id,
                question.Id,
                selectedChoiceIndex,
                set.UserId);
        }

        Flashcard currentCard = await database.Context.Flashcards
            .SingleAsync(card => card.Id == wrongQuestion.FlashcardId);
        currentCard.FrontText = "mutated current term";
        currentCard.BackText = "mutated current definition";
        await database.Context.SaveChangesAsync();

        QuizSessionResult result = await service.GetResultAsync(
            set.Id,
            session.Id,
            set.UserId);

        Assert.Equal(session.Id, result.SessionId);
        Assert.Equal(set.Id, result.SetId);
        Assert.Equal(set.Title, result.SetTitle);
        Assert.Equal(4, result.TotalQuestions);
        Assert.Equal(3, result.CorrectCount);
        Assert.Equal(75, result.Score);
        QuizWrongAnswer wrongAnswer = Assert.Single(result.WrongAnswers);
        Assert.Equal(wrongQuestion.FlashcardId, wrongAnswer.FlashcardId);
        Assert.Equal(wrongQuestion.Direction, wrongAnswer.Direction);
        Assert.Equal(wrongQuestion.PromptText, wrongAnswer.PromptText);
        Assert.Equal(wrongQuestion.Choices[wrongChoiceIndex], wrongAnswer.SelectedAnswer);
        Assert.Equal(
            wrongQuestion.Choices[wrongQuestion.CorrectChoiceIndex],
            wrongAnswer.CorrectAnswer);
        Assert.DoesNotContain("mutated current", wrongAnswer.PromptText);
        Assert.DoesNotContain("mutated current", wrongAnswer.CorrectAnswer);
    }

    [Fact]
    public async Task GetResult_rejects_incomplete_session()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());

        await Assert.ThrowsAsync<QuizConflictException>(() => service.GetResultAsync(
            set.Id,
            session.Id,
            set.UserId));
    }

    [Fact]
    public async Task GetResult_rejects_another_user()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetResultAsync(
            set.Id,
            session.Id,
            "another-user"));
    }

    [Fact]
    public async Task RetryWrong_contains_only_wrong_cards_and_preserves_directions()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession sourceSession = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        List<QuizSessionQuestion> sourceQuestions = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sourceSession.Id)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        int[] wrongCardIds = { sourceQuestions[0].FlashcardId, sourceQuestions[2].FlashcardId };

        foreach (QuizSessionQuestion question in sourceQuestions)
        {
            int selectedChoiceIndex = wrongCardIds.Contains(question.FlashcardId)
                ? DifferentChoice(question.CorrectChoiceIndex)
                : question.CorrectChoiceIndex;
            await service.AnswerAsync(
                set.Id,
                sourceSession.Id,
                question.Id,
                selectedChoiceIndex,
                set.UserId);
        }

        database.Context.ChangeTracker.Clear();
        StudySession sourceBefore = await database.Context.StudySessions
            .AsNoTracking()
            .SingleAsync(session => session.Id == sourceSession.Id);
        StoredQuestionSnapshot[] questionsBefore = await LoadQuestionSnapshotsAsync(
            database.Context,
            sourceSession.Id);
        Dictionary<int, QuizQuestionDirection> expectedDirections = sourceQuestions
            .Where(question => wrongCardIds.Contains(question.FlashcardId))
            .ToDictionary(question => question.FlashcardId, question => question.Direction);

        StudySession retrySession = await service.RetryWrongAsync(
            set.Id,
            sourceSession.Id,
            set.UserId);

        database.Context.ChangeTracker.Clear();
        List<QuizSessionQuestion> retryQuestions = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == retrySession.Id)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        Assert.NotEqual(sourceSession.Id, retrySession.Id);
        Assert.Null(retrySession.Score);
        Assert.Null(retrySession.QuizStartedAtUtc);
        Assert.Null(retrySession.QuizTimeLimitSeconds);
        Assert.Equal(wrongCardIds.Order(), retryQuestions.Select(question => question.FlashcardId).Order());
        Assert.All(retryQuestions, question =>
            Assert.Equal(expectedDirections[question.FlashcardId], question.Direction));
        Assert.All(retryQuestions, question =>
        {
            Assert.Null(question.SelectedChoiceIndex);
            Assert.Null(question.IsCorrect);
            Assert.Null(question.AnsweredAt);
        });
        await AssertSourceUnchangedAsync(
            database.Context,
            sourceBefore,
            questionsBefore);
    }

    [Fact]
    public async Task RetryWrong_with_no_wrong_answers_throws_conflict()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession sourceSession = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        await CompleteQuizAsync(database.Context, service, set, sourceSession);

        await Assert.ThrowsAsync<QuizConflictException>(() => service.RetryWrongAsync(
            set.Id,
            sourceSession.Id,
            set.UserId));

        Assert.Equal(1, await database.Context.StudySessions.CountAsync());
    }

    [Fact]
    public async Task RetryAll_preserves_original_card_scope_and_redistributes_directions()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession sourceSession = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        await CompleteQuizAsync(database.Context, service, set, sourceSession);
        await database.Context.QuizSessionQuestions
            .Where(question => question.StudySessionId == sourceSession.Id)
            .ExecuteUpdateAsync(updates => updates.SetProperty(
                question => question.Direction,
                QuizQuestionDirection.TermToDefinition));
        database.Context.ChangeTracker.Clear();
        StudySession sourceBefore = await database.Context.StudySessions
            .AsNoTracking()
            .SingleAsync(session => session.Id == sourceSession.Id);
        StoredQuestionSnapshot[] questionsBefore = await LoadQuestionSnapshotsAsync(
            database.Context,
            sourceSession.Id);

        StudySession retrySession = await service.RetryAllAsync(
            set.Id,
            sourceSession.Id,
            set.UserId);

        database.Context.ChangeTracker.Clear();
        List<QuizSessionQuestion> retryQuestions = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == retrySession.Id)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        Assert.Equal(
            questionsBefore.Select(question => question.FlashcardId).Order(),
            retryQuestions.Select(question => question.FlashcardId).Order());
        Assert.Null(retrySession.QuizStartedAtUtc);
        Assert.Null(retrySession.QuizTimeLimitSeconds);
        Assert.Equal(2, retryQuestions.Count(question =>
            question.Direction == QuizQuestionDirection.TermToDefinition));
        Assert.Equal(2, retryQuestions.Count(question =>
            question.Direction == QuizQuestionDirection.DefinitionToTerm));
        await AssertSourceUnchangedAsync(
            database.Context,
            sourceBefore,
            questionsBefore);
    }

    [Theory]
    [InlineData(true, 15 * 60)]
    [InlineData(false, null)]
    public async Task Restart_abandons_active_attempt_reuses_scope_and_starts_fresh_timer(
        bool sourceIsTimed,
        int? expectedTimeLimitSeconds)
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(
            2026, 7, 19, 8, 0, 0, TimeSpan.Zero));
        QuizService service = CreateService(
            database.Context,
            new RecordingStudyEventPublisher(),
            timeProvider);
        StudySession sourceSession = sourceIsTimed
            ? await service.StartNewAsync(set.Id, set.UserId, new UserStudySettings(), 15)
            : await service.StartOrResumeAsync(set.Id, set.UserId, new UserStudySettings());
        List<QuizSessionQuestion> questions = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sourceSession.Id)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        await service.AnswerAsync(
            set.Id,
            sourceSession.Id,
            questions[0].Id,
            questions[0].CorrectChoiceIndex,
            set.UserId);
        timeProvider.Advance(TimeSpan.FromMinutes(2));

        StudySession restarted = await service.RestartAsync(
            set.Id,
            sourceSession.Id,
            set.UserId);

        database.Context.ChangeTracker.Clear();
        StudySession storedSource = await database.Context.StudySessions
            .AsNoTracking()
            .SingleAsync(session => session.Id == sourceSession.Id);
        List<QuizSessionQuestion> restartedQuestions = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == restarted.Id)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        Assert.NotEqual(sourceSession.Id, restarted.Id);
        Assert.NotNull(storedSource.CompletedAt);
        Assert.Null(storedSource.Score);
        Assert.Equal(expectedTimeLimitSeconds, restarted.QuizTimeLimitSeconds);
        if (expectedTimeLimitSeconds.HasValue)
        {
            Assert.Equal(timeProvider.GetUtcNow().UtcDateTime, restarted.QuizStartedAtUtc);
        }
        else
        {
            Assert.Null(restarted.QuizStartedAtUtc);
        }
        Assert.Equal(
            questions.Select(question => question.FlashcardId).Order(),
            restartedQuestions.Select(question => question.FlashcardId).Order());
        Assert.All(restartedQuestions, question =>
        {
            Assert.Null(question.SelectedChoiceIndex);
            Assert.Null(question.IsCorrect);
            Assert.Null(question.AnsweredAt);
        });
    }

    [Fact]
    public async Task Retry_switch_from_wrong_to_all_replaces_active_attempt_with_full_scope()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(
            2026, 7, 19, 8, 0, 0, TimeSpan.Zero));
        QuizService service = CreateService(
            database.Context,
            new RecordingStudyEventPublisher(),
            timeProvider);
        StudySession sourceSession = await service.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            15);
        List<QuizSessionQuestion> sourceQuestions = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sourceSession.Id)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        foreach (QuizSessionQuestion question in sourceQuestions)
        {
            await service.AnswerAsync(
                set.Id,
                sourceSession.Id,
                question.Id,
                question == sourceQuestions[0]
                    ? DifferentChoice(question.CorrectChoiceIndex)
                    : question.CorrectChoiceIndex,
                set.UserId);
        }

        StudySession wrongOnly = await service.RetryWrongAsync(
            set.Id,
            sourceSession.Id,
            set.UserId);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        StudySession retryAll = await service.RetryAllAsync(
            set.Id,
            sourceSession.Id,
            set.UserId);

        database.Context.ChangeTracker.Clear();
        StudySession storedWrongOnly = await database.Context.StudySessions
            .AsNoTracking()
            .SingleAsync(session => session.Id == wrongOnly.Id);
        List<QuizSessionQuestion> retryAllQuestions = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == retryAll.Id)
            .ToListAsync();
        Assert.NotEqual(wrongOnly.Id, retryAll.Id);
        Assert.NotNull(storedWrongOnly.CompletedAt);
        Assert.Equal(sourceQuestions.Count, retryAllQuestions.Count);
        Assert.Equal(15 * 60, retryAll.QuizTimeLimitSeconds);
        Assert.Equal(timeProvider.GetUtcNow().UtcDateTime, retryAll.QuizStartedAtUtc);
    }

    [Fact]
    public async Task Retry_all_after_every_answer_is_wrong_does_not_reuse_wrong_only_attempt()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession source = await service.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            15);
        List<QuizSessionQuestion> sourceQuestions = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .Where(row => row.StudySessionId == source.Id)
            .OrderBy(row => row.OrderIndex)
            .ToListAsync();
        foreach (QuizSessionQuestion question in sourceQuestions)
        {
            await service.AnswerAsync(
                set.Id,
                source.Id,
                question.Id,
                DifferentChoice(question.CorrectChoiceIndex),
                set.UserId);
        }

        StudySession wrongOnly = await service.RetryWrongAsync(set.Id, source.Id, set.UserId);
        StudySession retryAll = await service.RetryAllAsync(set.Id, source.Id, set.UserId);

        database.Context.ChangeTracker.Clear();
        StudySession storedWrongOnly = await database.Context.StudySessions
            .AsNoTracking()
            .SingleAsync(row => row.Id == wrongOnly.Id);
        List<QuizSessionQuestion> retryAllQuestions = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .Where(row => row.StudySessionId == retryAll.Id)
            .ToListAsync();
        Assert.NotEqual(wrongOnly.Id, retryAll.Id);
        Assert.NotNull(storedWrongOnly.CompletedAt);
        Assert.Equal(sourceQuestions.Count, retryAllQuestions.Count);
        Assert.Equal(source.QuizTimeLimitSeconds, retryAll.QuizTimeLimitSeconds);
    }

    [Fact]
    public async Task Retry_identity_requires_matching_source_and_time_limit()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession firstSource = await service.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            5);
        await CompleteQuizAsync(database.Context, service, set, firstSource);
        StudySession secondSource = await service.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            15);
        await CompleteQuizAsync(database.Context, service, set, secondSource);

        StudySession firstRetry = await service.RetryAllAsync(set.Id, firstSource.Id, set.UserId);
        StudySession secondRetry = await service.RetryAllAsync(set.Id, secondSource.Id, set.UserId);

        Assert.NotEqual(firstRetry.Id, secondRetry.Id);
        Assert.Equal(5 * 60, firstRetry.QuizTimeLimitSeconds);
        Assert.Equal(15 * 60, secondRetry.QuizTimeLimitSeconds);
        Assert.NotNull(await database.Context.StudySessions
            .AsNoTracking()
            .Where(row => row.Id == firstRetry.Id)
            .Select(row => row.CompletedAt)
            .SingleAsync());
    }

    [Fact]
    public async Task Retry_identity_replaces_same_source_retry_with_mismatched_time_limit()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession source = await service.StartNewAsync(
            set.Id,
            set.UserId,
            new UserStudySettings(),
            15);
        await CompleteQuizAsync(database.Context, service, set, source);
        StudySession mismatchedRetry = await service.RetryAllAsync(set.Id, source.Id, set.UserId);
        await database.Context.StudySessions
            .Where(row => row.Id == mismatchedRetry.Id)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(row => row.QuizTimeLimitSeconds, 5 * 60));

        StudySession replacement = await service.RetryAllAsync(set.Id, source.Id, set.UserId);

        Assert.NotEqual(mismatchedRetry.Id, replacement.Id);
        Assert.Equal(15 * 60, replacement.QuizTimeLimitSeconds);
        Assert.NotNull(await database.Context.StudySessions
            .AsNoTracking()
            .Where(row => row.Id == mismatchedRetry.Id)
            .Select(row => row.CompletedAt)
            .SingleAsync());
    }

    [Fact]
    public async Task RetryAll_concurrent_requests_return_one_active_quiz_session()
    {
        await using var database = await SharedQuizTestDatabase.CreateAsync();
        int setId;
        int sourceSessionId;
        string userId;
        await using (AppDbContext setupContext = await database.CreateContextAsync())
        {
            FlashcardSet set = await SeedQuestionPoolAsync(setupContext);
            QuizService setupService = CreateService(
                setupContext,
                new RecordingStudyEventPublisher());
            StudySession source = await setupService.StartOrResumeAsync(
                set.Id,
                set.UserId,
                new UserStudySettings());
            await CompleteQuizAsync(setupContext, setupService, set, source);
            setId = set.Id;
            sourceSessionId = source.Id;
            userId = set.UserId;
        }

        await using AppDbContext firstContext = await database.CreateContextAsync();
        await using AppDbContext secondContext = await database.CreateContextAsync();
        QuizService firstService = CreateService(
            firstContext,
            new RecordingStudyEventPublisher());
        QuizService secondService = CreateService(
            secondContext,
            new RecordingStudyEventPublisher());
        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<StudySession> RetryAsync(QuizService service)
        {
            await startGate.Task;
            return await service.RetryAllAsync(setId, sourceSessionId, userId);
        }

        Task<StudySession> firstRequest = RetryAsync(firstService);
        Task<StudySession> secondRequest = RetryAsync(secondService);
        startGate.SetResult();
        StudySession[] sessions = await Task.WhenAll(firstRequest, secondRequest);

        Assert.Equal(sessions[0].Id, sessions[1].Id);
        await using AppDbContext verificationContext = await database.CreateContextAsync();
        Assert.Equal(1, await verificationContext.StudySessions.CountAsync(row =>
            row.FlashcardSetId == setId
            && row.UserId == userId
            && row.Mode == StudyMode.Quiz
            && row.Score == null));
    }

    [Fact]
    public async Task RetryAll_does_not_swallow_unrelated_persistence_failure()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession source = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        await CompleteQuizAsync(database.Context, service, set, source);
        await database.Context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER FailRetryInsert
            BEFORE INSERT ON StudySessions
            WHEN NEW.Mode = 1
            BEGIN
                SELECT RAISE(ABORT, 'unrelated retry persistence failure');
            END;
            """);

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.RetryAllAsync(set.Id, source.Id, set.UserId));

        Assert.Contains(
            "unrelated retry persistence failure",
            exception.InnerException?.Message);
        Assert.Equal(1, await database.Context.StudySessions.CountAsync());
    }

    [Fact]
    public async Task Retry_rolls_back_when_source_card_is_unavailable()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession sourceSession = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        await CompleteQuizAsync(database.Context, service, set, sourceSession);
        int missingCardId = await database.Context.QuizSessionQuestions
            .Where(question => question.StudySessionId == sourceSession.Id)
            .Select(question => question.FlashcardId)
            .FirstAsync();
        await database.Context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        await database.Context.Flashcards
            .Where(card => card.Id == missingCardId)
            .ExecuteDeleteAsync();
        await database.Context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
        database.Context.ChangeTracker.Clear();
        StudySession sourceBefore = await database.Context.StudySessions
            .AsNoTracking()
            .SingleAsync(session => session.Id == sourceSession.Id);
        StoredQuestionSnapshot[] questionsBefore = await LoadQuestionSnapshotsAsync(
            database.Context,
            sourceSession.Id);

        await Assert.ThrowsAsync<QuizUnavailableException>(() => service.RetryAllAsync(
            set.Id,
            sourceSession.Id,
            set.UserId));

        Assert.Equal(1, await database.Context.StudySessions.CountAsync());
        await AssertSourceUnchangedAsync(
            database.Context,
            sourceBefore,
            questionsBefore);
    }

    [Fact]
    public async Task Retry_holds_source_cards_in_a_serializable_transaction()
    {
        var interceptor = new RecordingTransactionInterceptor();
        await using var database = await QuizTestDatabase.CreateAsync(interceptor);
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession source = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        await CompleteQuizAsync(database.Context, service, set, source);
        interceptor.Armed = true;

        await service.RetryAllAsync(set.Id, source.Id, set.UserId);

        Assert.Equal(IsolationLevel.Serializable, interceptor.IsolationLevel);
    }

    [Fact]
    public async Task Retry_rolls_back_when_answer_pool_is_unavailable()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession sourceSession = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        await CompleteQuizAsync(database.Context, service, set, sourceSession);
        List<Flashcard> cards = await database.Context.Flashcards
            .Where(card => card.FlashcardSetId == set.Id)
            .ToListAsync();
        foreach (Flashcard card in cards)
        {
            card.FrontText = "same term";
            card.BackText = "same definition";
        }
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        StudySession sourceBefore = await database.Context.StudySessions
            .AsNoTracking()
            .SingleAsync(session => session.Id == sourceSession.Id);
        StoredQuestionSnapshot[] questionsBefore = await LoadQuestionSnapshotsAsync(
            database.Context,
            sourceSession.Id);

        await Assert.ThrowsAsync<QuizUnavailableException>(() => service.RetryAllAsync(
            set.Id,
            sourceSession.Id,
            set.UserId));

        Assert.Equal(1, await database.Context.StudySessions.CountAsync());
        await AssertSourceUnchangedAsync(
            database.Context,
            sourceBefore,
            questionsBefore);
    }

    [Fact]
    public async Task Retry_rejects_another_user()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession sourceSession = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RetryWrongAsync(
            set.Id,
            sourceSession.Id,
            "another-user"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RetryAllAsync(
            set.Id,
            sourceSession.Id,
            "another-user"));
        Assert.Equal(1, await database.Context.StudySessions.CountAsync());
    }

    [Fact]
    public async Task Quiz_session_model_persists_timing_and_only_incomplete_rows_are_active()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        IEntityType entity = database.Context.Model.FindEntityType(typeof(StudySession))!;

        Assert.NotNull(entity.FindProperty(nameof(StudySession.QuizStartedAtUtc)));
        Assert.NotNull(entity.FindProperty(nameof(StudySession.QuizTimeLimitSeconds)));
        IIndex activeIndex = entity.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "UserId", "FlashcardSetId", "Mode" }));
        Assert.Equal(
            "[Mode] = 1 AND [Score] IS NULL AND [CompletedAt] IS NULL",
            activeIndex.GetFilter());
    }

    [Fact]
    public async Task Schema_DuplicateOrderIndexWithinSession_ThrowsDbUpdateException()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        var (session, firstCard, secondCard) = await SeedQuizDataAsync(database.Context);

        database.Context.QuizSessionQuestions.AddRange(
            CreateQuestion(session.Id, firstCard.Id, orderIndex: 0),
            CreateQuestion(session.Id, secondCard.Id, orderIndex: 0));

        await Assert.ThrowsAsync<DbUpdateException>(() => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Schema_DuplicateFlashcardWithinSession_ThrowsDbUpdateException()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        var (session, firstCard, _) = await SeedQuizDataAsync(database.Context);

        database.Context.QuizSessionQuestions.AddRange(
            CreateQuestion(session.Id, firstCard.Id, orderIndex: 0),
            CreateQuestion(session.Id, firstCard.Id, orderIndex: 1));

        await Assert.ThrowsAsync<DbUpdateException>(() => database.Context.SaveChangesAsync());
    }

    private static async Task<(StudySession Session, Flashcard FirstCard, Flashcard SecondCard)>
        SeedQuizDataAsync(AppDbContext context)
    {
        var set = new FlashcardSet
        {
            Title = "Quiz schema set",
            UserId = "quiz-user"
        };
        var firstCard = CreateCard(set, "term one", "definition one", orderIndex: 0);
        var secondCard = CreateCard(set, "term two", "definition two", orderIndex: 1);
        var session = new StudySession
        {
            UserId = "quiz-user",
            FlashcardSet = set,
            Mode = StudyMode.Quiz
        };

        context.AddRange(set, firstCard, secondCard, session);
        await context.SaveChangesAsync();
        return (session, firstCard, secondCard);
    }

    private static Flashcard CreateCard(
        FlashcardSet set,
        string term,
        string definition,
        int orderIndex) => new()
    {
        FlashcardSet = set,
        FrontText = term,
        BackText = definition,
        Pronunciation = "/test/",
        PartOfSpeech = "noun",
        ExampleSentence = $"Example for {term}.",
        ExampleMeaning = $"Meaning for {term}.",
        OrderIndex = orderIndex
    };

    private static QuizSessionQuestion CreateQuestion(
        int sessionId,
        int flashcardId,
        int orderIndex) => new()
    {
        StudySessionId = sessionId,
        FlashcardId = flashcardId,
        OrderIndex = orderIndex,
        Direction = QuizQuestionDirection.TermToDefinition,
        PromptText = "prompt",
        Choice1Text = "correct",
        Choice2Text = "choice two",
        Choice3Text = "choice three",
        Choice4Text = "choice four",
        CorrectChoiceIndex = 0
    };

    private static async Task CompleteQuizAsync(
        AppDbContext context,
        QuizService service,
        FlashcardSet set,
        StudySession session)
    {
        List<QuizSessionQuestion> questions = await context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == session.Id)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        foreach (QuizSessionQuestion question in questions)
        {
            await service.AnswerAsync(
                set.Id,
                session.Id,
                question.Id,
                question.CorrectChoiceIndex,
                set.UserId);
        }
    }

    private static async Task AnswerAllDirectlyAsync(AppDbContext context, int sessionId)
    {
        await context.QuizSessionQuestions
            .Where(question => question.StudySessionId == sessionId)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(question => question.SelectedChoiceIndex, question =>
                    question.CorrectChoiceIndex)
                .SetProperty(question => question.IsCorrect, true)
                .SetProperty(question => question.AnsweredAt, DateTime.UtcNow));
        context.ChangeTracker.Clear();
    }

    private static async Task<StoredQuestionSnapshot[]> LoadQuestionSnapshotsAsync(
        AppDbContext context,
        int sessionId) => await context.QuizSessionQuestions
        .AsNoTracking()
        .Where(question => question.StudySessionId == sessionId)
        .OrderBy(question => question.OrderIndex)
        .Select(question => new StoredQuestionSnapshot(
            question.Id,
            question.StudySessionId,
            question.FlashcardId,
            question.OrderIndex,
            question.Direction,
            question.PromptText,
            question.Choice1Text,
            question.Choice2Text,
            question.Choice3Text,
            question.Choice4Text,
            question.CorrectChoiceIndex,
            question.SelectedChoiceIndex,
            question.IsCorrect,
            question.AnsweredAt))
        .ToArrayAsync();

    private static async Task AssertSourceUnchangedAsync(
        AppDbContext context,
        StudySession expectedSession,
        StoredQuestionSnapshot[] expectedQuestions)
    {
        StudySession storedSession = await context.StudySessions
            .AsNoTracking()
            .SingleAsync(session => session.Id == expectedSession.Id);
        Assert.Equal(expectedSession.Score, storedSession.Score);
        Assert.Equal(expectedSession.CompletedAt, storedSession.CompletedAt);
        Assert.Equal(
            expectedQuestions,
            await LoadQuestionSnapshotsAsync(context, expectedSession.Id));
    }

    private sealed record StoredQuestionSnapshot(
        int Id,
        int StudySessionId,
        int FlashcardId,
        int OrderIndex,
        QuizQuestionDirection Direction,
        string PromptText,
        string Choice1Text,
        string Choice2Text,
        string Choice3Text,
        string Choice4Text,
        int CorrectChoiceIndex,
        int? SelectedChoiceIndex,
        bool? IsCorrect,
        DateTime? AnsweredAt);

    private static int ReplaceActiveSession(
        AppDbContext context,
        StudySession source,
        DateTimeOffset replacedAt)
    {
        using DbCommand command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            """
            UPDATE StudySessions
            SET CompletedAt = $completedAt
            WHERE Id = $sourceSessionId;

            INSERT INTO StudySessions
                (UserId, FlashcardSetId, Mode, DictationContentMode, Score, CompletedAt,
                 QuizStartedAtUtc, QuizTimeLimitSeconds, QuizRetryKind, QuizRetrySourceSessionId)
            VALUES
                ($userId, $setId, 1, 0, NULL, NULL, $startedAt, 900, NULL, NULL);

            SELECT last_insert_rowid();
            """;
        DbParameter completedAt = command.CreateParameter();
        completedAt.ParameterName = "$completedAt";
        completedAt.Value = replacedAt.UtcDateTime;
        command.Parameters.Add(completedAt);
        DbParameter sourceSessionId = command.CreateParameter();
        sourceSessionId.ParameterName = "$sourceSessionId";
        sourceSessionId.Value = source.Id;
        command.Parameters.Add(sourceSessionId);
        DbParameter userId = command.CreateParameter();
        userId.ParameterName = "$userId";
        userId.Value = source.UserId;
        command.Parameters.Add(userId);
        DbParameter setId = command.CreateParameter();
        setId.ParameterName = "$setId";
        setId.Value = source.FlashcardSetId;
        command.Parameters.Add(setId);
        DbParameter startedAt = command.CreateParameter();
        startedAt.ParameterName = "$startedAt";
        startedAt.Value = replacedAt.UtcDateTime;
        command.Parameters.Add(startedAt);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan elapsed) => _utcNow += elapsed;
    }

    private sealed class CallbackTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        private Action? _callback;

        public CallbackTimeProvider(DateTimeOffset utcNow, Action callback)
        {
            _utcNow = utcNow;
            _callback = callback;
        }

        public override DateTimeOffset GetUtcNow()
        {
            Interlocked.Exchange(ref _callback, null)?.Invoke();
            return _utcNow;
        }
    }

    private sealed class SteppingTimeProvider : TimeProvider
    {
        private readonly Queue<DateTimeOffset> _times;
        private DateTimeOffset _last;

        public SteppingTimeProvider(params DateTimeOffset[] times)
        {
            _times = new Queue<DateTimeOffset>(times);
            _last = times[^1];
        }

        public override DateTimeOffset GetUtcNow() => _times.Count > 0
            ? _last = _times.Dequeue()
            : _last;
    }

    private static QuizService CreateService(
        AppDbContext context,
        RecordingStudyEventPublisher publisher,
        TimeProvider? timeProvider = null)
    {
        var questionFactory = new QuizQuestionFactory(context);
        var strategy = new QuizModeStrategy(
            new StudyCardQueryService(context),
            questionFactory);
        var resolver = new StudyModeStrategyResolver(new[] { strategy });
        return new QuizService(
            context,
            resolver,
            questionFactory,
            publisher,
            timeProvider ?? TimeProvider.System);
    }

    private static int DifferentChoice(int correctChoiceIndex) =>
        correctChoiceIndex == 0 ? 1 : 0;

    private static async Task<FlashcardSet> SeedQuestionPoolAsync(
        AppDbContext context,
        int cardCount = 4)
    {
        var set = new FlashcardSet
        {
            Title = "Quiz lifecycle set",
            UserId = "quiz-user"
        };

        for (int index = 0; index < cardCount; index++)
        {
            set.Flashcards.Add(CreateCard(
                set,
                $"term {index}",
                $"definition {index}",
                index));
        }

        context.FlashcardSets.Add(set);
        await context.SaveChangesAsync();
        return set;
    }

    private sealed class RecordingStudyEventPublisher : IStudyEventPublisher
    {
        public ConcurrentQueue<StudyEvent> Events { get; } = new();

        public Task PublishAsync(
            StudyEvent studyEvent,
            CancellationToken cancellationToken = default)
        {
            Events.Enqueue(studyEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTransactionInterceptor : DbTransactionInterceptor
    {
        public bool Armed { get; set; }
        public IsolationLevel? IsolationLevel { get; private set; }

        public override ValueTask<DbTransaction> TransactionStartedAsync(
            DbConnection connection,
            TransactionEndEventData eventData,
            DbTransaction result,
            CancellationToken cancellationToken = default)
        {
            if (Armed)
            {
                IsolationLevel = result.IsolationLevel;
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class ReplaceSessionBeforeAnswerInterceptor : DbCommandInterceptor
    {
        public int SourceSessionId { get; set; }
        public int SetId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public bool Armed { get; set; }
        private int _studySessionReads;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            MaybeReplaceBeforeSecondSessionRead(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            MaybeReplaceBeforeSecondSessionRead(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void MaybeReplaceBeforeSecondSessionRead(DbCommand command)
        {
            if (!Armed
                || SourceSessionId == 0
                || !command.CommandText.Contains("StudySessions", StringComparison.Ordinal)
                || !command.CommandText.Contains("SELECT", StringComparison.OrdinalIgnoreCase)
                || command.CommandText.Contains("QuizSessionQuestions", StringComparison.Ordinal))
            {
                return;
            }

            if (++_studySessionReads != 2) return;
            using DbCommand abandon = command.Connection!.CreateCommand();
            abandon.Transaction = command.Transaction;
            abandon.CommandText = $"UPDATE StudySessions SET CompletedAt = '2026-07-19T08:01:00' WHERE Id = {SourceSessionId};";
            abandon.ExecuteNonQuery();

            using DbCommand replacement = command.Connection.CreateCommand();
            replacement.Transaction = command.Transaction;
            replacement.CommandText = $"INSERT INTO StudySessions (UserId, FlashcardSetId, Mode, DictationContentMode, Score, CompletedAt, QuizStartedAtUtc, QuizTimeLimitSeconds, QuizRetryKind, QuizRetrySourceSessionId) VALUES ('{UserId}', {SetId}, 1, 0, NULL, NULL, '2026-07-19T08:01:00', 900, NULL, NULL);";
            replacement.ExecuteNonQuery();
            Armed = false;
        }

    }

    private sealed class SharedQuizTestDatabase : IAsyncDisposable
    {
        private readonly string _databasePath;
        private readonly string _connectionString;

        private SharedQuizTestDatabase(string databasePath)
        {
            _databasePath = databasePath;
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
                DefaultTimeout = 10
            }.ToString();
        }

        public static async Task<SharedQuizTestDatabase> CreateAsync()
        {
            string databasePath = Path.Combine(
                Path.GetTempPath(),
                $"ltwnc-quiz-{Guid.NewGuid():N}.db");
            var database = new SharedQuizTestDatabase(databasePath);
            await using AppDbContext context = await database.CreateContextAsync();
            await context.Database.EnsureCreatedAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            return database;
        }

        public async Task<AppDbContext> CreateContextAsync()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connectionString)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.OpenConnectionAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=10000;");
            return context;
        }

        public ValueTask DisposeAsync()
        {
            File.Delete(_databasePath);
            File.Delete($"{_databasePath}-wal");
            File.Delete($"{_databasePath}-shm");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class QuizTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private QuizTestDatabase(SqliteConnection connection, AppDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public AppDbContext Context { get; }

        public static async Task<QuizTestDatabase> CreateAsync(
            params IInterceptor[] interceptors)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(interceptors)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            return new QuizTestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
