using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Review;
using ltwnc.Services.Review;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Review;

public sealed class ReviewSettingsServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_NewSet_PersistsAgreedDefaults()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, "user-1", 1, quota: 5);

        ReviewSettingsViewModel? actual = await CreateService(context)
            .GetOrCreateAsync("user-1", 1);

        Assert.NotNull(actual);
        Assert.Equal(20, actual.ReviewSessionSize);
        Assert.Equal(5, actual.NewCardQuota);
        Assert.Equal(30, actual.ReviewMaxIntervalDays);
        Assert.Equal(1, await context.ReviewSettings.CountAsync());
    }

    [Fact]
    public async Task GetOrCreateAsync_LegacySet_CopiesLegacyReviewValuesAndSafeQuota()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, "user-1", 1, quota: 99);
        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            ReviewSessionSize = 40,
            ReviewMaxIntervalDays = 120,
            ShowFrontDefinition = true,
            PronounceBack = true
        });
        await context.SaveChangesAsync();

        ReviewSettingsViewModel? actual = await CreateService(context)
            .GetOrCreateAsync("user-1", 1);

        Assert.NotNull(actual);
        Assert.Equal(40, actual.ReviewSessionSize);
        Assert.Equal(ReviewSettingsPolicy.DefaultNewCardQuota, actual.NewCardQuota);
        Assert.Equal(120, actual.ReviewMaxIntervalDays);
        Assert.True(actual.ShowFrontDefinition);
        Assert.True(actual.PronounceBack);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenCalledRepeatedly_ReusesOneRow()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, "user-1", 1);
        ReviewSettingsService service = CreateService(context);

        await service.GetOrCreateAsync("user-1", 1);
        await service.GetOrCreateAsync("user-1", 1);

        Assert.Equal(1, await context.ReviewSettings.CountAsync());
    }

    [Fact]
    public async Task GetAsync_ExistingSettings_ReturnsPersistedValues()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, "user-1", 1);
        context.ReviewSettings.Add(new ReviewSettings
        {
            UserId = "user-1",
            FlashcardSetId = 1,
            ReviewSessionSize = 55,
            NewCardQuota = 3,
            ReviewMaxIntervalDays = 180,
            ShowBackExample = false
        });
        await context.SaveChangesAsync();

        ReviewSettingsViewModel? actual = await CreateService(context).GetAsync("user-1", 1);

        Assert.NotNull(actual);
        Assert.Equal(55, actual.ReviewSessionSize);
        Assert.Equal(3, actual.NewCardQuota);
        Assert.Equal(180, actual.ReviewMaxIntervalDays);
        Assert.False(actual.ShowBackExample);
    }

    [Fact]
    public async Task GetAsync_OwnedSetWithoutRow_ReturnsNullWithoutCreatingRow()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, "user-1", 1);

        Assert.Null(await CreateService(context).GetAsync("user-1", 1));
        Assert.Empty(await context.ReviewSettings.ToListAsync());
    }

    [Fact]
    public async Task GetOrCreateAsync_InvalidLegacyPolicyValues_UsesSafeDefaults()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, "user-1", 1);
        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            ReviewSessionSize = 101,
            ReviewMaxIntervalDays = 29
        });
        await context.SaveChangesAsync();

        ReviewSettingsViewModel? actual = await CreateService(context)
            .GetOrCreateAsync("user-1", 1);

        Assert.NotNull(actual);
        Assert.Equal(ReviewSettingsPolicy.DefaultSessionSize, actual.ReviewSessionSize);
        Assert.Equal(ReviewSettingsPolicy.DefaultMaxIntervalDays, actual.ReviewMaxIntervalDays);
    }

    [Fact]
    public async Task GetOrCreateAsync_DifferentSetsForOneUser_StoresIndependentRows()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, "user-1", 1);
        await SeedSetAsync(context, "user-1", 2);
        ReviewSettingsService service = CreateService(context);

        await service.SaveAsync("user-1", 1, new ReviewSettingsViewModel
        {
            ReviewSessionSize = 5,
            NewCardQuota = 0,
            ReviewMaxIntervalDays = 30
        });
        ReviewSettingsViewModel? second = await service.GetOrCreateAsync("user-1", 2);

        Assert.NotNull(second);
        Assert.Equal(20, second.ReviewSessionSize);
        Assert.Equal(5, second.NewCardQuota);
        Assert.Equal(1, await context.ReviewSettings.CountAsync(value => value.FlashcardSetId == 1));
        Assert.Equal(1, await context.ReviewSettings.CountAsync(value => value.FlashcardSetId == 2));
    }

    [Fact]
    public async Task GetOrCreateAsync_CancellationRequested_PropagatesCancellation()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, "user-1", 1);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService(context).GetOrCreateAsync("user-1", 1, cancellation.Token));
    }

    [Fact]
    public async Task GetOrCreateAsync_ForeignOrUnknownSet_DoesNotCreateSettings()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, "user-1", 1);
        await SeedSetAsync(context, "user-2", 2);
        ReviewSettingsService service = CreateService(context);

        Assert.Null(await service.GetOrCreateAsync("user-1", 2));
        Assert.Null(await service.GetOrCreateAsync("user-1", 999));
        Assert.Empty(await context.ReviewSettings.ToListAsync());
    }

    [Fact]
    public async Task SaveAsync_AllReviewFields_RoundTripWithoutUsingSharedSettings()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, "user-1", 1, quota: 9);
        ReviewSettingsViewModel input = new()
        {
            ReviewSessionSize = 100,
            NewCardQuota = 0,
            ReviewMaxIntervalDays = 365,
            ShowFrontTerm = false,
            ShowFrontDefinition = true,
            ShowFrontIpa = false,
            ShowFrontImage = true,
            ShowBackTerm = true,
            ShowBackDefinition = false,
            ShowBackIpa = true,
            ShowBackExample = false,
            ShowBackImage = false,
            HideImage = true,
            BlurImage = true,
            LargeImage = true,
            PronounceFront = false,
            PronounceBack = true
        };

        ReviewSettingsViewModel? saved = await CreateService(context)
            .SaveAsync("user-1", 1, input);
        ReviewSettings? persisted = await context.ReviewSettings.SingleAsync();

        Assert.NotNull(saved);
        Assert.Equal(input.ReviewSessionSize, persisted.ReviewSessionSize);
        Assert.Equal(input.NewCardQuota, persisted.NewCardQuota);
        Assert.Equal(input.ReviewMaxIntervalDays, persisted.ReviewMaxIntervalDays);
        Assert.Equal(input.ShowFrontImage, persisted.ShowFrontImage);
        Assert.Equal(input.ShowBackExample, persisted.ShowBackExample);
        Assert.Equal(input.HideImage, persisted.HideImage);
        Assert.Equal(input.PronounceBack, persisted.PronounceBack);
        Assert.Equal(9, (await context.FlashcardSets.SingleAsync()).NewCardQuota);
    }

    [Theory]
    [InlineData(4, 5, 30)]
    [InlineData(5, -1, 30)]
    [InlineData(5, 0, 29)]
    [InlineData(101, 21, 366)]
    public async Task SaveAsync_InvalidPolicyValue_ThrowsBeforePersistence(
        int sessionSize,
        int newCardQuota,
        int maxIntervalDays)
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, "user-1", 1);
        ReviewSettingsViewModel input = new()
        {
            ReviewSessionSize = sessionSize,
            NewCardQuota = newCardQuota,
            ReviewMaxIntervalDays = maxIntervalDays
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateService(context).SaveAsync("user-1", 1, input));

        Assert.Empty(await context.ReviewSettings.ToListAsync());
    }

    [Fact]
    public async Task SaveAsync_SecondSave_UpdatesExistingRowInsteadOfDuplicating()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, "user-1", 1);
        ReviewSettingsService service = CreateService(context);
        await service.GetOrCreateAsync("user-1", 1);

        await service.SaveAsync("user-1", 1, new ReviewSettingsViewModel
        {
            ReviewSessionSize = 50,
            NewCardQuota = 12,
            ReviewMaxIntervalDays = 90
        });

        ReviewSettings persisted = await context.ReviewSettings.SingleAsync();
        Assert.Equal(50, persisted.ReviewSessionSize);
        Assert.Equal(12, persisted.NewCardQuota);
        Assert.Equal(90, persisted.ReviewMaxIntervalDays);
        Assert.Equal(1, await context.ReviewSettings.CountAsync());
    }

    [Fact]
    public async Task SaveAsync_ForeignSet_ReturnsNullAndDoesNotMutateForeignRow()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, "user-1", 1);
        await SeedSetAsync(context, "user-2", 2);
        context.ReviewSettings.Add(ReviewSettings.CreateDefault("user-2", 2, 5));
        await context.SaveChangesAsync();

        ReviewSettingsViewModel? actual = await CreateService(context).SaveAsync(
            "user-1",
            2,
            new ReviewSettingsViewModel { ReviewSessionSize = 100 });

        Assert.Null(actual);
        ReviewSettings foreign = await context.ReviewSettings.SingleAsync();
        Assert.Equal(20, foreign.ReviewSessionSize);
    }

    [Fact]
    public async Task SaveAsync_NullInput_ThrowsArgumentNullException()
    {
        await using AppDbContext context = CreateContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateService(context).SaveAsync("user-1", 1, null!));
    }

    [Fact]
    public void ReviewSettingsViewModel_RangeAttributes_ExposeAgreedBounds()
    {
        ReviewSettingsViewModel valid = new()
        {
            ReviewSessionSize = 5,
            NewCardQuota = 0,
            ReviewMaxIntervalDays = 30
        };
        List<ValidationResult> errors = [];

        bool isValid = Validator.TryValidateObject(
            valid,
            new ValidationContext(valid),
            errors,
            validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ReviewSettingsMapper_RoundTripsEveryDisplayAndPronunciationFlag()
    {
        ReviewSettingsViewModel input = new()
        {
            ShowFrontTerm = false,
            ShowFrontDefinition = true,
            ShowFrontIpa = false,
            ShowFrontImage = true,
            ShowBackTerm = true,
            ShowBackDefinition = false,
            ShowBackIpa = true,
            ShowBackExample = false,
            ShowBackImage = false,
            HideImage = true,
            BlurImage = true,
            LargeImage = true,
            PronounceFront = false,
            PronounceBack = true
        };

        ReviewSettings entity = ReviewSettingsMapper.ToEntity("user-1", 7, input);
        ReviewSettingsViewModel output = ReviewSettingsMapper.ToViewModel(entity);

        Assert.Equal(input.ShowFrontTerm, output.ShowFrontTerm);
        Assert.Equal(input.ShowFrontDefinition, output.ShowFrontDefinition);
        Assert.Equal(input.ShowFrontIpa, output.ShowFrontIpa);
        Assert.Equal(input.ShowFrontImage, output.ShowFrontImage);
        Assert.Equal(input.ShowBackTerm, output.ShowBackTerm);
        Assert.Equal(input.ShowBackDefinition, output.ShowBackDefinition);
        Assert.Equal(input.ShowBackIpa, output.ShowBackIpa);
        Assert.Equal(input.ShowBackExample, output.ShowBackExample);
        Assert.Equal(input.ShowBackImage, output.ShowBackImage);
        Assert.Equal(input.HideImage, output.HideImage);
        Assert.Equal(input.BlurImage, output.BlurImage);
        Assert.Equal(input.LargeImage, output.LargeImage);
        Assert.Equal(input.PronounceFront, output.PronounceFront);
        Assert.Equal(input.PronounceBack, output.PronounceBack);
    }

    [Fact]
    public async Task GetOrCreateAsync_ConcurrentCalls_LeaveOneRow()
    {
        string databaseName = Guid.NewGuid().ToString();
        await using AppDbContext seedContext = CreateContext(databaseName);
        await SeedSetAsync(seedContext, "user-1", 1);

        await using AppDbContext firstContext = CreateContext(databaseName);
        await using AppDbContext secondContext = CreateContext(databaseName);
        Task<ReviewSettingsViewModel?>[] calls =
        [
            CreateService(firstContext).GetOrCreateAsync("user-1", 1),
            CreateService(secondContext).GetOrCreateAsync("user-1", 1)
        ];

        await Task.WhenAll(calls);

        await using AppDbContext verifyContext = CreateContext(databaseName);
        Assert.Equal(1, await verifyContext.ReviewSettings.CountAsync());
    }

    [Fact]
    public void ModelMetadata_UsesCompositeUniqueKeyAndCascadeDelete()
    {
        using AppDbContext context = CreateContext();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity =
            context.Model.FindEntityType(typeof(ReviewSettings))!;

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(ReviewSettings.UserId), nameof(ReviewSettings.FlashcardSetId)]));
        Microsoft.EntityFrameworkCore.Metadata.IForeignKey foreignKey =
            entity.GetForeignKeys().Single(foreign => foreign.PrincipalEntityType.ClrType == typeof(FlashcardSet));
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    private static ReviewSettingsService CreateService(AppDbContext context) => new(context);

    private static AppDbContext CreateContext(string? databaseName = null)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedSetAsync(
        AppDbContext context,
        string userId,
        int id,
        int quota = ReviewSettingsPolicy.DefaultNewCardQuota)
    {
        context.FlashcardSets.Add(new FlashcardSet
        {
            Id = id,
            UserId = userId,
            Title = $"Set {id}",
            NewCardQuota = quota
        });
        await context.SaveChangesAsync();
    }
}
