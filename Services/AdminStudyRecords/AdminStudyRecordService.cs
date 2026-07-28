using ltwnc.Areas.Admin;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.AdminStudyRecords;

// Truy vấn hồ sơ học tập chỉ đọc cho Admin.
// Mọi truy vấn dùng AsNoTracking và không có bất kỳ lệnh ghi nào vào dữ liệu học tập.
public sealed class AdminStudyRecordService : IAdminStudyRecordService
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    // Giá trị trạng thái chuẩn dùng chung giữa service, controller và view.
    public const string StatusCompleted = "completed";
    public const string StatusInProgress = "inprogress";
    public const string StatusAbandoned = "abandoned";

    // Phiên chưa hoàn thành nhưng mới bắt đầu trong 30 phút được xem là đang học;
    // quá 30 phút mà chưa hoàn thành được tính là bỏ dở (khớp quy tắc KPI của dashboard).
    private static readonly TimeSpan ActiveSessionWindow = TimeSpan.FromMinutes(30);

    private const int MaxReasonLength = 500;

    private readonly AppDbContext _context;
    private readonly IAdminAuditService _auditService;
    private readonly TimeProvider _timeProvider;

    // Nhận DbContext, cổng kiểm toán và đồng hồ (để kiểm thử điều khiển thờ gian).
    public AdminStudyRecordService(
        AppDbContext context,
        IAdminAuditService auditService,
        TimeProvider timeProvider)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_auditService` để các phương thức khác sử dụng.
        _auditService = auditService;
        // 3. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
    }

    // Trả về một trang phiên học đã lọc, sắp xếp và phân trang phía máy chủ.
    public async Task<AdminStudySessionPage> SearchAsync(
        AdminStudySessionQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `Max` và lưu kết quả vào `page`.
        int page = Math.Max(DefaultPage, query.Page);
        // 2. Gọi `Clamp` và lưu kết quả vào `pageSize`.
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // Lọc và sắp xếp trực tiếp trên bảng phiên học; dữ liệu tài khoản (email/tên)
        // được lấy qua truy vấn con để EF Core dịch toàn bộ sang SQL ổn định.
        // 3. Gọi `AsNoTracking` và lưu kết quả vào `sessions`.
        IQueryable<StudySession> sessions = _context.StudySessions.AsNoTracking();

        // Áp dụng lần lượt các bộ lọc; thứ tự rõ ràng giúp EF Core dịch SQL ổn định.
        // 4. Cập nhật `sessions` bằng giá trị mới.
        sessions = ApplyUserFilter(sessions, query.UserId);
        // 5. Cập nhật `sessions` bằng giá trị mới.
        sessions = ApplySearch(sessions, query.Search);
        // 6. Cập nhật `sessions` bằng giá trị mới.
        sessions = ApplyModeFilter(sessions, query.Mode);
        // 7. Cập nhật `sessions` bằng giá trị mới.
        sessions = ApplyStatusFilter(sessions, query.Status);
        // 8. Cập nhật `sessions` bằng giá trị mới.
        sessions = ApplyTimeFilter(sessions, query.From, query.To);
        // 9. Gọi `ApplySort` và lưu kết quả vào `sorted`.
        IQueryable<StudySession> sorted = ApplySort(sessions, query.Sort);

        // 10. Gọi `CountAsync` và lưu kết quả vào `totalCount`.
        int totalCount = await sorted.CountAsync(cancellationToken);

        // Bước 1: truy vấn dữ liệu thô của đúng một trang (mọi lọc/sắp xếp đều ở SQL).
        // 11. Gọi `ToListAsync` và lưu kết quả vào `rawItems`.
        List<RawSessionRow> rawItems = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(session => new RawSessionRow(
                session.Id,
                session.UserId,
                // Tên đăng nhập và email lấy bằng truy vấn con, không cần Include.
                _context.AppUsers
                    .Where(user => user.Id == session.UserId)
                    .Select(user => user.UserName)
                    .FirstOrDefault() ?? string.Empty,
                _context.AppUsers
                    .Where(user => user.Id == session.UserId)
                    .Select(user => user.Email)
                    .FirstOrDefault() ?? string.Empty,
                session.Mode,
                // Lấy tiêu đề bộ thẻ bằng truy vấn con để không cần Include toàn bộ entity.
                _context.FlashcardSets
                    .Where(set => set.Id == session.FlashcardSetId)
                    .Select(set => set.Title)
                    .FirstOrDefault() ?? string.Empty,
                session.Score,
                session.PlannedItemCount,
                session.StartedAt,
                session.CompletedAt,
                session.DurationSeconds))
            .ToListAsync(cancellationToken);

        // Bước 2: suy ra trạng thái hiển thị trên bộ nhớ để giữ truy vấn SQL đơn giản.
        // 12. Gọi `ToList` và lưu kết quả vào `items`.
        List<AdminStudySessionRow> items = rawItems
            .Select(raw => new AdminStudySessionRow(
                raw.SessionId,
                raw.UserId,
                raw.UserName,
                raw.Email,
                raw.Mode,
                raw.FlashcardSetTitle,
                raw.Score,
                raw.PlannedItemCount,
                raw.StartedAtUtc,
                raw.CompletedAtUtc,
                raw.DurationSeconds,
                DeriveStatus(raw.StartedAtUtc, raw.CompletedAtUtc)))
            .ToList();

        // 13. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminStudySessionPage(items, totalCount, page, pageSize);
    }

    // Mở chi tiết phiên học: ghi audit truy cập nhạy cảm TRƯỚC, rồi mới truy vấn và trả dữ liệu.
    public async Task<AdminStudySessionDetails?> GetDetailsAsync(
        int sessionId,
        AdminStudyRecordAccessCommand access,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `ValidateAccess` để thực hiện bước nghiệp vụ này.
        ValidateAccess(access);

        // 2. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `session`.
        StudySession? session = await _context.StudySessions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        // 3. Kiểm tra `session == null` để chọn nhánh xử lý phù hợp.
        if (session == null)
        {
            // 4. Trả `null` cho nơi gọi.
            return null;
        }

        // 5. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `user`.
        AppUser? user = await _context.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == session.UserId, cancellationToken);

        // 6. Gọi `DeriveStatus` và lưu kết quả vào `status`.
        string status = DeriveStatus(session.StartedAt, session.CompletedAt);

        // Ghi audit trước khi đọc phần dữ liệu còn lại.
        // RecordAsync ném lỗi khi không ghi được, nên dữ liệu nhạy cảm
        // không bao giờ rờ khỏi database nếu không có dấu vết kiểm toán.
        // 7. Gọi `RecordAccessAuditAsync` để thực hiện bước nghiệp vụ này.
        await RecordAccessAuditAsync(session, user, status, access, cancellationToken);

        // 8. Tính giá trị và lưu vào `setTitle` để dùng ở bước tiếp theo.
        string setTitle = await _context.FlashcardSets
            .AsNoTracking()
            .Where(set => set.Id == session.FlashcardSetId)
            .Select(set => set.Title)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        // 9. Gọi `LoadDictationAnswersAsync` và lưu kết quả vào `dictationAnswers`.
        IReadOnlyList<AdminDictationAnswerRow> dictationAnswers =
            await LoadDictationAnswersAsync(session, cancellationToken);
        // 10. Gọi `LoadMissionSummaryAsync` và lưu kết quả vào `mission`.
        AdminMissionSummary? mission = await LoadMissionSummaryAsync(session, cancellationToken);
        // 11. Gọi `LoadSetProgressAsync` và lưu kết quả vào `progress`.
        AdminSetProgressSummary progress =
            await LoadSetProgressAsync(session, cancellationToken);

        // 12. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminStudySessionDetails(
            SessionId: session.Id,
            UserId: session.UserId,
            UserName: user?.UserName ?? string.Empty,
            Email: user?.Email ?? string.Empty,
            Mode: session.Mode,
            FlashcardSetId: session.FlashcardSetId,
            FlashcardSetTitle: setTitle,
            Score: session.Score,
            PlannedItemCount: session.PlannedItemCount,
            StartedAtUtc: session.StartedAt,
            CompletedAtUtc: session.CompletedAt,
            DurationSeconds: session.DurationSeconds,
            Status: status,
            DictationAnswers: dictationAnswers,
            Mission: mission,
            SetProgress: progress);
    }

    // Lọc theo đúng một ngườ dùng khi Admin đi từ trang chi tiết tài khoản sang.
    private static IQueryable<StudySession> ApplyUserFilter(
        IQueryable<StudySession> sessions,
        string? userId)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(userId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(userId))
        {
            // 2. Trả `sessions` cho nơi gọi.
            return sessions;
        }

        // 3. Gọi `Trim` và lưu kết quả vào `normalizedUserId`.
        string normalizedUserId = userId.Trim();
        // 4. Trả kết quả từ `Where` cho nơi gọi.
        return sessions.Where(session => session.UserId == normalizedUserId);
    }

    // Tìm kiếm an toàn trên email, tên đăng nhập hoặc mã tài khoản của ngườ học.
    private IQueryable<StudySession> ApplySearch(
        IQueryable<StudySession> sessions,
        string? search)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(search)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(search))
        {
            // 2. Trả `sessions` cho nơi gọi.
            return sessions;
        }

        // 3. Gọi `Trim` và lưu kết quả vào `term`.
        string term = search.Trim();
        // Chỉ giữ phiên của tài khoản khớp từ khóa; truy vấn con dịch sang EXISTS trong SQL.
        // 4. Trả kết quả từ `Where` cho nơi gọi.
        return sessions.Where(session => _context.AppUsers.Any(user =>
            user.Id == session.UserId
            && ((user.Email != null && user.Email.Contains(term))
                || (user.UserName != null && user.UserName.Contains(term))
                || user.Id.Contains(term))));
    }

    // Lọc theo chế độ học; chỉ chấp nhận đúng tên enum để tránh giá trị tùy ý.
    private static IQueryable<StudySession> ApplyModeFilter(
        IQueryable<StudySession> sessions,
        string? mode)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(mode)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(mode))
        {
            // 2. Trả `sessions` cho nơi gọi.
            return sessions;
        }

        // 3. Gọi `TryParse` và lưu kết quả vào `parsed`.
        bool parsed = Enum.TryParse(mode.Trim(), ignoreCase: true, out StudyMode modeValue);
        // 4. Kiểm tra `!parsed || !Enum.IsDefined(modeValue)` để chọn nhánh xử lý phù hợp.
        if (!parsed || !Enum.IsDefined(modeValue))
        {
            // 5. Trả `sessions` cho nơi gọi.
            return sessions;
        }

        // 6. Trả kết quả từ `Where` cho nơi gọi.
        return sessions.Where(session => session.Mode == modeValue);
    }

    // Lọc theo trạng thái suy ra; giá trị lạ được xem như "tất cả".
    private IQueryable<StudySession> ApplyStatusFilter(
        IQueryable<StudySession> sessions,
        string? status)
    {
        // 1. Gọi `NormalizeToken` và lưu kết quả vào `normalizedStatus`.
        string normalizedStatus = NormalizeToken(status);
        // 2. Kiểm tra `normalizedStatus == StatusCompleted` để chọn nhánh xử lý phù hợp.
        if (normalizedStatus == StatusCompleted)
        {
            // 3. Trả kết quả từ `Where` cho nơi gọi.
            return sessions.Where(session => session.CompletedAt != null);
        }

        // 4. Kiểm tra `normalizedStatus == StatusInProgress` để chọn nhánh xử lý phù hợp.
        if (normalizedStatus == StatusInProgress)
        {
            // 5. Trả kết quả từ `Where` cho nơi gọi.
            return sessions.Where(session =>
                session.CompletedAt == null
                && session.StartedAt >= GetActiveThresholdUtc());
        }

        // 6. Kiểm tra `normalizedStatus == StatusAbandoned` để chọn nhánh xử lý phù hợp.
        if (normalizedStatus == StatusAbandoned)
        {
            // 7. Trả kết quả từ `Where` cho nơi gọi.
            return sessions.Where(session =>
                session.CompletedAt == null
                && session.StartedAt < GetActiveThresholdUtc());
        }

        // 8. Trả `sessions` cho nơi gọi.
        return sessions;
    }

    // Lọc theo khoảng ngày theo giờ Việt Nam, quy đổi sang ranh giới UTC trước khi truy vấn.
    private static IQueryable<StudySession> ApplyTimeFilter(
        IQueryable<StudySession> sessions,
        DateOnly? from,
        DateOnly? to)
    {
        // 1. Kiểm tra `from != null` để chọn nhánh xử lý phù hợp.
        if (from != null)
        {
            // Đầu ngày Việt Nam của mốc "từ" quy sang UTC.
            // 2. Gọi `ConvertVietnamDayBoundaryToUtc` và lưu kết quả vào `fromUtc`.
            DateTime fromUtc = ConvertVietnamDayBoundaryToUtc(from.Value);
            // 3. Cập nhật `sessions` bằng giá trị mới.
            sessions = sessions.Where(session => session.StartedAt >= fromUtc);
        }

        // 4. Kiểm tra `to != null` để chọn nhánh xử lý phù hợp.
        if (to != null)
        {
            // Dùng đầu ngày kế tiếp làm biên loại trừ để lấy trọn ngày "đến".
            // 5. Gọi `ConvertVietnamDayBoundaryToUtc` và lưu kết quả vào `toExclusiveUtc`.
            DateTime toExclusiveUtc = ConvertVietnamDayBoundaryToUtc(to.Value.AddDays(1));
            // 6. Cập nhật `sessions` bằng giá trị mới.
            sessions = sessions.Where(session => session.StartedAt < toExclusiveUtc);
        }

        // 7. Trả `sessions` cho nơi gọi.
        return sessions;
    }

    // Sắp xếp theo danh sách khóa cố định; mặc định phiên mới nhất lên đầu.
    private IQueryable<StudySession> ApplySort(
        IQueryable<StudySession> sessions,
        string? sort)
    {
        // 1. Gọi `NormalizeToken` và lưu kết quả vào `normalizedSort`.
        string normalizedSort = NormalizeToken(sort);
        // 2. Kiểm tra `normalizedSort == "oldest"` để chọn nhánh xử lý phù hợp.
        if (normalizedSort == "oldest")
        {
            // 3. Trả kết quả từ `ThenBy` cho nơi gọi.
            return sessions
                .OrderBy(session => session.StartedAt)
                .ThenBy(session => session.Id);
        }

        // 4. Kiểm tra `normalizedSort == "score"` để chọn nhánh xử lý phù hợp.
        if (normalizedSort == "score")
        {
            // 5. Trả kết quả từ `ThenByDescending` cho nơi gọi.
            return sessions
                .OrderByDescending(session => session.Score)
                .ThenByDescending(session => session.StartedAt);
        }

        // 6. Kiểm tra `normalizedSort == "duration"` để chọn nhánh xử lý phù hợp.
        if (normalizedSort == "duration")
        {
            // 7. Trả kết quả từ `ThenByDescending` cho nơi gọi.
            return sessions
                .OrderByDescending(session => session.DurationSeconds)
                .ThenByDescending(session => session.StartedAt);
        }

        // 8. Kiểm tra `normalizedSort == "user"` để chọn nhánh xử lý phù hợp.
        if (normalizedSort == "user")
        {
            // Sắp theo email ngườ học qua truy vấn con để tránh join làm rối câu SQL.
            // 9. Trả kết quả từ `ThenByDescending` cho nơi gọi.
            return sessions
                .OrderBy(session => _context.AppUsers
                    .Where(user => user.Id == session.UserId)
                    .Select(user => user.Email)
                    .FirstOrDefault())
                .ThenByDescending(session => session.StartedAt);
        }

        // 10. Trả kết quả từ `ThenByDescending` cho nơi gọi.
        return sessions
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id);
    }

    // Tải câu trả lờ nghe chép chính tả; chỉ gọi khi phiên đúng chế độ Dictation.
    private async Task<IReadOnlyList<AdminDictationAnswerRow>> LoadDictationAnswersAsync(
        StudySession session,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra `session.Mode != StudyMode.Dictation` để chọn nhánh xử lý phù hợp.
        if (session.Mode != StudyMode.Dictation)
        {
            // 2. Trả kết quả từ `Empty` cho nơi gọi.
            return Array.Empty<AdminDictationAnswerRow>();
        }

        // 3. Trả kết quả từ `ToListAsync` cho nơi gọi.
        return await (
            from detail in _context.DictationSessionDetails.AsNoTracking()
            join card in _context.Flashcards.AsNoTracking()
                on detail.FlashcardId equals card.Id
            where detail.StudySessionId == session.Id
            orderby detail.CreatedAt
            select new AdminDictationAnswerRow(
                card.FrontText,
                detail.AnsweredText,
                detail.IsCorrect,
                detail.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    // Tải tóm tắt Nhiệm vụ tiếng Anh; không trả nội dung hội thoại cho hồ sơ học tập.
    private async Task<AdminMissionSummary?> LoadMissionSummaryAsync(
        StudySession session,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra `session.Mode != StudyMode.EnglishMission` để chọn nhánh xử lý phù hợp.
        if (session.Mode != StudyMode.EnglishMission)
        {
            // 2. Trả `null` cho nơi gọi.
            return null;
        }

        // Ghi rõ namespace vì ltwnc.Services.EnglishMission trùng tên với entity.
        // 3. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `mission`.
        Models.Entities.EnglishMission? mission = await _context.EnglishMissions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.StudySessionId == session.Id, cancellationToken);
        // 4. Kiểm tra `mission == null` để chọn nhánh xử lý phù hợp.
        if (mission == null)
        {
            // 5. Trả `null` cho nơi gọi.
            return null;
        }

        // 6. Gọi `CountAsync` và lưu kết quả vào `targetWordTotal`.
        int targetWordTotal = await _context.EnglishMissionTargetWords
            .AsNoTracking()
            .CountAsync(word => word.EnglishMissionId == mission.Id, cancellationToken);
        // 7. Gọi `CountAsync` và lưu kết quả vào `targetWordUsed`.
        int targetWordUsed = await _context.EnglishMissionTargetWords
            .AsNoTracking()
            .CountAsync(
                word => word.EnglishMissionId == mission.Id && word.IsUsed,
                cancellationToken);

        // 8. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminMissionSummary(
            mission.Topic,
            mission.Title,
            mission.Status,
            mission.Score,
            mission.TurnCount,
            targetWordTotal,
            targetWordUsed);
    }

    // Ảnh chụp tiến độ hiện tại của ngườ học trên bộ thẻ của phiên (phù hợp chế độ lật thẻ).
    private async Task<AdminSetProgressSummary> LoadSetProgressAsync(
        StudySession session,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `CountAsync` và lưu kết quả vào `totalCards`.
        int totalCards = await _context.Flashcards
            .AsNoTracking()
            .CountAsync(card => card.FlashcardSetId == session.FlashcardSetId, cancellationToken);

        // Tiến độ của người học trên các thẻ thuộc bộ của phiên.
        // 2. Tính giá trị và lưu vào `progressQuery` để dùng ở bước tiếp theo.
        IQueryable<UserProgress> progressQuery =
            from progress in _context.UserProgresses.AsNoTracking()
            join card in _context.Flashcards.AsNoTracking()
                on progress.FlashcardId equals card.Id
            where progress.UserId == session.UserId
                && card.FlashcardSetId == session.FlashcardSetId
            select progress;

        // 3. Gọi `CountAsync` và lưu kết quả vào `masteredCount`.
        int masteredCount = await progressQuery
            .CountAsync(item => item.Status == UserProgressStatus.Mastered, cancellationToken);
        // 4. Gọi `CountAsync` và lưu kết quả vào `learningCount`.
        int learningCount = await progressQuery
            .CountAsync(item => item.Status == UserProgressStatus.Learning, cancellationToken);

        // Thẻ chưa có dòng tiến độ được tính là chưa học.
        // 5. Tính giá trị và lưu vào `unlearnedCount` để dùng ở bước tiếp theo.
        int unlearnedCount = totalCards - masteredCount - learningCount;
        // 6. Kiểm tra `unlearnedCount < 0` để chọn nhánh xử lý phù hợp.
        if (unlearnedCount < 0)
        {
            // 7. Cập nhật `unlearnedCount` bằng giá trị mới.
            unlearnedCount = 0;
        }

        // 8. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminSetProgressSummary(
            totalCards,
            masteredCount,
            learningCount,
            unlearnedCount);
    }

    // Ghi Bản ghi kiểm toán truy cập nhạy cảm; ném lỗi khi ghi không thành công.
    private async Task RecordAccessAuditAsync(
        StudySession session,
        AppUser? user,
        string status,
        AdminStudyRecordAccessCommand access,
        CancellationToken cancellationToken)
    {
        // Metadata chỉ dùng các khóa nằm trong danh sách cho phép của AdminAuditMetadata.
        // 1. Khởi tạo `metadata` với dữ liệu ban đầu cần thiết.
        var metadata = new Dictionary<string, string?>
        {
            ["scope"] = "learner-study-record",
            ["status"] = status
        };

        // 2. Khởi tạo `entry` với dữ liệu ban đầu cần thiết.
        var entry = new AdminAuditEntry(
            ActorUserId: access.ActorUserId,
            ActorDisplay: access.ActorDisplay,
            Action: AdminAuditActions.StudyRecordsViewDetails,
            Outcome: AdminAuditOutcome.Success,
            TargetType: "StudySession",
            TargetId: session.Id.ToString(),
            Reason: access.Reason.Trim(),
            CorrelationId: access.CorrelationId,
            Metadata: metadata);

        // 3. Gọi `RecordAsync` để thực hiện bước nghiệp vụ này.
        await _auditService.RecordAsync(entry, cancellationToken);
    }

    // Chặn sớm dữ liệu truy cập thiếu để không có lần xem nào thiếu lý do.
    private static void ValidateAccess(AdminStudyRecordAccessCommand access)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(access.ActorUserId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(access.ActorUserId))
        {
            // 2. Dừng xử lý và phát sinh lỗi `new InvalidOperationException("Không xác định được Quản trị viên đa...`.
            throw new InvalidOperationException("Không xác định được Quản trị viên đang xem.");
        }

        // 3. Kiểm tra `string.IsNullOrWhiteSpace(access.Reason)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(access.Reason))
        {
            // 4. Dừng xử lý và phát sinh lỗi `new InvalidOperationException("Vui lòng nhập lý do trước khi xem hồ...`.
            throw new InvalidOperationException("Vui lòng nhập lý do trước khi xem hồ sơ học tập.");
        }

        // 5. Kiểm tra `access.Reason.Trim().Length > MaxReasonLength` để chọn nhánh xử lý phù hợp.
        if (access.Reason.Trim().Length > MaxReasonLength)
        {
            // 6. Dừng xử lý và phát sinh lỗi `new InvalidOperationException("Lý do không được vượt quá 500 ký tự.")`.
            throw new InvalidOperationException("Lý do không được vượt quá 500 ký tự.");
        }
    }

    // Suy ra trạng thái hiển thị từ thờ điểm bắt đầu/hoàn thành và đồng hồ hiện tại.
    private string DeriveStatus(DateTime startedAtUtc, DateTime? completedAtUtc)
    {
        // 1. Kiểm tra `completedAtUtc != null` để chọn nhánh xử lý phù hợp.
        if (completedAtUtc != null)
        {
            // 2. Trả `StatusCompleted` cho nơi gọi.
            return StatusCompleted;
        }

        // 3. Kiểm tra `startedAtUtc >= GetActiveThresholdUtc()` để chọn nhánh xử lý phù hợp.
        if (startedAtUtc >= GetActiveThresholdUtc())
        {
            // 4. Trả `StatusInProgress` cho nơi gọi.
            return StatusInProgress;
        }

        // 5. Trả `StatusAbandoned` cho nơi gọi.
        return StatusAbandoned;
    }

    // Ngưỡng "đang học": phiên bắt đầu sau mốc này mà chưa hoàn thành vẫn tính đang học.
    private DateTime GetActiveThresholdUtc()
    {
        // 1. Trả `_timeProvider.GetUtcNow().UtcDateTime - ActiveSessionWindow` cho nơi gọi.
        return _timeProvider.GetUtcNow().UtcDateTime - ActiveSessionWindow;
    }

    // Quy đổi đầu ngày theo giờ Việt Nam sang UTC để so sánh với cột lưu UTC.
    private static DateTime ConvertVietnamDayBoundaryToUtc(DateOnly vietnamDay)
    {
        // 1. Gọi `ToDateTime` và lưu kết quả vào `unspecified`.
        DateTime unspecified = vietnamDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // 2. Trả kết quả từ `ConvertTimeToUtc` cho nơi gọi.
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, AdminTimeZone.Vietnam);
    }

    // Chuẩn hóa khóa lọc/sắp xếp từ query string.
    private static string NormalizeToken(string? value)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(value)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(value))
        {
            // 2. Trả `string.Empty` cho nơi gọi.
            return string.Empty;
        }

        // 3. Trả kết quả từ `ToLowerInvariant` cho nơi gọi.
        return value.Trim().ToLowerInvariant();
    }

    // Dữ liệu thô của một hàng danh sách ngay sau truy vấn SQL,
    // trước khi tầng ứng dụng suy ra trạng thái hiển thị.
    private sealed record RawSessionRow(
        int SessionId,
        string UserId,
        string UserName,
        string Email,
        StudyMode Mode,
        string FlashcardSetTitle,
        int? Score,
        int PlannedItemCount,
        DateTime StartedAtUtc,
        DateTime? CompletedAtUtc,
        int? DurationSeconds);
}
