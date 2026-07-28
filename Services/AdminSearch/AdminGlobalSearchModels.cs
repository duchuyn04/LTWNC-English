namespace ltwnc.Services.AdminSearch;

// Từ khóa tìm kiếm toàn cục và giới hạn kết quả cho mỗi loại dữ liệu.
public sealed record AdminGlobalSearchQuery(
    string? Query,
    int PerTypeLimit = AdminGlobalSearchService.DefaultPerTypeLimit);

// Kết quả tìm kiếm đã chuẩn hóa và được chia theo từng nhóm dữ liệu.
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
            // 1. Kiểm tra từ khóa sau chuẩn hóa còn nội dung hay không.
            return !string.IsNullOrWhiteSpace(NormalizedQuery);
        }
    }

    public bool HasAnyResult
    {
        get
        {
            // 1. Kiểm tra có ít nhất một nhóm chứa kết quả.
            return Groups.Any(group => group.Items.Count > 0);
        }
    }
}

// Một nhóm kết quả cùng loại và đường dẫn xem thêm.
public sealed record AdminGlobalSearchGroup(
    string Type,
    string Label,
    string SeeMoreUrl,
    bool HasMore,
    IReadOnlyList<AdminGlobalSearchItem> Items);

// Một kết quả tìm kiếm an toàn để hiển thị trong khu vực Admin.
public sealed record AdminGlobalSearchItem(
    string Type,
    string PrimaryText,
    string SecondaryText,
    string Status,
    string AdminUrl);
