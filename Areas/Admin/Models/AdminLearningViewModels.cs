using ltwnc.Models.Entities;
using ltwnc.Services.AdminStudyRecords;

namespace ltwnc.Areas.Admin.Models;

// View model trang danh sách phiên học: giữ lại toàn bộ bộ lọc để dựng lại URL phân trang.
public sealed class AdminLearningIndexViewModel
{
    public required IReadOnlyList<AdminLearningRowViewModel> Items { get; init; }
    public string? Search { get; init; }
    public string? UserId { get; init; }
    public string? Mode { get; init; }
    public string? Status { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public string? Sort { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }

    // Tổng trang tối thiểu là 1 để phần phân trang không rơi vào trạng thái rỗng.
    public int TotalPages
    {
        get
        {
            if (TotalCount == 0)
            {
                return 1;
            }

            return (int)Math.Ceiling(TotalCount / (double)PageSize);
        }
    }

    public bool HasPreviousPage
    {
        get
        {
            return Page > 1;
        }
    }

    public bool HasNextPage
    {
        get
        {
            return Page < TotalPages;
        }
    }
}

// Một hàng phiên học trong bảng danh sách, mọi giá trị đã được định dạng sẵn.
public sealed class AdminLearningRowViewModel
{
    public required int SessionId { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public required string ModeLabel { get; init; }
    public required string FlashcardSetTitle { get; init; }
    public required string ScoreDisplay { get; init; }
    public required string StartedDisplay { get; init; }
    public required string StartedRelativeDisplay { get; init; }
    public required string DurationDisplay { get; init; }
    public required string StatusLabel { get; init; }
    public required string StatusTone { get; init; }
}

// View model trang cổng lý do: chưa có bất kỳ dữ liệu phiên nào ở đây.
public sealed class AdminLearningReasonGateViewModel
{
    public required int SessionId { get; init; }
    public string? Reason { get; init; }
    public string? ErrorMessage { get; init; }
}

// View model trang chi tiết phiên học (chỉ đọc).
public sealed class AdminLearningDetailsViewModel
{
    public required int SessionId { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public required string ModeLabel { get; init; }
    public required int FlashcardSetId { get; init; }
    public required string FlashcardSetTitle { get; init; }
    public required string ScoreDisplay { get; init; }
    public required int PlannedItemCount { get; init; }
    public required string StartedDisplay { get; init; }
    public required string StartedRelativeDisplay { get; init; }
    public required string CompletedDisplay { get; init; }
    public required string DurationDisplay { get; init; }
    public required string StatusLabel { get; init; }
    public required string StatusTone { get; init; }
    public required string Reason { get; init; }
    public required IReadOnlyList<AdminDictationAnswerViewModel> DictationAnswers { get; init; }
    public AdminMissionSummaryViewModel? Mission { get; init; }
    public required AdminSetProgressViewModel SetProgress { get; init; }
}

// Một câu trả lờ nghe chép chính tả trong trang chi tiết.
public sealed class AdminDictationAnswerViewModel
{
    public required string CardFrontText { get; init; }
    public required string AnsweredText { get; init; }
    public required string ResultLabel { get; init; }
    public required string ResultTone { get; init; }
    public required string AnsweredAtDisplay { get; init; }
}

// Tóm tắt Nhiệm vụ tiếng Anh, không chứa nội dung hội thoại.
public sealed class AdminMissionSummaryViewModel
{
    public required string Topic { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public required string ScoreDisplay { get; init; }
    public required int TurnCount { get; init; }
    public required int TargetWordTotal { get; init; }
    public required int TargetWordUsed { get; init; }
}

// Ảnh chụp tiến độ của ngườ học trên bộ thẻ của phiên.
public sealed class AdminSetProgressViewModel
{
    public required int TotalCards { get; init; }
    public required int MasteredCount { get; init; }
    public required int LearningCount { get; init; }
    public required int UnlearnedCount { get; init; }
}

// Chuyển model tầng service sang view model đã định dạng sẵn cho Razor.
public static class AdminLearningViewModelMapper
{
    // Dựng view model danh sách, giữ nguyên bộ lọc để view dựng lại form và phân trang.
    public static AdminLearningIndexViewModel ToIndexViewModel(
        AdminStudySessionPage page,
        AdminStudySessionQuery query,
        DateTimeOffset now)
    {
        return new AdminLearningIndexViewModel
        {
            Items = page.Items
                .Select(row => ToRowViewModel(row, now))
                .ToArray(),
            Search = query.Search,
            UserId = query.UserId,
            Mode = query.Mode,
            Status = query.Status,
            From = FormatDateInput(query.From),
            To = FormatDateInput(query.To),
            Sort = query.Sort,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        };
    }

