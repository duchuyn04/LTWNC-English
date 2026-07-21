namespace ltwnc.Services.PublicLibrary;

// Service chỉ đọc phục vụ trang /Library công khai — không mở rộng service CRUD hiện hữu.
public interface IPublicLibraryService
{
    Task<PublicLibraryResult> BrowseAsync(
        PublicLibraryQuery query,
        CancellationToken cancellationToken = default);
}
