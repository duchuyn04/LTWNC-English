using ltwnc.Services.AdminEnglishMissions;

namespace ltwnc.Areas.Admin.Models;

public sealed class AdminEnglishMissionIndexViewModel
{
    public required IReadOnlyList<AdminEnglishMissionRowViewModel> Items { get; init; }
    public string? Search { get; init; }
    public string? Topic { get; init; }
    public string? Status { get; init; }
    public string? Retention { get; init; }
    public string? Sort { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }

    // Tổng trang tối thiểu là 1 để phân trang ổn định khi danh sách rỗng.
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

public sealed class AdminEnglishMissionRowViewModel
{
    public required int MissionId { get; init; }
    public required int StudySessionId { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public required string FlashcardSetTitle { get; init; }
    public required string Topic { get; init; }
    public required string Title { get; init; }
    public required string StatusLabel { get; init; }
    public required string StatusTone { get; init; }
    public required string TurnCountDisplay { get; init; }
    public required string ScoreDisplay { get; init; }
    public required string CreatedDisplay { get; init; }
    public required string CompletedDisplay { get; init; }
    public required string RetentionDisplay { get; init; }
    public required string RetentionTone { get; init; }
}

public sealed class AdminEnglishMissionGateViewModel
{
    public required int MissionId { get; init; }
    public string? IncidentType { get; init; }
    public string? CaseReference { get; init; }
    public string? Reason { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class AdminEnglishMissionDetailsViewModel
{
    public required int MissionId { get; init; }
    public required int StudySessionId { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public required string FlashcardSetTitle { get; init; }
    public required string Topic { get; init; }
    public required string Title { get; init; }
    public required string Situation { get; init; }
    public required string NpcName { get; init; }
    public required string NpcRole { get; init; }
    public required string OpeningLine { get; init; }
    public required string StatusLabel { get; init; }
    public required string ScoreDisplay { get; init; }
    public required string CreatedDisplay { get; init; }
    public required string CompletedDisplay { get; init; }
    public required string RetentionDeadlineDisplay { get; init; }
    public required string IncidentTypeLabel { get; init; }
    public required string CaseReferenceDisplay { get; init; }
    public required string Reason { get; init; }
    public required IReadOnlyList<AdminEnglishMissionTargetWordViewModel> TargetWords { get; init; }
    public required IReadOnlyList<AdminEnglishMissionTurnViewModel> Turns { get; init; }
}

public sealed class AdminEnglishMissionTargetWordViewModel
{
    public required string Term { get; init; }
    public required string Definition { get; init; }
    public required string PartOfSpeechDisplay { get; init; }
    public required string UsedDisplay { get; init; }
}

public sealed class AdminEnglishMissionTurnViewModel
{
    public required int TurnNumber { get; init; }
    public required string UserText { get; init; }
    public required string NpcText { get; init; }
    public required string FeedbackDisplay { get; init; }
    public required string CorrectionDisplay { get; init; }
    public required string CorrectionExplanationDisplay { get; init; }
    public required string UsedWordsDisplay { get; init; }
    public required string AchievedGoalsDisplay { get; init; }
    public required string CreatedDisplay { get; init; }
}

public static class AdminEnglishMissionViewModelMapper
{
    // Dựng view model danh sách, giữ bộ lọc để Razor dựng lại URL phân trang.
    public static AdminEnglishMissionIndexViewModel ToIndexViewModel(
        AdminEnglishMissionPage page,
        AdminEnglishMissionQuery query)
    {
        return new AdminEnglishMissionIndexViewModel
        {
            Items = page.Items.Select(ToRowViewModel).ToArray(),
            Search = query.Search,
            Topic = query.Topic,
            Status = query.Status,
            Retention = query.Retention,
            Sort = query.Sort,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        };
    }

    // Dựng cổng vụ việc không chứa hội thoại.
    public static AdminEnglishMissionGateViewModel ToGateViewModel(
        int missionId,
        string? incidentType,
        string? caseReference,
        string? reason,
        string? errorMessage)
    {
        return new AdminEnglishMissionGateViewModel
        {
            MissionId = missionId,
            IncidentType = incidentType,
            CaseReference = caseReference,
            Reason = reason,
            ErrorMessage = errorMessage
        };
    }

    // Dựng chi tiết hội thoại đã được audit.
    public static AdminEnglishMissionDetailsViewModel ToDetailsViewModel(
        AdminEnglishMissionConversation conversation)
    {
        return new AdminEnglishMissionDetailsViewModel
        {
            MissionId = conversation.MissionId,
            StudySessionId = conversation.StudySessionId,
            UserName = conversation.UserName,
            Email = conversation.Email,
            FlashcardSetTitle = conversation.FlashcardSetTitle,
            Topic = conversation.Topic,
            Title = conversation.Title,
            Situation = DisplayOrDash(conversation.Situation),
            NpcName = conversation.NpcName,
            NpcRole = conversation.NpcRole,
            OpeningLine = DisplayOrDash(conversation.OpeningLine),
            StatusLabel = BuildStatusLabel(conversation.Status),
            ScoreDisplay = FormatScore(conversation.Score),
            CreatedDisplay = FormatDateTime(conversation.CreatedAtUtc),
            CompletedDisplay = FormatDateTime(conversation.CompletedAtUtc),
            RetentionDeadlineDisplay = FormatDateTime(conversation.RetentionDeadlineUtc),
            IncidentTypeLabel = BuildIncidentTypeLabel(conversation.IncidentType),
            CaseReferenceDisplay = DisplayOrDash(conversation.CaseReference),
            Reason = conversation.Reason,
            TargetWords = conversation.TargetWords
                .Select(ToTargetWordViewModel)
                .ToArray(),
            Turns = conversation.Turns
                .Select(ToTurnViewModel)
                .ToArray()
        };
    }

    // Dựng một dòng summary cho bảng.
    private static AdminEnglishMissionRowViewModel ToRowViewModel(
        AdminEnglishMissionRow row)
    {
        string retentionDisplay = "Còn lưu nội dung";
        string retentionTone = "success";
        if (!row.ConversationAvailable)
        {
            retentionDisplay = "Hết hạn nội dung";
            retentionTone = "warning";
        }
        else if (row.HasRetentionHold)
        {
            retentionDisplay = "Đang tạm giữ";
            retentionTone = "info";
        }

        return new AdminEnglishMissionRowViewModel
        {
            MissionId = row.MissionId,
            StudySessionId = row.StudySessionId,
            UserName = row.UserName,
            Email = row.Email,
            FlashcardSetTitle = row.FlashcardSetTitle,
            Topic = row.Topic,
            Title = row.Title,
            StatusLabel = BuildStatusLabel(row.Status),
            StatusTone = BuildStatusTone(row.Status),
            TurnCountDisplay = row.TurnCount.ToString(),
            ScoreDisplay = FormatScore(row.Score),
            CreatedDisplay = FormatDateTime(row.CreatedAtUtc),
            CompletedDisplay = FormatDateTime(row.CompletedAtUtc),
            RetentionDisplay = $"{retentionDisplay} đến {FormatDateTime(row.RetentionDeadlineUtc)}",
            RetentionTone = retentionTone
        };
    }

    // Dựng từ mục tiêu cho trang chi tiết.
    private static AdminEnglishMissionTargetWordViewModel ToTargetWordViewModel(
        AdminEnglishMissionTargetWordRow row)
    {
        string usedDisplay = "Chưa dùng";
        if (row.IsUsed)
        {
            usedDisplay = "Đã dùng";
            if (row.FirstUsedTurn != null)
            {
                usedDisplay = $"Đã dùng ở lượt {row.FirstUsedTurn.Value}";
            }
        }

        return new AdminEnglishMissionTargetWordViewModel
        {
            Term = row.Term,
            Definition = row.Definition,
            PartOfSpeechDisplay = DisplayOrDash(row.PartOfSpeech),
            UsedDisplay = usedDisplay
        };
    }

    // Dựng một lượt hội thoại đã loại metadata vận hành nội bộ.
    private static AdminEnglishMissionTurnViewModel ToTurnViewModel(
        AdminEnglishMissionTurnRow row)
    {
        return new AdminEnglishMissionTurnViewModel
        {
            TurnNumber = row.TurnNumber,
            UserText = DisplayOrDash(row.UserText),
            NpcText = DisplayOrDash(row.NpcText),
            FeedbackDisplay = DisplayOrDash(row.FeedbackVi),
            CorrectionDisplay = DisplayOrDash(row.CorrectionEn),
            CorrectionExplanationDisplay = DisplayOrDash(row.CorrectionExplanationVi),
            UsedWordsDisplay = DisplayOrDash(row.UsedWordsDisplay),
            AchievedGoalsDisplay = DisplayOrDash(row.AchievedGoalsDisplay),
            CreatedDisplay = FormatDateTime(row.CreatedAtUtc)
        };
    }

    // Nhãn trạng thái mission.
    private static string BuildStatusLabel(string status)
    {
        if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return "Hoàn thành";
        }

        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return "Đang hoạt động";
        }

        return status;
    }

    // Tone CSS cho trạng thái mission.
    private static string BuildStatusTone(string status)
    {
        if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return "success";
        }

        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return "info";
        }

