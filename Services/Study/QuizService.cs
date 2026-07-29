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
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_strategyResolver` để các phương thức khác sử dụng.
        _strategyResolver = strategyResolver;
        // 3. Lưu dependency `_questionFactory` để các phương thức khác sử dụng.
        _questionFactory = questionFactory;
        // 4. Lưu dependency `_studyEvents` để các phương thức khác sử dụng.
        _studyEvents = studyEvents;
        // 5. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<QuizSetupState> GetSetupAsync(int setId, string userId)
    {
        // 1. Gọi `GetOwnedSetAsync` và lưu kết quả vào `set`.
        FlashcardSet set = await GetOwnedSetAsync(setId, userId);
        // 2. Tạo và trả dữ liệu cần thiết cho màn thiết lập.
        return new QuizSetupState
        {
            SetId = set.Id,
            SetTitle = set.Title
        };
    }

    public async Task<StudySession> StartNewAsync(
        int setId,
        string userId,
        UserStudySettings settings,
        int? timeLimitMinutes)
    {
        // 1. Kiểm tra `timeLimitMinutes.HasValue && timeLimitMinutes is < MinimumQuizMinut...` để chọn nhánh xử lý phù hợp.
        if (timeLimitMinutes.HasValue
            && timeLimitMinutes is < MinimumQuizMinutes or > MaximumQuizMinutes)
        {
            // 2. Dừng xử lý và phát sinh lỗi `new ArgumentOutOfRangeException(nameof(timeLimitMinutes))`.
            throw new ArgumentOutOfRangeException(nameof(timeLimitMinutes));
        }

        // 3. Gọi `GetOwnedSetAsync` để thực hiện bước nghiệp vụ này.
        await GetOwnedSetAsync(setId, userId);
        // 4. Gọi `Resolve` và lưu kết quả vào `strategy`.
        IStudyModeStrategy strategy = _strategyResolver.Resolve(StudyMode.Quiz);
        // 5. Gọi `GetCardsAsync` và lưu kết quả vào `sourceCards`.
        List<Flashcard> sourceCards = await strategy.GetCardsAsync(setId, settings, userId);
        // 6. Kiểm tra `sourceCards.Count == 0` để chọn nhánh xử lý phù hợp.
        if (sourceCards.Count == 0)
        {
            // 7. Dừng xử lý và phát sinh lỗi `new QuizUnavailableException( "Không có thẻ phù hợp với bộ lọc hiện...`.
            throw new QuizUnavailableException(
                "Không có thẻ phù hợp với bộ lọc hiện tại.");
        }

        // 8. Tính giá trị và lưu vào `transaction` để dùng ở bước tiếp theo.
        IDbContextTransaction? transaction = null;
        // 9. Kiểm tra `_context.Database.IsRelational()` để chọn nhánh xử lý phù hợp.
        if (_context.Database.IsRelational())
        {
            // 10. Cập nhật `transaction` bằng giá trị mới.
            transaction = await _context.Database.BeginTransactionAsync();
        }

        // 11. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 12. Gọi `GetUtcNow` và lưu kết quả vào `now`.
            DateTime now = GetUtcNow();
            // 13. Gọi `ExecuteUpdateAsync` để thực hiện bước nghiệp vụ này.
            await _context.StudySessions
                .Where(session => session.FlashcardSetId == setId
                    && session.UserId == userId
                    && session.Mode == StudyMode.Quiz
                    && session.Score == null
                    && session.CompletedAt == null)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(session => session.CompletedAt, now));

            // 14. Khởi tạo `session` với dữ liệu ban đầu cần thiết.
            var session = new StudySession
            {
                FlashcardSetId = setId,
                UserId = userId,
                Mode = StudyMode.Quiz,
                CompletedAt = null,
                StartedAt = now,
                QuizStartedAtUtc = timeLimitMinutes.HasValue ? now : null,
                QuizTimeLimitSeconds = timeLimitMinutes.HasValue
                    ? timeLimitMinutes.Value * 60
                    : null
            };
            // 15. Gọi `BuildQuestionsAsync` và lưu kết quả vào `questions`.
            List<QuizSessionQuestion> questions = await _questionFactory.BuildQuestionsAsync(
                setId,
                userId,
                sourceCards);
            // 16. Cập nhật `session.PlannedItemCount` bằng giá trị mới.
            session.PlannedItemCount = questions.Count;
            // 17. Duyệt từng `question` trong `questions` để xử lý lần lượt.
            foreach (QuizSessionQuestion question in questions)
            {
                // 18. Cập nhật `question.StudySession` bằng giá trị mới.
                question.StudySession = session;
            }

            // 19. Gọi `AddRange` để thực hiện bước nghiệp vụ này.
            _context.QuizSessionQuestions.AddRange(questions);
            // 20. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync();

            // 21. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 22. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
                await transaction.CommitAsync();
            }

            // 23. Trả `session` cho nơi gọi.
            return session;
        }
        catch (DbUpdateException exception) when (IsActiveQuizUniqueConflict(exception))
        {
            // 24. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 25. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
                await transaction.RollbackAsync();
            }

            // 26. Gọi `Clear` để thực hiện bước nghiệp vụ này.
            _context.ChangeTracker.Clear();
            // 27. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `winningSession`.
            StudySession? winningSession = await _context.StudySessions
                .AsNoTracking()
                .Where(session => session.FlashcardSetId == setId
                    && session.UserId == userId
                    && session.Mode == StudyMode.Quiz
                    && session.Score == null
                    && session.CompletedAt == null)
                .OrderByDescending(session => session.Id)
                .FirstOrDefaultAsync();
            // 28. Kiểm tra `winningSession != null` để chọn nhánh xử lý phù hợp.
            if (winningSession != null)
            {
                // 29. Trả `winningSession` cho nơi gọi.
                return winningSession;
            }

            // 30. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        catch
        {
            // 31. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 32. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
                await transaction.RollbackAsync();
            }

            // 33. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        finally
        {
            // 34. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 35. Gọi `DisposeAsync` để thực hiện bước nghiệp vụ này.
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<StudySession> StartOrResumeAsync(
        int setId,
        string userId,
        UserStudySettings settings)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == setId);
        // 2. Kiểm tra `set == null` để chọn nhánh xử lý phù hợp.
        if (set == null)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Bộ thẻ không tồn tại.")`.
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        }

        // 4. Kiểm tra `set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (set.UserId != userId)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền học bộ thẻ này.")`.
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");
        }

        // 6. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `existingSession`.
        StudySession? existingSession = await _context.StudySessions
            .AsNoTracking()
            .Where(session => session.FlashcardSetId == setId
                && session.UserId == userId
                && session.Mode == StudyMode.Quiz
                && session.Score == null
                && session.CompletedAt == null)
            .OrderByDescending(session => session.Id)
            .FirstOrDefaultAsync();
        // 7. Kiểm tra `existingSession != null` để chọn nhánh xử lý phù hợp.
        if (existingSession != null)
        {
            // 8. Gọi `AnyAsync` và lưu kết quả vào `hasUnansweredQuestion`.
            bool hasUnansweredQuestion = await _context.QuizSessionQuestions.AnyAsync(question =>
                question.StudySessionId == existingSession.Id
                && question.IsCorrect == null);
            // 9. Kiểm tra `!hasUnansweredQuestion` để chọn nhánh xử lý phù hợp.
            if (!hasUnansweredQuestion)
            {
                // 10. Gọi `RecoverCompletedSessionIfNeededAsync` để thực hiện bước nghiệp vụ này.
                await RecoverCompletedSessionIfNeededAsync(existingSession);
            }

            // 11. Trả `existingSession` cho nơi gọi.
            return existingSession;
        }

        // 12. Gọi `Resolve` và lưu kết quả vào `strategy`.
        IStudyModeStrategy strategy = _strategyResolver.Resolve(StudyMode.Quiz);
        // 13. Gọi `GetCardsAsync` và lưu kết quả vào `sourceCards`.
        List<Flashcard> sourceCards = await strategy.GetCardsAsync(setId, settings, userId);
        // 14. Kiểm tra `sourceCards.Count == 0` để chọn nhánh xử lý phù hợp.
        if (sourceCards.Count == 0)
        {
            // 15. Dừng xử lý và phát sinh lỗi `new QuizUnavailableException( "Không có thẻ phù hợp với bộ lọc hiện...`.
            throw new QuizUnavailableException(
                "Không có thẻ phù hợp với bộ lọc hiện tại.");
        }

        // 16. Tính giá trị và lưu vào `transaction` để dùng ở bước tiếp theo.
        IDbContextTransaction? transaction = null;
        // 17. Kiểm tra `_context.Database.IsRelational()` để chọn nhánh xử lý phù hợp.
        if (_context.Database.IsRelational())
        {
            // 18. Cập nhật `transaction` bằng giá trị mới.
            transaction = await _context.Database.BeginTransactionAsync();
        }

        // 19. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 20. Khởi tạo `session` với dữ liệu ban đầu cần thiết.
            var session = new StudySession
            {
                FlashcardSetId = setId,
                UserId = userId,
                Mode = StudyMode.Quiz,
                CompletedAt = null,
                StartedAt = GetUtcNow()
            };
            // 21. Gọi `BuildQuestionsAsync` và lưu kết quả vào `questions`.
            List<QuizSessionQuestion> questions = await _questionFactory.BuildQuestionsAsync(
                setId,
                userId,
                sourceCards);
            // 22. Cập nhật `session.PlannedItemCount` bằng giá trị mới.
            session.PlannedItemCount = questions.Count;
            // 23. Duyệt từng `question` trong `questions` để xử lý lần lượt.
            foreach (QuizSessionQuestion question in questions)
            {
                // 24. Cập nhật `question.StudySession` bằng giá trị mới.
                question.StudySession = session;
            }

            // 25. Gọi `AddRange` để thực hiện bước nghiệp vụ này.
            _context.QuizSessionQuestions.AddRange(questions);
            // 26. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync();

            // 27. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 28. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
                await transaction.CommitAsync();
            }

            // 29. Trả `session` cho nơi gọi.
            return session;
        }
        catch (DbUpdateException exception) when (IsActiveQuizUniqueConflict(exception))
        {
            // 30. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 31. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
                await transaction.RollbackAsync();
            }

            // 32. Gọi `Clear` để thực hiện bước nghiệp vụ này.
            _context.ChangeTracker.Clear();
            // 33. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `winner`.
            StudySession? winner = await _context.StudySessions
                .AsNoTracking()
                .Where(row => row.FlashcardSetId == setId
                    && row.UserId == userId
                    && row.Mode == StudyMode.Quiz
                    && row.Score == null
                    && row.CompletedAt == null)
                .OrderByDescending(row => row.Id)
                .FirstOrDefaultAsync();
            // 34. Kiểm tra `winner != null` để chọn nhánh xử lý phù hợp.
            if (winner != null)
            {
                // 35. Trả `winner` cho nơi gọi.
                return winner;
            }

            // 36. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        finally
        {
            // 37. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 38. Gọi `DisposeAsync` để thực hiện bước nghiệp vụ này.
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task AbandonActiveAsync(int setId, string userId)
    {
        await GetOwnedSetAsync(setId, userId);
        DateTime now = GetUtcNow();
        await _context.StudySessions
            .Where(session => session.FlashcardSetId == setId
                && session.UserId == userId
                && session.Mode == StudyMode.Quiz
                && session.Score == null
                && session.CompletedAt == null)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(session => session.CompletedAt, now));
    }

    public async Task AbandonAsync(int setId, int sessionId, string userId)
    {
        StudySession? session = await _context.StudySessions
            .FirstOrDefaultAsync(row => row.Id == sessionId);
        if (session == null
            || session.FlashcardSetId != setId
            || session.Mode != StudyMode.Quiz)
        {
            throw new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.");
        }

        if (session.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền hủy phiên trắc nghiệm này.");
        }

        if (session.CompletedAt.HasValue || session.Score.HasValue)
        {
            return;
        }

        session.CompletedAt = GetUtcNow();
        await _context.SaveChangesAsync();
    }

    public async Task<QuizQuestionState> GetCurrentQuestionAsync(
        int setId,
        int sessionId,
        string userId,
        int? questionId = null)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `session`.
        StudySession? session = await _context.StudySessions
            .AsNoTracking()
            .Include(row => row.FlashcardSet)
            .FirstOrDefaultAsync(row => row.Id == sessionId);
        // 2. Kiểm tra `session == null || session.FlashcardSetId != setId || session.Mode ...` để chọn nhánh xử lý phù hợp.
        if (session == null
            || session.FlashcardSetId != setId
            || session.Mode != StudyMode.Quiz)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.")`.
            throw new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.");
        }

        // 4. Kiểm tra `session.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (session.UserId != userId)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền xem phiên trắc nghi...`.
            throw new UnauthorizedAccessException("Không có quyền xem phiên trắc nghiệm này.");
        }

        // 6. Kiểm tra `IsAbandoned(session)` để chọn nhánh xử lý phù hợp.
        if (IsAbandoned(session))
        {
            // 7. Dừng xử lý và phát sinh lỗi `await CreateAbandonedExceptionAsync(session)`.
            throw await CreateAbandonedExceptionAsync(session);
        }

        // 8. Gọi `GetUtcNow` và lưu kết quả vào `now`.
        DateTime now = GetUtcNow();
        // 9. Kiểm tra `IsExpired(session, now)` để chọn nhánh xử lý phù hợp.
        if (IsExpired(session, now))
        {
            // 10. Gọi `CompleteExpiredAsync` để thực hiện bước nghiệp vụ này.
            await CompleteExpiredAsync(setId, sessionId, userId);
            // 11. Dừng xử lý và phát sinh lỗi `new QuizExpiredException()`.
            throw new QuizExpiredException();
        }

        // 12. Gọi `ToListAsync` và lưu kết quả vào `questions`.
        List<QuizSessionQuestion> questions = await _context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sessionId)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        // 13. Gọi `SingleAsync` và lưu kết quả vào `authoritativeSession`.
        StudySession authoritativeSession = await _context.StudySessions
            .AsNoTracking()
            .SingleAsync(row => row.Id == sessionId);
        // 14. Kiểm tra `IsAbandoned(authoritativeSession)` để chọn nhánh xử lý phù hợp.
        if (IsAbandoned(authoritativeSession))
        {
            // 15. Dừng xử lý và phát sinh lỗi `await CreateAbandonedExceptionAsync(authoritativeSession)`.
            throw await CreateAbandonedExceptionAsync(authoritativeSession);
        }

        // 16. Tính giá trị và lưu vào `totalQuestions` để dùng ở bước tiếp theo.
        int totalQuestions = questions.Count;
        // 17. Gọi `Count` và lưu kết quả vào `answeredCount`.
        int answeredCount = questions.Count(question => question.IsCorrect != null);
        // 18. Gọi `Count` và lưu kết quả vào `correctCount`.
        int correctCount = questions.Count(question => question.IsCorrect == true);
        // 19. Gọi `FirstOrDefault` và lưu kết quả vào `pendingQuestion`.
        QuizSessionQuestion? pendingQuestion = questions
            .FirstOrDefault(question => question.IsCorrect == null);
        // 20. Tính giá trị và lưu vào `currentQuestion` để dùng ở bước tiếp theo.
        QuizSessionQuestion? currentQuestion = questionId.HasValue
            ? questions.SingleOrDefault(question => question.Id == questionId.Value)
            : pendingQuestion;
        // 21. Kiểm tra `questionId.HasValue && currentQuestion == null` để chọn nhánh xử lý phù hợp.
        if (questionId.HasValue && currentQuestion == null)
        {
            // 22. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Câu hỏi trắc nghiệm không tồn tại.")`.
            throw new KeyNotFoundException("Câu hỏi trắc nghiệm không tồn tại.");
        }

        // 23. Kiểm tra `currentQuestion == null && session.Score == null` để chọn nhánh xử lý phù hợp.
        if (currentQuestion == null && session.Score == null)
        {
            // 24. Gọi `RecoverCompletedSessionIfNeededAsync` để thực hiện bước nghiệp vụ này.
            await RecoverCompletedSessionIfNeededAsync(session);
        }

        // 25. Tính giá trị và lưu vào `isReviewOnly` để dùng ở bước tiếp theo.
        bool isReviewOnly = currentQuestion?.IsCorrect != null;
        // 26. Tính giá trị và lưu vào `currentQuestionIndex` để dùng ở bước tiếp theo.
        int currentQuestionIndex = currentQuestion == null
            ? -1
            : questions.FindIndex(question => question.Id == currentQuestion.Id);

        // 27. Tạo và trả đối tượng kết quả cho nơi gọi.
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
        // 1. Kiểm tra `selectedChoiceIndex is < 0 or > 3` để chọn nhánh xử lý phù hợp.
        if (selectedChoiceIndex is < 0 or > 3)
        {
            // 2. Dừng xử lý và phát sinh lỗi `new ArgumentOutOfRangeException(nameof(selectedChoiceIndex))`.
            throw new ArgumentOutOfRangeException(nameof(selectedChoiceIndex));
        }

        // 3. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `session`.
        StudySession? session = await _context.StudySessions
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == sessionId);
        // 4. Kiểm tra `session == null || session.FlashcardSetId != setId || session.Mode ...` để chọn nhánh xử lý phù hợp.
        if (session == null
            || session.FlashcardSetId != setId
            || session.Mode != StudyMode.Quiz)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.")`.
            throw new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.");
        }

        // 6. Kiểm tra `session.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (session.UserId != userId)
        {
            // 7. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền trả lời phiên trắc ...`.
            throw new UnauthorizedAccessException("Không có quyền trả lời phiên trắc nghiệm này.");
        }

        // 8. Kiểm tra `IsAbandoned(session)` để chọn nhánh xử lý phù hợp.
        if (IsAbandoned(session))
        {
            // 9. Dừng xử lý và phát sinh lỗi `await CreateAbandonedExceptionAsync(session)`.
            throw await CreateAbandonedExceptionAsync(session);
        }

        // 10. Gọi `GetUtcNow` và lưu kết quả vào `now`.
        DateTime now = GetUtcNow();
        // 11. Kiểm tra `IsExpired(session, now)` để chọn nhánh xử lý phù hợp.
        if (IsExpired(session, now))
        {
            // 12. Gọi `CompleteExpiredAsync` để thực hiện bước nghiệp vụ này.
            await CompleteExpiredAsync(setId, sessionId, userId);
            // 13. Dừng xử lý và phát sinh lỗi `new QuizExpiredException()`.
            throw new QuizExpiredException();
        }

        // 14. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `question`.
        QuizSessionQuestion? question = await _context.QuizSessionQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == questionId);
        // 15. Kiểm tra `question == null || question.StudySessionId != sessionId` để chọn nhánh xử lý phù hợp.
        if (question == null || question.StudySessionId != sessionId)
        {
            // 16. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Câu hỏi trắc nghiệm không tồn tại.")`.
            throw new KeyNotFoundException("Câu hỏi trắc nghiệm không tồn tại.");
        }

        // 17. Tính giá trị và lưu vào `transaction` để dùng ở bước tiếp theo.
        IDbContextTransaction? transaction = null;
        // 18. Kiểm tra `_context.Database.IsRelational()` để chọn nhánh xử lý phù hợp.
        if (_context.Database.IsRelational())
        {
            // 19. Cập nhật `transaction` bằng giá trị mới.
            transaction = await _context.Database.BeginTransactionAsync();
        }

        // 20. Tính giá trị và lưu vào `completionEvent` để dùng ở bước tiếp theo.
        StudySessionCompletedEvent? completionEvent = null;
        // 21. Khai báo `answerResult` để lưu dữ liệu dùng ở các bước sau.
        QuizAnswerResult answerResult;
        // 22. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 23. Gọi `SingleAsync` và lưu kết quả vào `persistedSession`.
            StudySession persistedSession = await _context.StudySessions
                .AsNoTracking()
                .SingleAsync(row => row.Id == sessionId);
            // 24. Gọi `GetUtcNow` và lưu kết quả vào `writeNow`.
            DateTime writeNow = GetUtcNow();
            // 25. Kiểm tra `IsAbandoned(persistedSession)` để chọn nhánh xử lý phù hợp.
            if (IsAbandoned(persistedSession))
            {
                // 26. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
                if (transaction != null)
                {
                    // 27. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
                    await transaction.RollbackAsync();
                    // 28. Gọi `DisposeAsync` để thực hiện bước nghiệp vụ này.
                    await transaction.DisposeAsync();
                    // 29. Cập nhật `transaction` bằng giá trị mới.
                    transaction = null;
                }

                // 30. Dừng xử lý và phát sinh lỗi `await CreateAbandonedExceptionAsync(persistedSession)`.
                throw await CreateAbandonedExceptionAsync(persistedSession);
            }

            // 31. Kiểm tra `IsExpired(persistedSession, writeNow)` để chọn nhánh xử lý phù hợp.
            if (IsExpired(persistedSession, writeNow))
            {
                // 32. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
                if (transaction != null)
                {
                    // 33. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
                    await transaction.RollbackAsync();
                    // 34. Gọi `DisposeAsync` để thực hiện bước nghiệp vụ này.
                    await transaction.DisposeAsync();
                    // 35. Cập nhật `transaction` bằng giá trị mới.
                    transaction = null;
                }

                // 36. Gọi `CompleteExpiredAsync` để thực hiện bước nghiệp vụ này.
                await CompleteExpiredAsync(setId, sessionId, userId);
                // 37. Dừng xử lý và phát sinh lỗi `new QuizExpiredException()`.
                throw new QuizExpiredException();
            }

            // 38. Tính giá trị và lưu vào `isCorrect` để dùng ở bước tiếp theo.
            bool isCorrect = selectedChoiceIndex == question.CorrectChoiceIndex;
            // 39. Gọi `ExecuteUpdateAsync` và lưu kết quả vào `affected`.
            int affected = await _context.QuizSessionQuestions
                .Where(row => row.Id == questionId
                    && row.IsCorrect == null
                    && row.StudySession!.Score == null
                    && row.StudySession.CompletedAt == null)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(row => row.SelectedChoiceIndex, selectedChoiceIndex)
                    .SetProperty(row => row.IsCorrect, isCorrect)
                    .SetProperty(row => row.AnsweredAt, writeNow));
            // 40. Kiểm tra `affected == 0` để chọn nhánh xử lý phù hợp.
            if (affected == 0)
            {
                // 41. Gọi `SingleAsync` và lưu kết quả vào `currentSession`.
                StudySession currentSession = await _context.StudySessions
                    .AsNoTracking()
                    .SingleAsync(row => row.Id == sessionId);
                // 42. Kiểm tra `IsAbandoned(currentSession)` để chọn nhánh xử lý phù hợp.
                if (IsAbandoned(currentSession))
                {
                    // 43. Dừng xử lý và phát sinh lỗi `await CreateAbandonedExceptionAsync(currentSession)`.
                    throw await CreateAbandonedExceptionAsync(currentSession);
                }

                // 44. Gọi `SingleAsync` và lưu kết quả vào `storedQuestion`.
                QuizSessionQuestion storedQuestion = await _context.QuizSessionQuestions
                    .AsNoTracking()
                    .SingleAsync(row => row.Id == questionId);
                // 45. Kiểm tra `storedQuestion.SelectedChoiceIndex != selectedChoiceIndex` để chọn nhánh xử lý phù hợp.
                if (storedQuestion.SelectedChoiceIndex != selectedChoiceIndex)
                {
                    // 46. Dừng xử lý và phát sinh lỗi `new QuizConflictException( "Câu hỏi đã được trả lời bằng lựa chọn k...`.
                    throw new QuizConflictException(
                        "Câu hỏi đã được trả lời bằng lựa chọn khác.");
                }

                // 47. Tính giá trị và lưu vào `storedIsLastQuestion` để dùng ở bước tiếp theo.
                bool storedIsLastQuestion = !await _context.QuizSessionQuestions.AnyAsync(row =>
                    row.StudySessionId == sessionId
                    && row.IsCorrect == null);
                // 48. Kiểm tra `storedIsLastQuestion` để chọn nhánh xử lý phù hợp.
                if (storedIsLastQuestion)
                {
                    // 49. Cập nhật `completionEvent` bằng giá trị mới.
                    completionEvent = await CompleteSessionIfEligibleAsync(session);
                }

                // 50. Cập nhật `answerResult` bằng giá trị mới.
                answerResult = new QuizAnswerResult(
                    storedQuestion.IsCorrect == true,
                    storedQuestion.CorrectChoiceIndex,
                    storedIsLastQuestion);
            }
            else
            {
                // 51. Tính giá trị và lưu vào `isLastQuestion` để dùng ở bước tiếp theo.
                bool isLastQuestion = !await _context.QuizSessionQuestions.AnyAsync(row =>
                    row.StudySessionId == sessionId
                    && row.IsCorrect == null);
                // 52. Kiểm tra `isLastQuestion` để chọn nhánh xử lý phù hợp.
                if (isLastQuestion)
                {
                    // 53. Cập nhật `completionEvent` bằng giá trị mới.
                    completionEvent = await CompleteSessionIfEligibleAsync(session);
                }

                // 54. Cập nhật `answerResult` bằng giá trị mới.
                answerResult = new QuizAnswerResult(
                    isCorrect,
                    question.CorrectChoiceIndex,
                    isLastQuestion);
            }

            // 55. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 56. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
                await transaction.CommitAsync();
            }
        }
        catch
        {
            // 57. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 58. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
                await transaction.RollbackAsync();
            }

            // 59. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        finally
        {
            // 60. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 61. Gọi `DisposeAsync` để thực hiện bước nghiệp vụ này.
                await transaction.DisposeAsync();
            }
        }

        // 62. Kiểm tra `completionEvent != null` để chọn nhánh xử lý phù hợp.
        if (completionEvent != null)
        {
            // 63. Gọi `PublishAsync` để thực hiện bước nghiệp vụ này.
            await _studyEvents.PublishAsync(completionEvent);
        }

        // 64. Trả `answerResult` cho nơi gọi.
        return answerResult;
    }

    public async Task CompleteExpiredAsync(int setId, int sessionId, string userId)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `session`.
        StudySession? session = await _context.StudySessions
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == sessionId);
        // 2. Kiểm tra `session == null || session.FlashcardSetId != setId || session.Mode ...` để chọn nhánh xử lý phù hợp.
        if (session == null
            || session.FlashcardSetId != setId
            || session.Mode != StudyMode.Quiz)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.")`.
            throw new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.");
        }

        // 4. Kiểm tra `session.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (session.UserId != userId)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền hoàn thành phiên tr...`.
            throw new UnauthorizedAccessException("Không có quyền hoàn thành phiên trắc nghiệm này.");
        }

        // 6. Kiểm tra `IsAbandoned(session)` để chọn nhánh xử lý phù hợp.
        if (IsAbandoned(session))
        {
            // 7. Dừng xử lý và phát sinh lỗi `await CreateAbandonedExceptionAsync(session)`.
            throw await CreateAbandonedExceptionAsync(session);
        }

        // 8. Gọi `GetUtcNow` và lưu kết quả vào `now`.
        DateTime now = GetUtcNow();
        // 9. Gọi `SingleAsync` và lưu kết quả vào `authoritativeSession`.
        StudySession authoritativeSession = await _context.StudySessions
            .AsNoTracking()
            .SingleAsync(row => row.Id == sessionId);
        // 10. Kiểm tra `IsAbandoned(authoritativeSession)` để chọn nhánh xử lý phù hợp.
        if (IsAbandoned(authoritativeSession))
        {
            // 11. Dừng xử lý và phát sinh lỗi `await CreateAbandonedExceptionAsync(authoritativeSession)`.
            throw await CreateAbandonedExceptionAsync(authoritativeSession);
        }

        // 12. Kiểm tra `!IsExpired(authoritativeSession, now)` để chọn nhánh xử lý phù hợp.
        if (!IsExpired(authoritativeSession, now))
        {
            // 13. Dừng xử lý và phát sinh lỗi `new QuizNotExpiredException(GetRemainingSeconds(authoritativeSessio...`.
            throw new QuizNotExpiredException(GetRemainingSeconds(authoritativeSession, now) ?? 0);
        }

        // 14. Gọi `CompleteExpiredSessionAsync` để thực hiện bước nghiệp vụ này.
        await CompleteExpiredSessionAsync(authoritativeSession, now);

        // 15. Gọi `SingleAsync` và lưu kết quả vào `completedSession`.
        StudySession completedSession = await _context.StudySessions
            .AsNoTracking()
            .SingleAsync(row => row.Id == sessionId);
        // 16. Kiểm tra `IsAbandoned(completedSession)` để chọn nhánh xử lý phù hợp.
        if (IsAbandoned(completedSession))
        {
            // 17. Dừng xử lý và phát sinh lỗi `await CreateAbandonedExceptionAsync(completedSession)`.
            throw await CreateAbandonedExceptionAsync(completedSession);
        }

        // 18. Kiểm tra `completedSession.Score == null` để chọn nhánh xử lý phù hợp.
        if (completedSession.Score == null)
        {
            // 19. Dừng xử lý và phát sinh lỗi `new QuizConflictException( "Không thể hoàn thành phiên trắc nghiệm ...`.
            throw new QuizConflictException(
                "Không thể hoàn thành phiên trắc nghiệm vì trạng thái phiên đã thay đổi.");
        }
    }

    private async Task CompleteExpiredSessionAsync(StudySession session, DateTime now)
    {
        // 1. Tính giá trị và lưu vào `transaction` để dùng ở bước tiếp theo.
        IDbContextTransaction? transaction = null;
        // 2. Kiểm tra `_context.Database.IsRelational()` để chọn nhánh xử lý phù hợp.
        if (_context.Database.IsRelational())
        {
            // 3. Cập nhật `transaction` bằng giá trị mới.
            transaction = await _context.Database.BeginTransactionAsync();
        }

        // 4. Khai báo `completionEvent` để lưu dữ liệu dùng ở các bước sau.
        StudySessionCompletedEvent? completionEvent;
        // 5. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 6. Gọi `ExecuteUpdateAsync` để thực hiện bước nghiệp vụ này.
            await _context.QuizSessionQuestions
                .Where(question => question.StudySessionId == session.Id
                    && question.IsCorrect == null
                    && question.StudySession!.Score == null
                    && question.StudySession.CompletedAt == null)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(question => question.IsCorrect, false)
                    .SetProperty(question => question.AnsweredAt, now));
            // 7. Cập nhật `completionEvent` bằng giá trị mới.
            completionEvent = await CompleteSessionIfEligibleAsync(session, now);

            // 8. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 9. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
                await transaction.CommitAsync();
            }
        }
        catch
        {
            // 10. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 11. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
                await transaction.RollbackAsync();
            }

            // 12. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        finally
        {
            // 13. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 14. Gọi `DisposeAsync` để thực hiện bước nghiệp vụ này.
                await transaction.DisposeAsync();
            }
        }

        // 15. Kiểm tra `completionEvent != null` để chọn nhánh xử lý phù hợp.
        if (completionEvent != null)
        {
            // 16. Gọi `PublishAsync` để thực hiện bước nghiệp vụ này.
            await _studyEvents.PublishAsync(completionEvent);
        }
    }

    private async Task<StudySessionCompletedEvent?> CompleteSessionIfEligibleAsync(
        StudySession session,
        DateTime? completedAtUtc = null)
    {
        // 1. Gọi `AnyAsync` và lưu kết quả vào `hasUnansweredQuestion`.
        bool hasUnansweredQuestion = await _context.QuizSessionQuestions.AnyAsync(row =>
            row.StudySessionId == session.Id
            && row.IsCorrect == null);
        // 2. Kiểm tra `hasUnansweredQuestion` để chọn nhánh xử lý phù hợp.
        if (hasUnansweredQuestion)
        {
            // 3. Trả `null` cho nơi gọi.
            return null;
        }

        // 4. Gọi `CountAsync` và lưu kết quả vào `totalCount`.
        int totalCount = await _context.QuizSessionQuestions.CountAsync(row =>
            row.StudySessionId == session.Id);
        // 5. Gọi `CountAsync` và lưu kết quả vào `correctCount`.
        int correctCount = await _context.QuizSessionQuestions.CountAsync(row =>
            row.StudySessionId == session.Id
            && row.IsCorrect == true);
        // 6. Tính giá trị và lưu vào `score` để dùng ở bước tiếp theo.
        int score = totalCount == 0
            ? 0
            : (int)Math.Round(
                correctCount * 100.0 / totalCount,
                MidpointRounding.AwayFromZero);
        // 7. Tính giá trị và lưu vào `completedAt` để dùng ở bước tiếp theo.
        DateTime completedAt = completedAtUtc ?? GetUtcNow();
        // 8. Gọi `CalculateDurationSeconds` và lưu kết quả vào `durationSeconds`.
        int durationSeconds = StudySessionTiming.CalculateDurationSeconds(
            session.StartedAt,
            completedAt);
        // 9. Gọi `ExecuteUpdateAsync` và lưu kết quả vào `affected`.
        int affected = await _context.StudySessions
            .Where(row => row.Id == session.Id
                && row.FlashcardSetId == session.FlashcardSetId
                && row.UserId == session.UserId
                && row.Mode == StudyMode.Quiz
                && row.Score == null
                && row.CompletedAt == null)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(row => row.Score, score)
                .SetProperty(row => row.CompletedAt, completedAt)
                .SetProperty(row => row.PlannedItemCount, totalCount)
                .SetProperty(row => row.DurationSeconds, durationSeconds));
        // 10. Kiểm tra `affected == 1` để chọn nhánh xử lý phù hợp.
        if (affected == 1)
        {
            // 11. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new StudySessionCompletedEvent(
                UserId: session.UserId,
                OccurredAtUtc: completedAt,
                SetId: session.FlashcardSetId,
                SessionId: session.Id,
                Mode: StudyMode.Quiz,
                Score: score);
        }

        // 12. Trả `null` cho nơi gọi.
        return null;
    }

    private DateTime GetUtcNow()
    {
        // 1. Trả `_timeProvider.GetUtcNow().UtcDateTime` cho nơi gọi.
        return _timeProvider.GetUtcNow().UtcDateTime;
    }

    private static bool IsExpired(StudySession session, DateTime now)
    {
        // 1. Gọi `GetDeadlineUtc` và lưu kết quả vào `deadline`.
        DateTime? deadline = GetDeadlineUtc(session);
        // 2. Trả `deadline.HasValue && now >= deadline.Value` cho nơi gọi.
        return deadline.HasValue && now >= deadline.Value;
    }

    private static DateTime? GetDeadlineUtc(StudySession session)
    {
        // 1. Kiểm tra `session.QuizStartedAtUtc is not DateTime startedAtUtc || session.Qu...` để chọn nhánh xử lý phù hợp.
        if (session.QuizStartedAtUtc is not DateTime startedAtUtc
            || session.QuizTimeLimitSeconds is not int timeLimitSeconds
            || timeLimitSeconds <= 0)
        {
            // 2. Trả `null` cho nơi gọi.
            return null;
        }

        // 3. Trả kết quả từ `AddSeconds` cho nơi gọi.
        return DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Utc)
            .AddSeconds(timeLimitSeconds);
    }

    private static int? GetRemainingSeconds(StudySession session, DateTime now)
    {
        // 1. Gọi `GetDeadlineUtc` và lưu kết quả vào `deadline`.
        DateTime? deadline = GetDeadlineUtc(session);
        // 2. Kiểm tra `!deadline.HasValue` để chọn nhánh xử lý phù hợp.
        if (!deadline.HasValue)
        {
            // 3. Trả `null` cho nơi gọi.
            return null;
        }

        // 4. Trả kết quả từ `Max` cho nơi gọi.
        return Math.Max(0, (int)Math.Ceiling((deadline.Value - now).TotalSeconds));
    }

    private async Task RecoverCompletedSessionIfNeededAsync(StudySession session)
    {
        // 1. Tính giá trị và lưu vào `transaction` để dùng ở bước tiếp theo.
        IDbContextTransaction? transaction = null;
        // 2. Kiểm tra `_context.Database.IsRelational()` để chọn nhánh xử lý phù hợp.
        if (_context.Database.IsRelational())
        {
            // 3. Cập nhật `transaction` bằng giá trị mới.
            transaction = await _context.Database.BeginTransactionAsync();
        }

        // 4. Khai báo `completionEvent` để lưu dữ liệu dùng ở các bước sau.
        StudySessionCompletedEvent? completionEvent;
        // 5. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 6. Cập nhật `completionEvent` bằng giá trị mới.
            completionEvent = await CompleteSessionIfEligibleAsync(session);
            // 7. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 8. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
                await transaction.CommitAsync();
            }
        }
        catch
        {
            // 9. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 10. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
                await transaction.RollbackAsync();
            }

            // 11. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        finally
        {
            // 12. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 13. Gọi `DisposeAsync` để thực hiện bước nghiệp vụ này.
                await transaction.DisposeAsync();
            }
        }

        // 14. Kiểm tra `completionEvent != null` để chọn nhánh xử lý phù hợp.
        if (completionEvent != null)
        {
            // 15. Gọi `PublishAsync` để thực hiện bước nghiệp vụ này.
            await _studyEvents.PublishAsync(completionEvent);
        }
    }

    public async Task<QuizSessionResult> GetResultAsync(
        int setId,
        int sessionId,
        string userId)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `session`.
        StudySession? session = await _context.StudySessions
            .AsNoTracking()
            .Include(row => row.FlashcardSet)
            .FirstOrDefaultAsync(row => row.Id == sessionId);
        // 2. Kiểm tra `session == null || session.FlashcardSetId != setId || session.Mode ...` để chọn nhánh xử lý phù hợp.
        if (session == null
            || session.FlashcardSetId != setId
            || session.Mode != StudyMode.Quiz)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.")`.
            throw new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.");
        }

        // 4. Kiểm tra `session.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (session.UserId != userId)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException( "Không có quyền xem kết quả phiên ...`.
            throw new UnauthorizedAccessException(
                "Không có quyền xem kết quả phiên trắc nghiệm này.");
        }

        // 6. Kiểm tra `session.Score == null` để chọn nhánh xử lý phù hợp.
        if (session.Score == null)
        {
            // 7. Dừng xử lý và phát sinh lỗi `new QuizConflictException("Phiên trắc nghiệm chưa hoàn thành.")`.
            throw new QuizConflictException("Phiên trắc nghiệm chưa hoàn thành.");
        }

        // 8. Gọi `ToListAsync` và lưu kết quả vào `questions`.
        List<QuizSessionQuestion> questions = await _context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sessionId)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        // 9. Gọi `ToList` và lưu kết quả vào `wrongAnswers`.
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

        // 10. Tạo và trả đối tượng kết quả cho nơi gọi.
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
        // 1. Cập nhật `(StudySession sourceSession, List<QuizSessionQuestion> sourceQuesti...` bằng giá trị mới.
        (StudySession sourceSession, List<QuizSessionQuestion> sourceQuestions) =
            await LoadQuizSourceAsync(setId, sessionId, userId, requireCompleted: true);

        // 2. Gọi `ToList` và lưu kết quả vào `wrongQuestions`.
        List<QuizSessionQuestion> wrongQuestions = sourceQuestions
            .Where(question => question.IsCorrect == false)
            .ToList();
        // 3. Kiểm tra `wrongQuestions.Count == 0` để chọn nhánh xử lý phù hợp.
        if (wrongQuestions.Count == 0)
        {
            // 4. Dừng xử lý và phát sinh lỗi `new QuizConflictException("Phiên trắc nghiệm không có câu trả lời s...`.
            throw new QuizConflictException("Phiên trắc nghiệm không có câu trả lời sai.");
        }

        // 5. Trả kết quả từ `CreateReplacementSessionAsync` cho nơi gọi.
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
        // 1. Cập nhật `(StudySession sourceSession, List<QuizSessionQuestion> sourceQuesti...` bằng giá trị mới.
        (StudySession sourceSession, List<QuizSessionQuestion> sourceQuestions) =
            await LoadQuizSourceAsync(setId, sessionId, userId, requireCompleted: true);

        // 2. Trả kết quả từ `CreateReplacementSessionAsync` cho nơi gọi.
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
        // 1. Cập nhật `(StudySession sourceSession, List<QuizSessionQuestion> sourceQuesti...` bằng giá trị mới.
        (StudySession sourceSession, List<QuizSessionQuestion> sourceQuestions) =
            await LoadQuizSourceAsync(setId, sessionId, userId, requireCompleted: false);

        // 2. Gọi `GetUtcNow` và lưu kết quả vào `restartNow`.
        DateTime restartNow = GetUtcNow();
        // 3. Kiểm tra `IsExpired(sourceSession, restartNow)` để chọn nhánh xử lý phù hợp.
        if (IsExpired(sourceSession, restartNow))
        {
            // 4. Gọi `CompleteExpiredSessionAsync` để thực hiện bước nghiệp vụ này.
            await CompleteExpiredSessionAsync(sourceSession, restartNow);
            // 5. Dừng xử lý và phát sinh lỗi `new QuizExpiredException()`.
            throw new QuizExpiredException();
        }

        // 6. Trả kết quả từ `CreateReplacementSessionAsync` cho nơi gọi.
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
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `sourceSession`.
        StudySession? sourceSession = await _context.StudySessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.Id == sessionId);
        // 2. Kiểm tra `sourceSession == null || sourceSession.FlashcardSetId != setId || s...` để chọn nhánh xử lý phù hợp.
        if (sourceSession == null
            || sourceSession.FlashcardSetId != setId
            || sourceSession.Mode != StudyMode.Quiz)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.")`.
            throw new KeyNotFoundException("Phiên trắc nghiệm không tồn tại.");
        }

        // 4. Kiểm tra `sourceSession.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (sourceSession.UserId != userId)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException( "Không có quyền tạo lại phiên trắc...`.
            throw new UnauthorizedAccessException(
                "Không có quyền tạo lại phiên trắc nghiệm này.");
        }

        // 6. Kiểm tra `requireCompleted && sourceSession.Score == null` để chọn nhánh xử lý phù hợp.
        if (requireCompleted && sourceSession.Score == null)
        {
            // 7. Dừng xử lý và phát sinh lỗi `new QuizConflictException("Phiên trắc nghiệm chưa hoàn thành.")`.
            throw new QuizConflictException("Phiên trắc nghiệm chưa hoàn thành.");
        }

        // 8. Kiểm tra `!requireCompleted && IsAbandoned(sourceSession)` để chọn nhánh xử lý phù hợp.
        if (!requireCompleted && IsAbandoned(sourceSession))
        {
            // 9. Dừng xử lý và phát sinh lỗi `await CreateAbandonedExceptionAsync(sourceSession)`.
            throw await CreateAbandonedExceptionAsync(sourceSession);
        }

        // 10. Kiểm tra `!requireCompleted && sourceSession.Score != null` để chọn nhánh xử lý phù hợp.
        if (!requireCompleted && sourceSession.Score != null)
        {
            // 11. Dừng xử lý và phát sinh lỗi `new QuizConflictException("Phiên trắc nghiệm không còn đang làm.")`.
            throw new QuizConflictException("Phiên trắc nghiệm không còn đang làm.");
        }

        // 12. Gọi `ToListAsync` và lưu kết quả vào `sourceQuestions`.
        List<QuizSessionQuestion> sourceQuestions = await _context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == sourceSession.Id)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        // 13. Trả `(sourceSession, sourceQuestions)` cho nơi gọi.
        return (sourceSession, sourceQuestions);
    }

    private async Task<StudySession> CreateReplacementSessionAsync(
        StudySession sourceSession,
        IReadOnlyList<QuizSessionQuestion> sourceQuestions,
        bool preserveDirections,
        bool reuseMatchingActiveSession,
        QuizRetryKind? retryKind)
    {
        // 1. Kiểm tra `reuseMatchingActiveSession` để chọn nhánh xử lý phù hợp.
        if (reuseMatchingActiveSession)
        {
            // 2. Gọi `FindActiveQuizSessionAsync` và lưu kết quả vào `activeSession`.
            StudySession? activeSession = await FindActiveQuizSessionAsync(sourceSession);
            // 3. Kiểm tra `activeSession != null && await MatchesRequestedRetryAsync( activeSe...` để chọn nhánh xử lý phù hợp.
            if (activeSession != null
                && await MatchesRequestedRetryAsync(
                    activeSession,
                    sourceQuestions,
                    preserveDirections,
                    sourceSession,
                    retryKind))
            {
                // 4. Trả `activeSession` cho nơi gọi.
                return activeSession;
            }
        }

        // 5. Tính giá trị và lưu vào `transaction` để dùng ở bước tiếp theo.
        IDbContextTransaction? transaction = null;
        // 6. Kiểm tra `_context.Database.IsRelational()` để chọn nhánh xử lý phù hợp.
        if (_context.Database.IsRelational())
        {
            // 7. Cập nhật `transaction` bằng giá trị mới.
            transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);
        }

        // 8. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 9. Gọi `ToArray` và lưu kết quả vào `sourceCardIds`.
            int[] sourceCardIds = sourceQuestions
                .Select(question => question.FlashcardId)
                .Distinct()
                .ToArray();
            // 10. Gọi `ToListAsync` và lưu kết quả vào `storedCards`.
            List<Flashcard> storedCards = await _context.Flashcards
                .AsNoTracking()
                .Where(card => sourceCardIds.Contains(card.Id))
                .ToListAsync();
            // 11. Kiểm tra `storedCards.Count != sourceCardIds.Length` để chọn nhánh xử lý phù hợp.
            if (storedCards.Count != sourceCardIds.Length)
            {
                // 12. Dừng xử lý và phát sinh lỗi `new QuizUnavailableException( "Một hoặc nhiều thẻ nguồn không còn k...`.
                throw new QuizUnavailableException(
                    "Một hoặc nhiều thẻ nguồn không còn khả dụng.");
            }

            // 13. Gọi `ToDictionary` và lưu kết quả vào `cardsById`.
            Dictionary<int, Flashcard> cardsById = storedCards
                .ToDictionary(card => card.Id);
            // 14. Gọi `ToList` và lưu kết quả vào `sourceCards`.
            List<Flashcard> sourceCards = sourceQuestions
                .Select(question => cardsById[question.FlashcardId])
                .ToList();
            // 15. Tính giá trị và lưu vào `fixedDirections` để dùng ở bước tiếp theo.
            IReadOnlyDictionary<int, QuizQuestionDirection>? fixedDirections =
                preserveDirections
                    ? sourceQuestions.ToDictionary(
                        question => question.FlashcardId,
                        question => question.Direction)
                    : null;
            // 16. Kiểm tra `reuseMatchingActiveSession` để chọn nhánh xử lý phù hợp.
            if (reuseMatchingActiveSession)
            {
                // 17. Gọi `FindActiveQuizSessionAsync` và lưu kết quả vào `activeSession`.
                StudySession? activeSession = await FindActiveQuizSessionAsync(sourceSession);
                // 18. Kiểm tra `activeSession != null && await MatchesRequestedRetryAsync( activeSe...` để chọn nhánh xử lý phù hợp.
                if (activeSession != null
                    && await MatchesRequestedRetryAsync(
                        activeSession,
                        sourceQuestions,
                        preserveDirections,
                        sourceSession,
                        retryKind))
                {
                    // 19. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
                    if (transaction != null)
                    {
                        // 20. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
                        await transaction.CommitAsync();
                    }

                    // 21. Trả `activeSession` cho nơi gọi.
                    return activeSession;
                }
            }

            // 22. Gọi `GetUtcNow` và lưu kết quả vào `now`.
            DateTime now = GetUtcNow();
            // 23. Kiểm tra `retryKind is null && IsExpired(sourceSession, now)` để chọn nhánh xử lý phù hợp.
            if (retryKind is null && IsExpired(sourceSession, now))
            {
                // 24. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
                if (transaction != null)
                {
                    // 25. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
                    await transaction.RollbackAsync();
                    // 26. Gọi `DisposeAsync` để thực hiện bước nghiệp vụ này.
                    await transaction.DisposeAsync();
                    // 27. Cập nhật `transaction` bằng giá trị mới.
                    transaction = null;
                }

                // 28. Gọi `CompleteExpiredSessionAsync` để thực hiện bước nghiệp vụ này.
                await CompleteExpiredSessionAsync(sourceSession, now);
                // 29. Dừng xử lý và phát sinh lỗi `new QuizExpiredException()`.
                throw new QuizExpiredException();
            }

            // 30. Gọi `ExecuteUpdateAsync` để thực hiện bước nghiệp vụ này.
            await _context.StudySessions
                .Where(session => session.FlashcardSetId == sourceSession.FlashcardSetId
                    && session.UserId == sourceSession.UserId
                    && session.Mode == StudyMode.Quiz
                    && session.Score == null
                    && session.CompletedAt == null)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(session => session.CompletedAt, now));

            // 31. Khởi tạo `replacementSession` với dữ liệu ban đầu cần thiết.
            var replacementSession = new StudySession
            {
                FlashcardSetId = sourceSession.FlashcardSetId,
                UserId = sourceSession.UserId,
                Mode = StudyMode.Quiz,
                CompletedAt = null,
                StartedAt = now,
                QuizStartedAtUtc = sourceSession.QuizTimeLimitSeconds.HasValue ? now : null,
                QuizTimeLimitSeconds = sourceSession.QuizTimeLimitSeconds,
                QuizRetrySourceSessionId = retryKind.HasValue ? sourceSession.Id : null,
                QuizRetryKind = retryKind
            };
            // 32. Gọi `BuildQuestionsAsync` và lưu kết quả vào `replacementQuestions`.
            List<QuizSessionQuestion> replacementQuestions =
                await _questionFactory.BuildQuestionsAsync(
                    sourceSession.FlashcardSetId,
                    sourceSession.UserId,
                    sourceCards,
                    fixedDirections);
            // 33. Cập nhật `replacementSession.PlannedItemCount` bằng giá trị mới.
            replacementSession.PlannedItemCount = replacementQuestions.Count;
            // 34. Duyệt từng `question` trong `replacementQuestions` để xử lý lần lượt.
            foreach (QuizSessionQuestion question in replacementQuestions)
            {
                // 35. Cập nhật `question.StudySession` bằng giá trị mới.
                question.StudySession = replacementSession;
            }

            // 36. Gọi `AddRange` để thực hiện bước nghiệp vụ này.
            _context.QuizSessionQuestions.AddRange(replacementQuestions);
            // 37. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync();

            // 38. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 39. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
                await transaction.CommitAsync();
            }

            // 40. Trả `replacementSession` cho nơi gọi.
            return replacementSession;
        }
        catch (DbUpdateException exception) when (IsActiveQuizUniqueConflict(exception))
        {
            // 41. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 42. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
                await transaction.RollbackAsync();
            }

            // 43. Gọi `Clear` để thực hiện bước nghiệp vụ này.
            _context.ChangeTracker.Clear();
            // 44. Gọi `FindActiveQuizSessionAsync` và lưu kết quả vào `activeSession`.
            StudySession? activeSession = await FindActiveQuizSessionAsync(sourceSession);
            // 45. Kiểm tra `reuseMatchingActiveSession && activeSession != null && await Matche...` để chọn nhánh xử lý phù hợp.
            if (reuseMatchingActiveSession
                && activeSession != null
                && await MatchesRequestedRetryAsync(
                    activeSession,
                    sourceQuestions,
                    preserveDirections,
                    sourceSession,
                    retryKind))
            {
                // 46. Trả `activeSession` cho nơi gọi.
                return activeSession;
            }

            // 47. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        catch
        {
            // 48. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 49. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
                await transaction.RollbackAsync();
            }

            // 50. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        finally
        {
            // 51. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 52. Gọi `DisposeAsync` để thực hiện bước nghiệp vụ này.
                await transaction.DisposeAsync();
            }
        }
    }

    private Task<StudySession?> FindActiveQuizSessionAsync(StudySession sourceSession)
    {
        // 1. Trả kết quả từ `FirstOrDefaultAsync` cho nơi gọi.
        return _context.StudySessions
            .AsNoTracking()
            .Where(session => session.FlashcardSetId == sourceSession.FlashcardSetId
                && session.UserId == sourceSession.UserId
                && session.Mode == StudyMode.Quiz
                && session.Score == null
                && session.CompletedAt == null)
            .OrderByDescending(session => session.Id)
            .FirstOrDefaultAsync();
    }

    private static bool IsAbandoned(StudySession session)
    {
        // 1. Trả `session.Score == null && session.CompletedAt != null` cho nơi gọi.
        return session.Score == null && session.CompletedAt != null;
    }

    private async Task<QuizSessionAbandonedException> CreateAbandonedExceptionAsync(
        StudySession session)
    {
        // 1. Gọi `FindActiveQuizSessionAsync` và lưu kết quả vào `activeSession`.
        StudySession? activeSession = await FindActiveQuizSessionAsync(session);
        // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new QuizSessionAbandonedException(activeSession?.Id);
    }

    private async Task<bool> MatchesRequestedRetryAsync(
        StudySession activeSession,
        IReadOnlyList<QuizSessionQuestion> sourceQuestions,
        bool preserveDirections,
        StudySession sourceSession,
        QuizRetryKind? retryKind)
    {
        // 1. Kiểm tra `retryKind is null || activeSession.QuizRetrySourceSessionId != sour...` để chọn nhánh xử lý phù hợp.
        if (retryKind is null
            || activeSession.QuizRetrySourceSessionId != sourceSession.Id
            || activeSession.QuizRetryKind != retryKind
            || activeSession.QuizTimeLimitSeconds != sourceSession.QuizTimeLimitSeconds)
        {
            // 2. Trả `false` cho nơi gọi.
            return false;
        }

        // 3. Gọi `ToListAsync` và lưu kết quả vào `activeQuestions`.
        List<QuizSessionQuestion> activeQuestions = await _context.QuizSessionQuestions
            .AsNoTracking()
            .Where(question => question.StudySessionId == activeSession.Id)
            .ToListAsync();
        // 4. Kiểm tra `activeQuestions.Count != sourceQuestions.Count` để chọn nhánh xử lý phù hợp.
        if (activeQuestions.Count != sourceQuestions.Count)
        {
            // 5. Trả `false` cho nơi gọi.
            return false;
        }

        // 6. Gọi `ToDictionary` và lưu kết quả vào `sourceByCardId`.
        Dictionary<int, QuizSessionQuestion> sourceByCardId = sourceQuestions
            .ToDictionary(question => question.FlashcardId);
        // 7. Trả kết quả từ `All` cho nơi gọi.
        return activeQuestions.All(question => sourceByCardId.TryGetValue(
                question.FlashcardId,
                out QuizSessionQuestion? sourceQuestion)
            && (!preserveDirections || question.Direction == sourceQuestion.Direction));
    }

    private async Task<FlashcardSet> GetOwnedSetAsync(int setId, string userId)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == setId);
        // 2. Kiểm tra `set == null` để chọn nhánh xử lý phù hợp.
        if (set == null)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Bộ thẻ không tồn tại.")`.
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        }

        // 4. Kiểm tra `set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (set.UserId != userId)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền học bộ thẻ này.")`.
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");
        }

        // 6. Trả `set` cho nơi gọi.
        return set;
    }

    private static bool IsActiveQuizUniqueConflict(DbUpdateException exception)
    {
        // 1. Lặp qua phạm vi dữ liệu cần xử lý.
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            // 2. Kiểm tra `current.Message.Contains( "IX_StudySessions_UserId_FlashcardSetId_M...` để chọn nhánh xử lý phù hợp.
            if (current.Message.Contains(
                    "IX_StudySessions_UserId_FlashcardSetId_Mode",
                    StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains(
                    "StudySessions.UserId, StudySessions.FlashcardSetId, StudySessions.Mode",
                    StringComparison.OrdinalIgnoreCase))
            {
                // 3. Trả `true` cho nơi gọi.
                return true;
            }
        }

        // 4. Trả `false` cho nơi gọi.
        return false;
    }
}