    // Dựng view model chi tiết; mọi giá trị thờ gian đã quy sang giờ Việt Nam.
    public static AdminLearningDetailsViewModel ToDetailsViewModel(
        AdminStudySessionDetails details,
        string reason,
        DateTimeOffset now)
    {
        string statusLabel = BuildStatusLabel(details.Status);
        string statusTone = BuildStatusTone(details.Status);

        AdminMissionSummaryViewModel? mission = null;
        if (details.Mission != null)
        {
            mission = new AdminMissionSummaryViewModel
            {
                Topic = details.Mission.Topic,
                Title = details.Mission.Title,
                Status = details.Mission.Status,
                ScoreDisplay = FormatScore(details.Mission.Score),
                TurnCount = details.Mission.TurnCount,
                TargetWordTotal = details.Mission.TargetWordTotal,
                TargetWordUsed = details.Mission.TargetWordUsed
            };
        }

        var setProgress = new AdminSetProgressViewModel
        {
            TotalCards = details.SetProgress.TotalCards,
            MasteredCount = details.SetProgress.MasteredCount,
            LearningCount = details.SetProgress.LearningCount,
            UnlearnedCount = details.SetProgress.UnlearnedCount
        };

        return new AdminLearningDetailsViewModel
        {
            SessionId = details.SessionId,
            UserName = details.UserName,
            Email = details.Email,
            ModeLabel = BuildModeLabel(details.Mode),
            FlashcardSetId = details.FlashcardSetId,
            FlashcardSetTitle = details.FlashcardSetTitle,
            ScoreDisplay = FormatScore(details.Score),
            PlannedItemCount = details.PlannedItemCount,
            StartedDisplay = FormatDateTime(details.StartedAtUtc),
            StartedRelativeDisplay = FormatRelative(now, details.StartedAtUtc),
            CompletedDisplay = FormatDateTime(details.CompletedAtUtc),
            DurationDisplay = FormatDuration(details.DurationSeconds),
            StatusLabel = statusLabel,
            StatusTone = statusTone,
            Reason = reason,
            DictationAnswers = details.DictationAnswers
                .Select(ToDictationAnswerViewModel)
                .ToArray(),
            Mission = mission,
            SetProgress = setProgress
        };
    }

    // Dựng view model cho trang cổng lý do, không mang theo dữ liệu phiên.
    public static AdminLearningReasonGateViewModel ToReasonGateViewModel(
        int sessionId,
        string? reason,
        string? errorMessage)
    {
        return new AdminLearningReasonGateViewModel
        {
            SessionId = sessionId,
            Reason = reason,
            ErrorMessage = errorMessage
        };
    }

    // Chuyển một hàng phiên học sang dữ liệu hiển thị trong bảng.
    private static AdminLearningRowViewModel ToRowViewModel(
        AdminStudySessionRow row,
        DateTimeOffset now)
    {
        return new AdminLearningRowViewModel
        {
            SessionId = row.SessionId,
            UserName = row.UserName,
            Email = row.Email,
            ModeLabel = BuildModeLabel(row.Mode),
            FlashcardSetTitle = row.FlashcardSetTitle,
            ScoreDisplay = FormatScore(row.Score),
            StartedDisplay = FormatDateTime(row.StartedAtUtc),
            StartedRelativeDisplay = FormatRelative(now, row.StartedAtUtc),
            DurationDisplay = FormatDuration(row.DurationSeconds),
            StatusLabel = BuildStatusLabel(row.Status),
            StatusTone = BuildStatusTone(row.Status)
        };
    }

