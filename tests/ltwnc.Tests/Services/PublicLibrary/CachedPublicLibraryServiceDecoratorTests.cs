using ltwnc.Services.PublicLibrary;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace ltwnc.Tests.Services.PublicLibrary;

public sealed class CachedPublicLibraryServiceDecoratorTests
{
    [Fact]
    public async Task BrowseAsync_CacheMiss_DelegatesToInnerService()
    {
        PublicLibraryQuery query = new(null, PublicLibrarySort.Popular, 1);
        PublicLibraryResult expected = CreateResult(PublicLibrarySort.Popular, 1);
        Mock<IPublicLibraryService> inner = CreateInner(expected);
        using MemoryCache cache = new(new MemoryCacheOptions());
        CachedPublicLibraryServiceDecorator decorator = new(inner.Object, cache);

        PublicLibraryResult actual = await decorator.BrowseAsync(query);

        Assert.Same(expected, actual);
        inner.Verify(
            service => service.BrowseAsync(query, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BrowseAsync_SameEligibleQuery_ReturnsCachedResult()
    {
        PublicLibraryQuery query = new(null, PublicLibrarySort.Recent, 2);
        PublicLibraryResult expected = CreateResult(PublicLibrarySort.Recent, 2);
        Mock<IPublicLibraryService> inner = CreateInner(expected);
        using MemoryCache cache = new(new MemoryCacheOptions());
        CachedPublicLibraryServiceDecorator decorator = new(inner.Object, cache);

        PublicLibraryResult first = await decorator.BrowseAsync(query);
        PublicLibraryResult second = await decorator.BrowseAsync(query);

        Assert.Same(expected, first);
        Assert.Same(first, second);
        inner.Verify(
            service => service.BrowseAsync(query, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BrowseAsync_EquivalentSortValues_UseSameCacheEntry()
    {
        PublicLibraryQuery defaultSort = new(null, null, 1);
        PublicLibraryQuery explicitPopular = new(null, PublicLibrarySort.Popular, 1);
        PublicLibraryResult expected = CreateResult(PublicLibrarySort.Popular, 1);
        Mock<IPublicLibraryService> inner = CreateInner(expected);
        using MemoryCache cache = new(new MemoryCacheOptions());
        CachedPublicLibraryServiceDecorator decorator = new(inner.Object, cache);

        await decorator.BrowseAsync(defaultSort);
        PublicLibraryResult cached = await decorator.BrowseAsync(explicitPopular);

        Assert.Same(expected, cached);
        inner.Verify(
            service => service.BrowseAsync(
                It.IsAny<PublicLibraryQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BrowseAsync_DifferentPages_UseDifferentCacheEntries()
    {
        PublicLibraryResult expected = CreateResult(PublicLibrarySort.Popular, 1);
        Mock<IPublicLibraryService> inner = CreateInner(expected);
        using MemoryCache cache = new(new MemoryCacheOptions());
        CachedPublicLibraryServiceDecorator decorator = new(inner.Object, cache);

        await decorator.BrowseAsync(new PublicLibraryQuery(null, PublicLibrarySort.Popular, 1));
        await decorator.BrowseAsync(new PublicLibraryQuery(null, PublicLibrarySort.Popular, 2));

        inner.Verify(
            service => service.BrowseAsync(
                It.IsAny<PublicLibraryQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BrowseAsync_DifferentSorts_UseDifferentCacheEntries()
    {
        PublicLibraryResult expected = CreateResult(PublicLibrarySort.Popular, 1);
        Mock<IPublicLibraryService> inner = CreateInner(expected);
        using MemoryCache cache = new(new MemoryCacheOptions());
        CachedPublicLibraryServiceDecorator decorator = new(inner.Object, cache);

        await decorator.BrowseAsync(new PublicLibraryQuery(null, PublicLibrarySort.Popular, 1));
        await decorator.BrowseAsync(new PublicLibraryQuery(null, PublicLibrarySort.Recent, 1));
        await decorator.BrowseAsync(new PublicLibraryQuery(null, PublicLibrarySort.Cards, 1));

        inner.Verify(
            service => service.BrowseAsync(
                It.IsAny<PublicLibraryQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task BrowseAsync_SearchQuery_BypassesCache()
    {
        PublicLibraryQuery query = new("english", PublicLibrarySort.Popular, 1);
        PublicLibraryResult expected = CreateResult(PublicLibrarySort.Popular, 1);
        Mock<IPublicLibraryService> inner = CreateInner(expected);
        using MemoryCache cache = new(new MemoryCacheOptions());
        CachedPublicLibraryServiceDecorator decorator = new(inner.Object, cache);

        await decorator.BrowseAsync(query);
        await decorator.BrowseAsync(query);

        inner.Verify(
            service => service.BrowseAsync(query, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(21)]
    [InlineData(int.MaxValue)]
    public async Task BrowseAsync_PageOutsideCacheRange_BypassesCache(int page)
    {
        PublicLibraryQuery query = new(null, PublicLibrarySort.Popular, page);
        PublicLibraryResult expected = CreateResult(PublicLibrarySort.Popular, page);
        Mock<IPublicLibraryService> inner = CreateInner(expected);
        using MemoryCache cache = new(new MemoryCacheOptions());
        CachedPublicLibraryServiceDecorator decorator = new(inner.Object, cache);

        await decorator.BrowseAsync(query);
        await decorator.BrowseAsync(query);

        inner.Verify(
            service => service.BrowseAsync(query, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BrowseAsync_InnerServiceThrows_DoesNotCacheFailure()
    {
        PublicLibraryQuery query = new(null, PublicLibrarySort.Popular, 1);
        PublicLibraryResult expected = CreateResult(PublicLibrarySort.Popular, 1);
        Mock<IPublicLibraryService> inner = new();
        inner.SetupSequence(service => service.BrowseAsync(
                query,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable."))
            .ReturnsAsync(expected);
        using MemoryCache cache = new(new MemoryCacheOptions());
        CachedPublicLibraryServiceDecorator decorator = new(inner.Object, cache);

        await Assert.ThrowsAsync<InvalidOperationException>(() => decorator.BrowseAsync(query));
        PublicLibraryResult recovered = await decorator.BrowseAsync(query);

        Assert.Same(expected, recovered);
        inner.Verify(
            service => service.BrowseAsync(query, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private static Mock<IPublicLibraryService> CreateInner(PublicLibraryResult result)
    {
        Mock<IPublicLibraryService> inner = new();
        inner.Setup(service => service.BrowseAsync(
                It.IsAny<PublicLibraryQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return inner;
    }

    private static PublicLibraryResult CreateResult(string sort, int page)
    {
        return new PublicLibraryResult(
            Search: null,
            Sort: sort,
            Page: page,
            PageSize: 12,
            TotalItems: 0,
            TotalPages: 0,
            Summary: new PublicLibrarySummary(0, 0, 0),
            Items: Array.Empty<PublicLibrarySetItem>());
    }
}
