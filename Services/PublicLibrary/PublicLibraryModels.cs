namespace ltwnc.Services.PublicLibrary;

// Hằng số sort của thư viện công khai — mọi giá trị lạ đều quy về "popular".
public static class PublicLibrarySort
{
    public const string Popular = "popular";
    public const string Recent = "recent";
    public const string Cards = "cards";

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Recent => Recent,
        Cards => Cards,
        _ => Popular
    };
}

public sealed record PublicLibraryQuery(string? Search, string? Sort, int Page = 1);
public sealed record PublicLibrarySummary(int SetCount, int CardCount, int CopyCount);
public sealed record PublicLibrarySetItem(
    int Id,
    string Title,
    string? Description,
    string AuthorName,
    int CardCount,
    int CopyCount,
    DateTime UpdatedAt);

public sealed record PublicLibraryResult(
    string? Search,
    string Sort,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    PublicLibrarySummary Summary,
    IReadOnlyList<PublicLibrarySetItem> Items);
