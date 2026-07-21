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
        _context = context;
        _auditService = auditService;
        _timeProvider = timeProvider;
    }

    // Trả về một trang phiên học đã lọc, sắp xếp và phân trang phía máy chủ.
    public async Task<AdminStudySessionPage> SearchAsync(
        AdminStudySessionQuery query,
        CancellationToken cancellationToken = default)
    {
        int page = Math.Max(DefaultPage, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // Lọc và sắp xếp trực tiếp trên bảng phiên học; dữ liệu tài khoản (email/tên)
        // được lấy qua truy vấn con để EF Core dịch toàn bộ sang SQL ổn định.
        IQueryable<StudySession> sessions = _context.StudySessions.AsNoTracking();

        // Áp dụng lần lượt các bộ lọc; thứ tự rõ ràng giúp EF Core dịch SQL ổn định.
        sessions = ApplyUserFilter(sessions, query.UserId);
        sessions = ApplySearch(sessions, query.Search);
        sessions = ApplyModeFilter(sessions, query.Mode);
        sessions = ApplyStatusFilter(sessions, query.Status);
        sessions = ApplyTimeFilter(sessions, query.From, query.To);
        IQueryable<StudySession> sorted = ApplySort(sessions, query.Sort);

        int totalCount = await sorted.CountAsync(cancellationToken);

        // Bước 1: truy vấn dữ liệu thô của đúng một trang (mọi lọc/sắp xếp đều ở SQL).
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

        return new AdminStudySessionPage(items, totalCount, page, pageSize);
    }

    // Mở chi tiết phiên học: ghi audit truy cập nhạy cảm TRƯỚC, rồi mới truy vấn và trả dữ liệu.
    public async Task<AdminStudySessionDetails?> GetDetailsAsync(
        int sessionId,
        AdminStudyRecordAccessCommand access,
        CancellationToken cancellationToken = default)
    {
        ValidateAccess(access);

        StudySession? session = await _context.StudySessions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session == null)
        {
            return null;
        }

        AppUser? user = await _context.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == session.UserId, cancellationToken);

        string status = DeriveStatus(session.StartedAt, session.CompletedAt);

        // Ghi audit trước khi đọc phần dữ liệu còn lại.
        // RecordAsync ném lỗi khi không ghi được, nên dữ liệu nhạy cảm
        // không bao giờ rờ khỏi database nếu không có dấu vết kiểm toán.
        await RecordAccessAuditAsync(session, user, status, access, cancellationToken);

        string setTitle = await _context.FlashcardSets
            .AsNoTracking()
            .Where(set => set.Id == session.FlashcardSetId)
            .Select(set => set.Title)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        IReadOnlyList<AdminDictationAnswerRow> dictationAnswers =
            await LoadDictationAnswersAsync(session, cancellationToken);
        AdminMissionSummary? mission = await LoadMissionSummaryAsync(session, cancellationToken);
        AdminSetProgressSummary progress =
            await LoadSetProgressAsync(session, cancellationToken);

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
        if (string.IsNullOrWhiteSpace(userId))
        {
            return sessions;
        }

        string normalizedUserId = userId.Trim();
        return sessions.Where(session => session.UserId == normalizedUserId);
    }

    // Tìm kiếm an toàn trên email, tên đăng nhập hoặc mã tài khoản của ngườ học.
    private IQueryable<StudySession> ApplySearch(
        IQueryable<StudySession> sessions,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return sessions;
        }

        string term = search.Trim();
        // Chỉ giữ phiên của tài khoản khớp từ khóa; truy vấn con dịch sang EXISTS trong SQL.
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
        if (string.IsNullOrWhiteSpace(mode))
        {
            return sessions;
        }

        bool parsed = Enum.TryParse(mode.Trim(), ignoreCase: true, out StudyMode modeValue);
        if (!parsed || !Enum.IsDefined(modeValue))
        {
            return sessions;
        }

        return sessions.Where(session => session.Mode == modeValue);
    }

    // Lọc theo trạng thái suy ra; giá trị lạ được xem như "tất cả".
    private IQueryable<StudySession> ApplyStatusFilter(
        IQueryable<StudySession> sessions,
        string? status)
    {
        string normalizedStatus = NormalizeToken(status);
        if (normalizedStatus == StatusCompleted)
        {
            return sessions.Where(session => session.CompletedAt != null);
        }

        if (normalizedStatus == StatusInProgress)
        {
            return sessions.Where(session =>
                session.CompletedAt == null
                && session.StartedAt >= GetActiveThresholdUtc());
        }

        if (normalizedStatus == StatusAbandoned)
        {
            return sessions.Where(session =>
                session.CompletedAt == null
                && session.StartedAt < GetActiveThresholdUtc());
        }

        return sessions;
    }

    // Lọc theo khoảng ngày theo giờ Việt Nam, quy đổi sang ranh giới UTC trước khi truy vấn.
    private static IQueryable<StudySession> ApplyTimeFilter(
        IQueryable<StudySession> sessions,
        DateOnly? from,
        DateOnly? to)
    {
        if (from != null)
        {
            // Đầu ngày Việt Nam của mốc "từ" quy sang UTC.
            DateTime fromUtc = ConvertVietnamDayBoundaryToUtc(from.Value);
            sessions = sessions.Where(session => session.StartedAt >= fromUtc);
        }

        if (to != null)
        {
            // Dùng đầu ngày kế tiếp làm biên loại trừ để lấy trọn ngày "đến".
            DateTime toExclusiveUtc = ConvertVietnamDayBoundaryToUtc(to.Value.AddDays(1));
            sessions = sessions.Where(session => session.StartedAt < toExclusiveUtc);
        }

        return sessions;
    }

    // Sắp xếp theo danh sách khóa cố định; mặc định phiên mới nhất lên đầu.
    private IQueryable<StudySession> ApplySort(
        IQueryable<StudySession> sessions,
        string? sort)
    {
        string normalizedSort = NormalizeToken(sort);
        if (normalizedSort == "oldest")
        {
            return sessions
                .OrderBy(session => session.StartedAt)
                .ThenBy(session => session.Id);
        }

        if (normalizedSort == "score")
        {
            return sessions
                .OrderByDescending(session => session.Score)
                .ThenByDescending(session => session.StartedAt);
        }

        if (normalizedSort == "duration")
        {
            return sessions
                .OrderByDescending(session => session.DurationSeconds)
                .ThenByDescending(session => session.StartedAt);
        }

        if (normalizedSort == "user")
        {
            // Sắp theo email ngườ học qua truy vấn con để tránh join làm rối câu SQL.
            return sessions
                .OrderBy(session => _context.AppUsers
                    .Where(user => user.Id == session.UserId)
                    .Select(user => user.Email)
                    .FirstOrDefault())
                .ThenByDescending(session => session.StartedAt);
        }

        return sessions
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id);
    }

    // Tải câu trả lờ nghe chép chính tả; chỉ gọi khi phiên đúng chế độ Dictation.
    private async Task<IReadOnlyList<AdminDictationAnswerRow>> LoadDictationAnswersAsync(
        StudySession session,
        CancellationToken cancellationToken)
    {
        if (session.Mode != StudyMode.Dictation)
        {
            return Array.Empty<AdminDictationAnswerRow>();
        }

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
        if (session.Mode != StudyMode.EnglishMission)
        {
            return null;
        }

        // Ghi rõ namespace vì ltwnc.Services.EnglishMission trùng tên với entity.
        Models.Entities.EnglishMission? mission = await _context.EnglishMissions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.StudySessionId == session.Id, cancellationToken);
        if (mission == null)
        {
            return null;
        }

        int targetWordTotal = await _context.EnglishMissionTargetWords
            .AsNoTracking()
            .CountAsync(word => word.EnglishMissionId == mission.Id, cancellationToken);
        int targetWordUsed = await _context.EnglishMissionTargetWords
            .AsNoTracking()
            .CountAsync(
                word => word.EnglishMissionId == mission.Id && word.IsUsed,
                cancellationToken);

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
        int totalCards = await _context.Flashcards
            .AsNoTracking()
            .CountAsync(card => card.FlashcardSetId == session.FlashcardSetId, cancellationToken);

        // Tiến độ của người học trên các thẻ thuộc bộ của phiên.
        IQueryable<UserProgress> progressQuery =
            from progress in _context.UserProgresses.AsNoTracking()
            join card in _context.Flashcards.AsNoTracking()
                on progress.FlashcardId equals card.Id
            where progress.UserId == session.UserId
                && card.FlashcardSetId == session.FlashcardSetId
            select progress;

        int masteredCount = await progressQuery
            .CountAsync(item => item.Status == UserProgressStatus.Mastered, cancellationToken);
        int learningCount = await progressQuery
            .CountAsync(item => item.Status == UserProgressStatus.Learning, cancellationToken);

        // Thẻ chưa có dòng tiến độ được tính là chưa học.
        int unlearnedCount = totalCards - masteredCount - learningCount;
        if (unlearnedCount < 0)
        {
            unlearnedCount = 0;
        }

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
        var metadata = new Dictionary<string, string?>
        {
            ["scope"] = "learner-study-record",
            ["status"] = status
        };

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

        await _auditService.RecordAsync(entry, cancellationToken);
    }

    // Chặn sớm dữ liệu truy cập thiếu để không có lần xem nào thiếu lý do.
    private static void ValidateAccess(AdminStudyRecordAccessCommand access)
    {
        if (string.IsNullOrWhiteSpace(access.ActorUserId))
        {
            throw new InvalidOperationException("Không xác định được Quản trị viên đang xem.");
        }

        if (string.IsNullOrWhiteSpace(access.Reason))
        {
            throw new InvalidOperationException("Vui lòng nhập lý do trước khi xem hồ sơ học tập.");
        }

        if (access.Reason.Trim().Length > MaxReasonLength)
        {
            throw new InvalidOperationException("Lý do không được vượt quá 500 ký tự.");
        }
    }

    // Suy ra trạng thái hiển thị từ thờ điểm bắt đầu/hoàn thành và đồng hồ hiện tại.
    private string DeriveStatus(DateTime startedAtUtc, DateTime? completedAtUtc)
    {
        if (completedAtUtc != null)
        {
            return StatusCompleted;
        }

        if (startedAtUtc >= GetActiveThresholdUtc())
        {
            return StatusInProgress;
        }

        return StatusAbandoned;
    }

    // Ngưỡng "đang học": phiên bắt đầu sau mốc này mà chưa hoàn thành vẫn tính đang học.
    private DateTime GetActiveThresholdUtc()
    {
        return _timeProvider.GetUtcNow().UtcDateTime - ActiveSessionWindow;
    }

    // Quy đổi đầu ngày theo giờ Việt Nam sang UTC để so sánh với cột lưu UTC.
    private static DateTime ConvertVietnamDayBoundaryToUtc(DateOnly vietnamDay)
    {
        DateTime unspecified = vietnamDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, AdminTimeZone.Vietnam);
    }

    // Chuẩn hóa khóa lọc/sắp xếp từ query string.
    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

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
