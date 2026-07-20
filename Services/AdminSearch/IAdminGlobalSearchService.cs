namespace ltwnc.Services.AdminSearch;

public interface IAdminGlobalSearchService
{
    // Tìm kiếm toàn cục cho Admin trên các trường nhận diện an toàn.
    Task<AdminGlobalSearchResult> SearchAsync(
        AdminGlobalSearchQuery query,
        CancellationToken cancellationToken = default);
}
