using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace ltwnc.Tests.Services.FlashcardSets;

public sealed class ReviewSettingsProvisioningTests
{
    [Fact]
    public async Task CreateSetAsync_CreatesDefaultReviewSettingsForNewSet()
    {
        await using AppDbContext context = CreateContext();
        FlashcardSetService service = new(context, new TestWebHostEnvironment());

        FlashcardSet set = await service.CreateSetAsync(
            "Bộ mới",
            null,
            isPublic: false,
            userId: "user-1",
            newCardQuota: 12);

        ReviewSettings settings = await context.ReviewSettings.SingleAsync();

        Assert.Equal(set.Id, settings.FlashcardSetId);
        Assert.Equal("user-1", settings.UserId);
        Assert.Equal(20, settings.ReviewSessionSize);
        Assert.Equal(12, settings.NewCardQuota);
        Assert.Equal(30, settings.ReviewMaxIntervalDays);
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ltwnc.Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
