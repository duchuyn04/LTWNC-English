namespace ltwnc.Services.AdminSearch;

public sealed record AdminGlobalSearchQuery(
    string? Query,
    int PerTypeLimit = AdminGlobalSearchService.DefaultPerTypeLimit);

public sealed record AdminGlobalSearchResult(
    string OriginalQuery,
    string NormalizedQuery,
    bool WasTruncated,
    IReadOnlyList<AdminGlobalSearchGroup> Groups)
{
    public bool HasQuery
    {
        get
        {
            return !string.IsNullOrWhiteSpace(NormalizedQuery);
        }
    }

    public bool HasAnyResult
    {
        get
        {
            return Groups.Any(group => group.Items.Count > 0);
        }
    }
}

public sealed record AdminGlobalSearchGroup(
    string Type,
    string Label,
    string SeeMoreUrl,
    bool HasMore,
    IReadOnlyList<AdminGlobalSearchItem> Items);

public sealed record AdminGlobalSearchItem(
    string Type,
    string PrimaryText,
    string SecondaryText,
    string Status,
    string AdminUrl);
