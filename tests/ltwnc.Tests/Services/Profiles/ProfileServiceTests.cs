using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Profile;
using ltwnc.Services.Auth;
using ltwnc.Services.Profiles;
using Moq;

namespace ltwnc.Tests.Services.Profiles;

public class ProfileServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static AppUser CreateUser(string id, string userName)
    {
        return new AppUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = $"{userName}@example.com",
            NormalizedEmail = $"{userName.ToUpperInvariant()}@EXAMPLE.COM"
        };
    }

    private static ProfileService CreateService(
        AppDbContext db,
        AppUser user,
        DateTimeOffset? now = null,
        Mock<IAuthService>? authService = null)
    {
        db.AppUsers.Add(user);
        db.SaveChanges();
        return new ProfileService(
            db,
            (authService ?? new Mock<IAuthService>()).Object,
            new FixedTimeProvider(now ?? Now));
    }

    [Fact]
    public async Task GetPublicProfile_PrivateProfile_ReturnsPrivateShellForNonOwner()
    {
        using AppDbContext db = CreateContext();
        AppUser user = CreateUser("user-1", "private-user");
        db.UserProfiles.Add(new UserProfile { UserId = user.Id, IsPublic = false });
        await db.SaveChangesAsync();
        ProfileService service = CreateService(db, user);

        PublicProfileViewModel? result = await service.GetPublicProfileAsync("private-user", "viewer");

        Assert.NotNull(result);
        Assert.True(result.IsPrivate);
        Assert.Null(result.Statistics);
        Assert.Empty(result.Timeline);
        Assert.Empty(result.Badges);
        Assert.Empty(result.PublicSets);
    }

    [Fact]
    public async Task GetPublicProfile_HiddenSections_AreNotReturned()
    {
        using AppDbContext db = CreateContext();
        AppUser user = CreateUser("user-1", "user1");
        db.UserProfiles.Add(new UserProfile { UserId = user.Id, IsPublic = true });
        await db.SaveChangesAsync();
        ProfileService service = CreateService(db, user);

        PublicProfileViewModel result = (await service.GetPublicProfileAsync("user1", "viewer"))!;

        Assert.Null(result.Statistics);
        Assert.Empty(result.Timeline);
        Assert.Empty(result.Badges);
        Assert.Empty(result.PublicSets);
    }

    [Fact]
    public async Task GetPublicProfile_VisibleSections_ReturnCorrectCountsAndNewestTwentyEvents()
    {
        using AppDbContext db = CreateContext();
        AppUser user = CreateUser("user-1", "user1");
        db.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            IsPublic = true,
            ShowStats = true,
            ShowBadges = true,
            ShowActivity = true,
            ShowPublicSets = true
        });
        var set = new FlashcardSet
        {
            Id = 1,
            UserId = user.Id,
            Title = "Public set",
            IsPublic = true,
            CreatedAt = Now.UtcDateTime.AddDays(-1),
            Flashcards =
            [
                new Flashcard { Id = 1, FrontText = "one", BackText = "một" },
                new Flashcard { Id = 2, FrontText = "two", BackText = "hai" }
            ]
        };
        db.FlashcardSets.Add(set);
        db.UserProgresses.Add(new UserProgress
        {
            UserId = user.Id,
            FlashcardId = 1,
            IsLearned = true
        });
        db.UserAchievements.Add(new UserAchievement
        {
            UserId = user.Id,
            Code = "first",
            Title = "First badge",
            Description = "First badge",
            UnlockedAt = Now.UtcDateTime
        });
        for (int index = 0; index < 21; index++)
        {
            db.StudySessions.Add(new StudySession
            {
                UserId = user.Id,
                FlashcardSetId = set.Id,
                Mode = StudyMode.Dictation,
                Score = 80,
                CompletedAt = Now.UtcDateTime.AddMinutes(-index)
            });
        }

        await db.SaveChangesAsync();
        ProfileService service = CreateService(db, user);

        PublicProfileViewModel result = (await service.GetPublicProfileAsync("user1", "viewer"))!;

        Assert.NotNull(result.Statistics);
        Assert.Equal(1, result.Statistics.OwnedSetCount);
        Assert.Equal(1, result.Statistics.PublicSetCount);
        Assert.Equal(2, result.Statistics.TotalFlashcardCount);
        Assert.Equal(1, result.Statistics.LearnedFlashcardCount);
        Assert.Equal(21, result.Statistics.CompletedSessionCount);
        Assert.Equal(1, result.Statistics.UnlockedBadgeCount);
        Assert.Equal(2, result.Statistics.CurrentStreak);
        Assert.Equal(20, result.Timeline.Count);
        Assert.True(result.Timeline.SequenceEqual(
            result.Timeline.OrderByDescending(item => item.Timestamp)));
        Assert.Single(result.Badges);
        Assert.Single(result.PublicSets);
        Assert.Contains(result.Timeline, item =>
            item.Kind == "study" &&
            item.Detail != null &&
            item.Detail.Contains("Dictation") &&
            item.Detail.Contains("80"));
    }

    [Fact]
    public async Task GetPublicProfile_StreakStopsAtFirstUtcDateGap()
    {
        using AppDbContext db = CreateContext();
        AppUser user = CreateUser("user-1", "user1");
        db.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            IsPublic = true,
            ShowStats = true
        });
        db.FlashcardSets.AddRange(
            new FlashcardSet
            {
                Id = 1,
                UserId = user.Id,
                Title = "Today",
                IsPublic = true,
                CreatedAt = Now.UtcDateTime
            },
            new FlashcardSet
            {
                Id = 2,
                UserId = user.Id,
                Title = "Two days ago",
                IsPublic = true,
                CreatedAt = Now.UtcDateTime.AddDays(-2)
            });
        await db.SaveChangesAsync();
        ProfileService service = CreateService(db, user);

        PublicProfileViewModel result = (await service.GetPublicProfileAsync("user1", null))!;

        Assert.Equal(1, result.Statistics!.CurrentStreak);
    }

    [Fact]
    public async Task UpdateProfile_UsernameChangedWithinThirtyDays_ReturnsCooldownError()
    {
        using AppDbContext db = CreateContext();
        AppUser user = CreateUser("user-1", "user1");
        db.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            LastUsernameChangedAt = Now.UtcDateTime.AddDays(-10)
        });
        await db.SaveChangesAsync();
        ProfileService service = CreateService(db, user);

        ProfileOperationResult result = await service.UpdateProfileAsync(
            user.Id,
            new ProfileEditViewModel { Username = "new-name", IsPublic = true });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error =>
            error.Field == nameof(ProfileEditViewModel.Username) &&
            error.Message.Contains("30 ngày"));
    }

    [Fact]
    public async Task UpdateProfile_InvalidUsername_DoesNotLoadUser()
    {
        using AppDbContext db = CreateContext();
        var authService = new Mock<IAuthService>();
        ProfileService service = new(db, authService.Object, new FixedTimeProvider(Now));

        ProfileOperationResult result = await service.UpdateProfileAsync(
            "user-1",
            new ProfileEditViewModel { Username = "profile", IsPublic = true });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error =>
            error.Field == nameof(ProfileEditViewModel.Username) &&
            error.Message == "Username này được dành riêng cho hệ thống.");
        Assert.Empty(db.AppUsers);
    }

    [Fact]
    public async Task UpdateProfile_UsernameChangedAfterThirtyDays_UpdatesAppUserAndTimestamp()
    {
        using AppDbContext db = CreateContext();
        AppUser user = CreateUser("user-1", "user1");
        string originalSecurityStamp = user.SecurityStamp;
        db.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            LastUsernameChangedAt = Now.UtcDateTime.AddDays(-31)
        });
        await db.SaveChangesAsync();
        ProfileService service = CreateService(db, user);

        ProfileOperationResult result = await service.UpdateProfileAsync(
            user.Id,
            new ProfileEditViewModel { Username = "new-name", IsPublic = true });

        Assert.True(result.Succeeded);
        Assert.Equal("new-name", user.UserName);
        Assert.Equal("NEW-NAME", user.NormalizedUserName);
        Assert.NotEqual(originalSecurityStamp, user.SecurityStamp);
        Assert.Equal(Now.UtcDateTime, db.UserProfiles.Single().LastUsernameChangedAt);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ReturnsVietnameseFieldError()
    {
        using AppDbContext db = CreateContext();
        AppUser user = CreateUser("user-1", "user1");
        db.UserProfiles.Add(new UserProfile { UserId = user.Id });
        await db.SaveChangesAsync();
        var authService = new Mock<IAuthService>();
        authService.Setup(service => service.ChangePasswordAsync(
                user,
                "Wrong123",
                "NewPass123",
                default))
            .ReturnsAsync(AuthResult.Failure(
                new AuthError("PasswordMismatch", "Mật khẩu hiện tại không đúng.")));
        ProfileService service = CreateService(db, user, authService: authService);

        ProfileOperationResult result = await service.ChangePasswordAsync(
            user.Id,
            new ChangePasswordViewModel
            {
                CurrentPassword = "Wrong123",
                NewPassword = "NewPass123",
                ConfirmPassword = "NewPass123"
            });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error =>
            error.Field == nameof(ChangePasswordViewModel.CurrentPassword) &&
            error.Message == "Mật khẩu hiện tại không đúng.");
    }

    [Fact]
    public async Task UpdateProfile_SaveFails_PropagatesDatabaseError()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FailingProfileDbContext(options);
        AppUser user = CreateUser("user-1", "user1");
        db.UserProfiles.Add(new UserProfile { UserId = user.Id });
        await db.SaveChangesAsync();
        ProfileService service = CreateService(db, user);
        db.FailSaves = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateProfileAsync(
            user.Id,
            new ProfileEditViewModel { Username = "new-name", IsPublic = true }));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FailingProfileDbContext(DbContextOptions<AppDbContext> options)
        : AppDbContext(options)
    {
        public bool FailSaves { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            FailSaves
                ? throw new InvalidOperationException("database failure")
                : base.SaveChangesAsync(cancellationToken);
    }
}