    // Chuyển một câu trả lờ nghe chép sang dữ liệu hiển thị.
    private static AdminDictationAnswerViewModel ToDictationAnswerViewModel(
        AdminDictationAnswerRow answer)
    {
        string resultLabel;
        string resultTone;
        if (answer.IsCorrect)
        {
            resultLabel = "Đúng";
            resultTone = "success";
        }
        else
        {
            resultLabel = "Sai";
            resultTone = "danger";
        }

        return new AdminDictationAnswerViewModel
        {
            CardFrontText = answer.CardFrontText,
            AnsweredText = answer.AnsweredText,
            ResultLabel = resultLabel,
            ResultTone = resultTone,
            AnsweredAtDisplay = FormatDateTime(answer.AnsweredAtUtc)
        };
    }

    // Nhãn chế độ học bằng tiếng Việt cho từng giá trị enum.
    private static string BuildModeLabel(StudyMode mode)
    {
        if (mode == StudyMode.Flashcard)
        {
            return "Lật thẻ";
        }

        if (mode == StudyMode.Quiz)
        {
            return "Trắc nghiệm";
        }

        if (mode == StudyMode.Write)
        {
            return "Viết chính tả";
        }

        if (mode == StudyMode.Match)
        {
            return "Ghép đôi";
        }

        if (mode == StudyMode.Dictation)
        {
            return "Nghe chép chính tả";
        }

        if (mode == StudyMode.EnglishMission)
        {
            return "Nhiệm vụ tiếng Anh";
        }

        return mode.ToString();
    }

    // Nhãn trạng thái phiên bằng tiếng Việt.
    private static string BuildStatusLabel(string status)
    {
        if (status == AdminStudyRecordService.StatusCompleted)
        {
            return "Hoàn thành";
        }

        if (status == AdminStudyRecordService.StatusInProgress)
        {
            return "Đang học";
        }

        return "Bỏ dở";
    }

    // Tone CSS ổn định cho badge trạng thái phiên.
    private static string BuildStatusTone(string status)
    {
        if (status == AdminStudyRecordService.StatusCompleted)
        {
            return "success";
        }

        if (status == AdminStudyRecordService.StatusInProgress)
        {
            return "info";
        }

        return "warning";
    }

    // Hiển thị điểm hoặc dấu gạch khi chế độ học không chấm điểm.
    private static string FormatScore(int? score)
    {
        if (score == null)
        {
            return "—";
        }

        return score.Value.ToString();
    }

    // Định dạng DateTime UTC sang giờ Việt Nam đầy đủ ngày giờ, hoặc dấu gạch khi chưa có.
    private static string FormatDateTime(DateTime? valueUtc)
    {
        if (valueUtc == null)
        {
            return "—";
        }

        return AdminTimeZone.ToVietnamTime(valueUtc.Value).ToString("HH:mm:ss dd/MM/yyyy");
    }

    // Định dạng khoảng thờ lượng học thành chuỗi dễ đọc.
    private static string FormatDuration(int? durationSeconds)
    {
        if (durationSeconds == null)
        {
            return "—";
        }

        int totalSeconds = durationSeconds.Value;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        if (minutes == 0)
        {
            return $"{seconds} giây";
        }

        return $"{minutes} phút {seconds} giây";
    }

    // Thờ gian tương đối để Admin đọc nhanh, ví dụ "5 phút trước".
    private static string FormatRelative(DateTimeOffset now, DateTime valueUtc)
    {
        DateTimeOffset value = new DateTimeOffset(
            DateTime.SpecifyKind(valueUtc, DateTimeKind.Utc));
        TimeSpan elapsed = now - value;

        if (elapsed < TimeSpan.Zero)
        {
            return "vừa xong";
        }

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "vừa xong";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes} phút trước";
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            return $"{(int)elapsed.TotalHours} giờ trước";
        }

        return $"{(int)elapsed.TotalDays} ngày trước";
    }

    // Định dạng DateOnly về chuỗi yyyy-MM-dd cho thẻ input type="date".
    private static string? FormatDateInput(DateOnly? value)
    {
        if (value == null)
        {
            return null;
        }

        return value.Value.ToString("yyyy-MM-dd");
    }
}
