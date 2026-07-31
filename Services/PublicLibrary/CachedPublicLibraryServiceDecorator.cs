using Microsoft.Extensions.Caching.Memory;

namespace ltwnc.Services.PublicLibrary;

// Decorator bổ sung cache cho IPublicLibraryService mà không đưa trách nhiệm cache
// vào PublicLibraryService. Mọi truy vấn không đủ điều kiện cache vẫn được chuyển
// nguyên vẹn cho Concrete Component xử lý.
public sealed class CachedPublicLibraryServiceDecorator : IPublicLibraryService
{
    private const int MaximumCachedPage = 20;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly IPublicLibraryService _inner;
    private readonly IMemoryCache _cache;

    public CachedPublicLibraryServiceDecorator(
        IPublicLibraryService inner,
        IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<PublicLibraryResult> BrowseAsync(
        PublicLibraryQuery query,
        CancellationToken cancellationToken = default)
    {
        // Không cache từ khóa tùy ý để người dùng ẩn danh không thể tạo số lượng
        // cache key không giới hạn. Page ngoài phạm vi phổ biến cũng đi thẳng vào
        // service gốc, nhờ đó Decorator chỉ tạo tối đa 60 key.
        if (!CanCache(query))
        {
            return await _inner.BrowseAsync(query, cancellationToken);
        }

        PublicLibraryCacheKey key = new(
            PublicLibrarySort.Normalize(query.Sort),
            query.Page);

        // Chỉ lưu kết quả thành công. Nếu service gốc ném lỗi hoặc request bị hủy,
        // factory không hoàn tất nên MemoryCache không tạo entry lỗi.
        PublicLibraryResult? result = await _cache.GetOrCreateAsync(
            key,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return await _inner.BrowseAsync(query, cancellationToken);
            });

        // Contract của IPublicLibraryService không cho phép null. Nhánh bảo vệ này
        // chỉ phát hiện implementation sai thay vì âm thầm trả dữ liệu không hợp lệ.
        return result
            ?? throw new InvalidOperationException("Dịch vụ thư viện trả về kết quả rỗng.");
    }

    private static bool CanCache(PublicLibraryQuery query)
    {
        return string.IsNullOrWhiteSpace(query.Search)
            && query.Page is >= 1 and <= MaximumCachedPage;
    }

    // Record làm cache key theo value equality, tránh xung đột có thể xảy ra khi
    // ghép search, sort và page thành một chuỗi thủ công.
    private sealed record PublicLibraryCacheKey(string Sort, int Page);
}
