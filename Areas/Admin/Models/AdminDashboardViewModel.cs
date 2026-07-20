namespace ltwnc.Areas.Admin.Models;

public sealed class AdminDashboardViewModel
{
    public int Days { get; init; }
    public IReadOnlyList<int> AllowedDays { get; init; } = [7, 30, 90];
    public DateTimeOffset PeriodStartVietnam { get; init; }
    public DateTimeOffset PeriodEndVietnam { get; init; }
    public DateTimeOffset GeneratedAtVietnam { get; init; }
    public IReadOnlyList<AdminDashboardKpiCardViewModel> Kpis { get; init; } = [];
}

public sealed class AdminDashboardKpiCardViewModel
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string Comparison { get; init; } = string.Empty;
    public string Tone { get; init; } = "neutral";
    public string Icon { get; init; } = "ph-chart-line-up";
    public string ActionLabel { get; init; } = string.Empty;
    public string ActionHref { get; init; } = string.Empty;
}
