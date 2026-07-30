namespace ltwnc.Areas.Admin.Models;

public sealed class AdminDashboardViewModel
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public DateOnly Today { get; init; }
    public string? RangeError { get; init; }
    public int PendingReportCount { get; init; }
    public required AdminDashboardAiStatus AiStatus { get; init; }
    public IReadOnlyList<AdminDashboardActivityDay> Activity { get; init; } = [];
    public IReadOnlyList<AdminDashboardNewUserDay> NewUsers { get; init; } = [];
    public IReadOnlyList<AdminDashboardReportDay> Reports { get; init; } = [];

    public bool IsToday => From == Today && To == Today;
}

public sealed record AdminDashboardAiStatus(bool IsHealthy, string Title, string Detail);

public sealed record AdminDashboardActivityDay(DateOnly Date, int Completed, int Abandoned);

public sealed record AdminDashboardNewUserDay(DateOnly Date, int Count);

public sealed record AdminDashboardReportDay(DateOnly Date, int Count);
