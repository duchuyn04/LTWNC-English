using ltwnc.Areas.Admin;
using ltwnc.Services.AdminAchievements;

namespace ltwnc.Areas.Admin.Models;

// View model trang Admin thanh tich, gom catalog va ket qua theo nguoi dung.
public sealed class AdminAchievementIndexViewModel
{
    public required IReadOnlyList<AdminAchievementDefinitionViewModel> Catalog { get; init; }
    public required IReadOnlyList<AdminAchievementUserResultViewModel> UserResults { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalUsers { get; init; }
    public int DefaultBatchSize { get; init; } = AdminAchievementService.DefaultBatchSize;

    // Tong so trang, toi thieu la 1 de UI khong roi vao trang 0.
    public int TotalPages
    {
        get
        {
            if (TotalUsers == 0)
            {
                return 1;
            }

            return (int)Math.Ceiling(TotalUsers / (double)PageSize);
        }
    }

    // Cho view biet co can hien nut trang truoc khong.
    public bool HasPreviousPage
    {
        get
        {
            return Page > 1;
        }
    }

    // Cho view biet co can hien nut trang sau khong.
    public bool HasNextPage
    {
        get
        {
            return Page < TotalPages;
        }
    }
}

// Mot dong catalog thanh tich trong bang read-only.
public sealed class AdminAchievementDefinitionViewModel
{
    public required string Code { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string MetricLabel { get; init; }
    public required int Target { get; init; }
    public required int RecipientCount { get; init; }
}

// Mot dong ket qua thanh tich theo nguoi dung.
public sealed class AdminAchievementUserResultViewModel
{
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public required int UnlockedCount { get; init; }
    public required int EligibleCount { get; init; }
    public required int MissingCount { get; init; }
    public required string LastUnlockedDisplay { get; init; }
    public required string MissingCodesDisplay { get; init; }
    public required string StatusLabel { get; init; }
    public required string StatusTone { get; init; }
}

// Input form dong bo lai cho mot nguoi dung.
public sealed class AdminAchievementResyncUserInputModel
{
    public string TargetUserId { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public bool Confirmed { get; set; }
}

// Input form dong bo lai toan he thong.
public sealed class AdminAchievementResyncAllInputModel
{
    public string? Reason { get; set; }
    public bool Confirmed { get; set; }
    public int BatchSize { get; set; } = AdminAchievementService.DefaultBatchSize;
}

// Chuyen model service sang view model da dinh dang san cho Razor.
public static class AdminAchievementViewModelMapper
{
    // Dung overview tu service de tao view model trang danh sach.
    public static AdminAchievementIndexViewModel ToIndexViewModel(
        AdminAchievementOverview overview,
        AdminAchievementQuery query)
    {
        return new AdminAchievementIndexViewModel
        {
            Catalog = overview.Catalog.Select(ToDefinitionViewModel).ToArray(),
            UserResults = overview.UserResults.Select(ToUserResultViewModel).ToArray(),
            Search = query.Search,
            Page = overview.Page,
            PageSize = overview.PageSize,
            TotalUsers = overview.TotalUsers
        };
    }

    // Dinh dang mot dinh nghia thanh tich tu catalog source code.
    private static AdminAchievementDefinitionViewModel ToDefinitionViewModel(
        AdminAchievementDefinitionSummary definition)
    {
        return new AdminAchievementDefinitionViewModel
        {
            Code = definition.Code,
            Title = definition.Title,
            Description = definition.Description,
            MetricLabel = BuildMetricLabel(definition.Metric),
            Target = definition.Target,
            RecipientCount = definition.RecipientCount
        };
    }

    // Dinh dang ket qua thanh tich theo nguoi dung.
    private static AdminAchievementUserResultViewModel ToUserResultViewModel(
        AdminAchievementUserResult result)
    {
        string statusLabel = "Day du";
        string statusTone = "success";
        if (result.MissingCount > 0)
        {
            statusLabel = "Can dong bo";
            statusTone = "warning";
        }

        return new AdminAchievementUserResultViewModel
        {
            UserId = result.UserId,
            UserName = result.UserName,
            Email = result.Email,
            UnlockedCount = result.UnlockedCount,
            EligibleCount = result.EligibleCount,
            MissingCount = result.MissingCount,
            LastUnlockedDisplay = FormatDateTime(result.LastUnlockedAtUtc),
            MissingCodesDisplay = FormatMissingCodes(result.MissingCodes),
            StatusLabel = statusLabel,
            StatusTone = statusTone
        };
    }

    // Doi ten metric ky thuat sang nhan tieng Viet cho Admin.
    private static string BuildMetricLabel(string metric)
    {
        if (metric == "CardsMastered")
        {
            return "The da thuoc";
        }

        if (metric == "FlashcardSessions")
        {
            return "Buoi Flashcard";
        }

        if (metric == "DictationSessions")
        {
            return "Buoi nghe chep";
        }

        if (metric == "DictationCorrectAnswers")
        {
            return "Cau nghe chep dung";
        }

        if (metric == "DictationPerfectSessions")
        {
            return "Buoi nghe chep 100 diem";
        }

        return metric;
    }

    // Dinh dang danh sach ma con thieu, gioi han de bang khong qua dai.
    private static string FormatMissingCodes(IReadOnlyList<string> missingCodes)
    {
        if (missingCodes.Count == 0)
        {
            return "Khong co";
        }

        return string.Join(", ", missingCodes.Take(4));
    }

    // Dinh dang thoi gian UTC sang gio Viet Nam, tra dau gach khi chua co thanh tich nao.
    private static string FormatDateTime(DateTime? valueUtc)
    {
        if (valueUtc == null)
        {
            return "-";
        }

        return AdminTimeZone.ToVietnamTime(valueUtc.Value).ToString("HH:mm dd/MM/yyyy");
    }
}
