namespace ltwnc.Areas.Admin.Models;

public sealed class AdminAuditLogIndexViewModel
{
    public required IReadOnlyList<AdminAuditLogRow> Items { get; init; }
    public required string? Search { get; init; }
    public required string? Action { get; init; }
    public required string? Outcome { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }

    public int TotalPages => TotalCount == 0
        ? 1
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public sealed class AdminAuditLogRow
{
    public required string OccurredAtDisplay { get; init; }
    public required string ActorDisplay { get; init; }
    public required string Action { get; init; }
    public required string Target { get; init; }
    public required string Outcome { get; init; }
    public required string? Reason { get; init; }
    public required string? CorrelationId { get; init; }
}
