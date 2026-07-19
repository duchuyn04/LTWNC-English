namespace ltwnc.Areas.Admin.Models;

// PROTOTYPE ONLY — dữ liệu giả để so sánh bố cục, không dùng làm contract production.
public sealed class AdminDashboardPrototypeViewModel
{
    public string Variant { get; init; } = "A";
    public DateTime GeneratedAt { get; init; }
    public IReadOnlyList<AdminPrototypeMetric> Metrics { get; init; } = [];
    public IReadOnlyList<AdminPrototypeActivity> Activities { get; init; } = [];
    public IReadOnlyList<AdminPrototypeContentRow> PopularSets { get; init; } = [];
    public IReadOnlyList<AdminPrototypeHealthItem> Health { get; init; } = [];
}

public sealed record AdminPrototypeMetric(
    string Label,
    string Value,
    string Change,
    string Icon,
    string Tone);

public sealed record AdminPrototypeActivity(
    string Time,
    string Title,
    string Detail,
    string Icon,
    string Tone);

public sealed record AdminPrototypeContentRow(
    string Title,
    string Owner,
    int Cards,
    int Sessions,
    string CompletionRate);

public sealed record AdminPrototypeHealthItem(
    string Label,
    string Value,
    string Detail,
    string Tone);