        return "warning";
    }

    // Nhãn loại vụ việc cho cổng truy cập hội thoại.
    private static string BuildIncidentTypeLabel(string incidentType)
    {
        if (string.Equals(incidentType, "support", StringComparison.OrdinalIgnoreCase))
        {
            return "Hỗ trợ";
        }

        if (string.Equals(incidentType, "report", StringComparison.OrdinalIgnoreCase))
        {
            return "Báo cáo";
        }

        if (string.Equals(incidentType, "safety", StringComparison.OrdinalIgnoreCase))
        {
            return "An toàn";
        }

        if (string.Equals(incidentType, "quality", StringComparison.OrdinalIgnoreCase))
        {
            return "Chất lượng";
        }

        return incidentType;
    }

    // Hiển thị điểm hoặc dấu gạch.
    private static string FormatScore(int? score)
    {
        if (score == null)
        {
            return "—";
        }

        return score.Value.ToString();
    }

    // Định dạng thời gian UTC sang giờ Việt Nam.
    private static string FormatDateTime(DateTime? valueUtc)
    {
        if (valueUtc == null)
        {
            return "—";
        }

        return AdminTimeZone.ToVietnamTime(valueUtc.Value).ToString("HH:mm dd/MM/yyyy");
    }

    // Chuẩn hóa chuỗi rỗng khi hiển thị.
    private static string DisplayOrDash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "—";
        }

        return value;
    }
}
