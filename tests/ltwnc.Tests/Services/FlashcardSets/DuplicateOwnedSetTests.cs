using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace ltwnc.Tests.Services.FlashcardSets;

public sealed class DuplicateOwnedSetTests
{
    [Fact]
    public async Task DuplicateOwnedSetAsync_ClonesContentSettingsStarsAndImageFile()
    {
        string root = CreateTemporaryRoot();
        try
        {
            await using AppDbContext context = CreateContext();
            byte[] sourceImage = { 1, 3, 5, 7, 9 };
            string sourceImagePath = CreateImage(root, "source.png", sourceImage);
            FlashcardSet source = await SeedSourceAsync(
                context,
                uploadedImagePath: sourceImagePath,
                isStarred: true,
                reviewPaused: true);
            int sourceCardId = source.Flashcards.Single().Id;
            context.ReviewProgresses.Add(new ReviewProgress
            {
                UserId = "user-1",
                FlashcardId = sourceCardId,
                Stage = ReviewStage.Reviewing,
                LongTermIntervalDays = 10
            });
            context.StudySessions.Add(new StudySession
            {
                UserId = "user-1",
                FlashcardSetId = source.Id,
                Mode = StudyMode.Flashcard,
                StartedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            FlashcardSetService service = new(context, new TestWebHostEnvironment(root));

            FlashcardSet result = await service.DuplicateOwnedSetAsync(source.Id, "user-1");

            FlashcardSet saved = await context.FlashcardSets
                .AsNoTracking()
                .Include(set => set.Flashcards)
                .SingleAsync(set => set.Id == result.Id);
            ReviewSettings settings = await context.ReviewSettings
                .AsNoTracking()
                .SingleAsync(item => item.FlashcardSetId == saved.Id);
            Flashcard clonedCard = Assert.Single(saved.Flashcards);

            Assert.Equal("Vocabulary (Bản sao)", saved.Title);
            Assert.Equal("user-1", saved.UserId);
            Assert.False(saved.IsPublic);
            Assert.Null(saved.SourceSetId);
            Assert.True(saved.ReviewPaused);
            Assert.Equal(7, saved.NewCardQuota);
            Assert.Equal(FlashcardSetModerationStatus.Active, saved.ModerationStatus);
            Assert.True(clonedCard.IsStarred);
            Assert.Equal("https://example.test/image.png", clonedCard.ImageUrl);
            Assert.NotEqual(sourceImagePath, clonedCard.UploadedImagePath);
            Assert.NotNull(clonedCard.UploadedImagePath);
            Assert.Equal(sourceImage, File.ReadAllBytes(ToPhysicalPath(root, clonedCard.UploadedImagePath!)));
            Assert.Equal(35, settings.ReviewSessionSize);
            Assert.Equal(7, settings.NewCardQuota);
            Assert.Equal(90, settings.ReviewMaxIntervalDays);
            Assert.True(settings.ShowFrontImage);
            Assert.True(settings.PronounceBack);
            ReviewProgress originalProgress = await context.ReviewProgresses.SingleAsync();
            StudySession originalSession = await context.StudySessions.SingleAsync();
            Assert.Equal(sourceCardId, originalProgress.FlashcardId);
            Assert.Equal(source.Id, originalSession.FlashcardSetId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DuplicateOwnedSetAsync_RepeatedCopiesUseSequentialTitles()
    {
        string root = CreateTemporaryRoot();
        try
        {
            await using AppDbContext context = CreateContext();
            FlashcardSet source = await SeedSourceAsync(context);
            FlashcardSetService service = new(context, new TestWebHostEnvironment(root));

            FlashcardSet first = await service.DuplicateOwnedSetAsync(source.Id, "user-1");
            FlashcardSet second = await service.DuplicateOwnedSetAsync(source.Id, "user-1");
            FlashcardSet third = await service.DuplicateOwnedSetAsync(first.Id, "user-1");

            Assert.Equal("Vocabulary (Bản sao)", first.Title);
            Assert.Equal("Vocabulary (Bản sao 2)", second.Title);
            Assert.Equal("Vocabulary (Bản sao 3)", third.Title);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DuplicateOwnedSetAsync_LongTitleStaysWithinDatabaseLimit()
    {
        string root = CreateTemporaryRoot();
        try
        {
            await using AppDbContext context = CreateContext();
            FlashcardSet source = await SeedSourceAsync(context, title: new string('A', 200));
            FlashcardSetService service = new(context, new TestWebHostEnvironment(root));

            FlashcardSet duplicate = await service.DuplicateOwnedSetAsync(source.Id, "user-1");

            Assert.Equal(200, duplicate.Title.Length);
            Assert.EndsWith(" (Bản sao)", duplicate.Title);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DuplicateOwnedSetAsync_QuarantinedSetIsRejected()
    {
        string root = CreateTemporaryRoot();
        try
        {
            await using AppDbContext context = CreateContext();
            FlashcardSet source = await SeedSourceAsync(
                context,
                moderationStatus: FlashcardSetModerationStatus.Quarantined);
            FlashcardSetService service = new(context, new TestWebHostEnvironment(root));

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.DuplicateOwnedSetAsync(source.Id, "user-1"));

            Assert.Contains("cách ly", exception.Message);
            Assert.Equal(1, await context.FlashcardSets.CountAsync());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DuplicateOwnedSetAsync_MissingImageRollsBackEntireCopy()
    {
        string root = CreateTemporaryRoot();
        try
        {
            await using AppDbContext context = CreateContext();
            FlashcardSet source = await SeedSourceAsync(
                context,
                uploadedImagePath: "/uploads/flashcards/missing.png");
            FlashcardSetService service = new(context, new TestWebHostEnvironment(root));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.DuplicateOwnedSetAsync(source.Id, "user-1"));

            Assert.Equal(1, await context.FlashcardSets.CountAsync());
            Assert.Equal(1, await context.ReviewSettings.CountAsync());
            Assert.Empty(Directory.GetFiles(ImageDirectory(root)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DuplicateOwnedSetAsync_DatabaseFailureDeletesCopiedFiles()
    {
        string root = CreateTemporaryRoot();
        try
        {
            await using FailingAppDbContext context = CreateFailingContext();
            string sourceImagePath = CreateImage(root, "source.png", new byte[] { 2, 4, 6 });
            FlashcardSet source = await SeedSourceAsync(context, uploadedImagePath: sourceImagePath);
            context.RejectSaves = true;
            FlashcardSetService service = new(context, new TestWebHostEnvironment(root));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => service.DuplicateOwnedSetAsync(source.Id, "user-1"));

            Assert.Equal(new[] { "source.png" }, Directory.GetFiles(ImageDirectory(root))
                .Select(Path.GetFileName)
                .OrderBy(name => name)
                .ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DuplicateOwnedSetAsync_OtherOwnerCannotProbeOrCopySet()
    {
        string root = CreateTemporaryRoot();
        try
        {
            await using AppDbContext context = CreateContext();
            FlashcardSet source = await SeedSourceAsync(context);
            FlashcardSetService service = new(context, new TestWebHostEnvironment(root));

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => service.DuplicateOwnedSetAsync(source.Id, "user-2"));

            Assert.Equal(1, await context.FlashcardSets.CountAsync());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<FlashcardSet> SeedSourceAsync(
        AppDbContext context,
        string? uploadedImagePath = null,
        bool isStarred = false,
        bool reviewPaused = false,
        string moderationStatus = FlashcardSetModerationStatus.Active,
        string title = "Vocabulary")
    {
        FlashcardSet source = new()
        {
            Title = title,
            Description = "Source description",
            UserId = "user-1",
            IsPublic = true,
            ModerationStatus = moderationStatus,
            NewCardQuota = 3,
            ReviewPaused = reviewPaused,
            Flashcards = new List<Flashcard>
            {
                new()
                {
                    FrontText = "hello",
                    BackText = "xin chào",
                    Pronunciation = "həˈləʊ",
                    PartOfSpeech = "interjection",
                    ExampleSentence = "Hello world",
                    ExampleMeaning = "Xin chào thế giới",
                    ImageUrl = "https://example.test/image.png",
                    UploadedImagePath = uploadedImagePath,
                    IsStarred = isStarred,
                    OrderIndex = 0
                }
            }
        };
        context.FlashcardSets.Add(source);
        await context.SaveChangesAsync();
        context.ReviewSettings.Add(new ReviewSettings
        {
            UserId = source.UserId,
            FlashcardSetId = source.Id,
            ReviewSessionSize = 35,
            NewCardQuota = 7,
            ReviewMaxIntervalDays = 90,
            ShowFrontImage = true,
            PronounceBack = true
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return source;
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static FailingAppDbContext CreateFailingContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FailingAppDbContext(options);
    }

    private static string CreateTemporaryRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "ltwnc-duplicate-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ImageDirectory(root));
        return root;
    }

    private static string CreateImage(string root, string fileName, byte[] content)
    {
        File.WriteAllBytes(Path.Combine(ImageDirectory(root), fileName), content);
        return $"/uploads/flashcards/{fileName}";
    }

    private static string ToPhysicalPath(string root, string uploadedImagePath) =>
        Path.Combine(ImageDirectory(root), Path.GetFileName(uploadedImagePath));

    private static string ImageDirectory(string root) =>
        Path.Combine(root, "uploads", "flashcards");

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string webRootPath)
        {
            WebRootPath = webRootPath;
        }

        public string ApplicationName { get; set; } = "ltwnc.Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FailingAppDbContext : AppDbContext
    {
        public FailingAppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public bool RejectSaves { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return RejectSaves
                ? Task.FromException<int>(new DbUpdateException("Forced test failure."))
                : base.SaveChangesAsync(cancellationToken);
        }
    }
}
