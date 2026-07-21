using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Profile;
using ltwnc.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Profiles;

public sealed class ProfileService : IProfileService
{
    private readonly AppDbContext _db;
    private readonly IAuthService _authService;
    private readonly TimeProvider _timeProvider;

    public ProfileService(
        AppDbContext db,
        IAuthService authService,
        TimeProvider timeProvider)
    {
        _db = db;
        _authService = authService;
        _timeProvider = timeProvider;
    }

    public async Task<PublicProfileViewModel?> GetPublicProfileAsync(
        string username,
        string? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        string normalizedUserName = username.Trim().ToUpperInvariant();
        AppUser? user = await _db.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.NormalizedUserName == normalizedUserName,
                cancellationToken);
        if (user == null)
        {
            return null;
        }

        UserProfile? profile = await _db.UserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        profile ??= new UserProfile { UserId = user.Id };

        bool isOwner = string.Equals(user.Id, viewerUserId, StringComparison.Ordinal);
        if (!profile.IsPublic && !isOwner)
        {
            return new PublicProfileViewModel
            {
                Username = user.UserName,
                AvatarPath = profile.AvatarPath,
                AvatarInitial = AvatarInitial(user.UserName),
                IsPrivate = true
            };
        }

        bool showStats = isOwner || profile.ShowStats;
        bool showBadges = isOwner || profile.ShowBadges;
        bool showActivity = isOwner || profile.ShowActivity;
        bool showPublicSets = isOwner || profile.ShowPublicSets;

        ProfileStatisticsViewModel? statistics = showStats
            ? await BuildStatisticsAsync(user.Id, cancellationToken)
            : null;
        IReadOnlyList<ProfileBadgeViewModel> badges = showBadges
            ? await LoadBadgesAsync(user.Id, cancellationToken)
            : [];
        IReadOnlyList<ProfileTimelineItemViewModel> timeline = showActivity
            ? await LoadTimelineAsync(user.Id, cancellationToken)
            : [];
        IReadOnlyList<ProfilePublicSetViewModel> publicSets = showPublicSets
            ? await LoadPublicSetsAsync(user.Id, cancellationToken)
            : [];

        return new PublicProfileViewModel
        {
            Username = user.UserName,
            Bio = profile.Bio,
            AvatarPath = profile.AvatarPath,
            AvatarInitial = AvatarInitial(user.UserName),
            IsOwner = isOwner,
            ShowStats = showStats,
            ShowBadges = showBadges,
            ShowActivity = showActivity,
            ShowPublicSets = showPublicSets,
            Statistics = statistics,
            Badges = badges,
            Timeline = timeline,
            PublicSets = publicSets
        };
    }

    public async Task<ProfileEditViewModel> GetEditModelAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        AppUser user = await FindUserAsync(userId);
        UserProfile? profile = await _db.UserProfiles
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (profile == null)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            profile = new UserProfile
            {
                UserId = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.UserProfiles.Add(profile);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return ToEditModel(user, profile);
    }

    public async Task<ProfileOperationResult> UpdateProfileAsync(
        string userId,
        ProfileEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        string username = model.Username?.Trim() ?? string.Empty;
        string? usernameError = UsernamePolicy.GetValidationError(username);
        if (usernameError != null)
        {
            return ProfileOperationResult.Failure(new ProfileFieldError(
                nameof(ProfileEditViewModel.Username),
                usernameError));
        }

        AppUser user = await FindUserAsync(userId);
        UserProfile profile = await GetOrCreateProfileAsync(userId, cancellationToken);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

        if (!string.Equals(user.UserName, username, StringComparison.Ordinal))
        {
            if (profile.LastUsernameChangedAt.HasValue &&
                now - profile.LastUsernameChangedAt.Value < TimeSpan.FromDays(30))
            {
                return ProfileOperationResult.Failure(new ProfileFieldError(
                    nameof(ProfileEditViewModel.Username),
                    "Bạn chỉ có thể đổi tên đăng nhập sau mỗi 30 ngày."));
            }

            string normalizedUserName = username.ToUpperInvariant();
            bool duplicated = await _db.AppUsers.AnyAsync(
                item => item.NormalizedUserName == normalizedUserName && item.Id != userId,
                cancellationToken);
            if (duplicated)
            {
                return ProfileOperationResult.Failure(new ProfileFieldError(
                    nameof(ProfileEditViewModel.Username),
                    "Tên đăng nhập đã được sử dụng."));
            }

            user.UserName = username;
            user.NormalizedUserName = normalizedUserName;
            user.SecurityStamp = Guid.NewGuid().ToString();
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
            profile.LastUsernameChangedAt = now;
        }

        profile.Bio = string.IsNullOrWhiteSpace(model.Bio) ? null : model.Bio.Trim();
        profile.IsPublic = model.IsPublic;
        profile.ShowStats = model.ShowStats;
        profile.ShowBadges = model.ShowBadges;
        profile.ShowActivity = model.ShowActivity;
        profile.ShowPublicSets = model.ShowPublicSets;
        profile.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return ProfileOperationResult.Success();
    }

    public async Task<ProfileOperationResult> ChangePasswordAsync(
        string userId,
        ChangePasswordViewModel model,
        CancellationToken cancellationToken = default)
    {
        AppUser user = await FindUserAsync(userId);
        AuthResult result = await _authService.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword,
            cancellationToken);
        if (result.Succeeded)
        {
            return ProfileOperationResult.Success();
        }

        ProfileFieldError[] errors = result.Errors
            .Select(error => new ProfileFieldError(
                error.Code == "PasswordMismatch"
                    ? nameof(ChangePasswordViewModel.CurrentPassword)
                    : nameof(ChangePasswordViewModel.NewPassword),
                error.Message))
            .ToArray();
        return ProfileOperationResult.Failure(errors);
    }

    private async Task<AppUser> FindUserAsync(string userId)
    {
        return await _db.AppUsers.SingleOrDefaultAsync(item => item.Id == userId)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
    }

    private async Task<UserProfile> GetOrCreateProfileAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        UserProfile? profile = await _db.UserProfiles
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (profile != null)
        {
            return profile;
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        profile = new UserProfile { UserId = userId, CreatedAt = now, UpdatedAt = now };
        _db.UserProfiles.Add(profile);
        await _db.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private ProfileEditViewModel ToEditModel(AppUser user, UserProfile profile) => new()
    {
        Username = user.UserName,
        Email = user.Email,
        Bio = profile.Bio,
        AvatarPath = profile.AvatarPath,
        AvatarInitial = AvatarInitial(user.UserName),
        IsPublic = profile.IsPublic,
        ShowStats = profile.ShowStats,
        ShowBadges = profile.ShowBadges,
        ShowActivity = profile.ShowActivity,
        ShowPublicSets = profile.ShowPublicSets
    };

    private async Task<ProfileStatisticsViewModel> BuildStatisticsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        List<int> setIds = await _db.FlashcardSets
            .Where(set => set.UserId == userId)
            .Select(set => set.Id)
            .ToListAsync(cancellationToken);

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        List<DateTime> activeDates = await LoadActiveDatesAsync(userId, cancellationToken);

        return new ProfileStatisticsViewModel
        {
            OwnedSetCount = setIds.Count,
            PublicSetCount = await _db.FlashcardSets.CountAsync(
                set =>
                    set.UserId == userId
                    && set.IsPublic
                    && set.ModerationStatus == FlashcardSetModerationStatus.Active,
                cancellationToken),
            TotalFlashcardCount = await _db.Flashcards.CountAsync(
                card => setIds.Contains(card.FlashcardSetId), cancellationToken),
            LearnedFlashcardCount = await _db.UserProgresses.CountAsync(
                progress => progress.UserId == userId && progress.IsLearned, cancellationToken),
            CompletedSessionCount = await _db.StudySessions.CountAsync(
                session => session.UserId == userId && session.CompletedAt.HasValue, cancellationToken),
            UnlockedBadgeCount = await _db.UserAchievements.CountAsync(
                achievement => achievement.UserId == userId, cancellationToken),
            CurrentStreak = CalculateStreak(activeDates, now.Date)
        };
    }

    private async Task<IReadOnlyList<ProfileBadgeViewModel>> LoadBadgesAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return await _db.UserAchievements
            .AsNoTracking()
            .Where(achievement => achievement.UserId == userId)
            .OrderByDescending(achievement => achievement.UnlockedAt)
            .Select(achievement => new ProfileBadgeViewModel
            {
                Code = achievement.Code,
                Title = achievement.Title,
                Description = achievement.Description,
                UnlockedAt = achievement.UnlockedAt
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ProfilePublicSetViewModel>> LoadPublicSetsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return await _db.FlashcardSets
            .AsNoTracking()
            .Where(set =>
                set.UserId == userId
                && set.IsPublic
                && set.ModerationStatus == FlashcardSetModerationStatus.Active)
            .OrderByDescending(set => set.CreatedAt)
            .Select(set => new ProfilePublicSetViewModel
            {
                Id = set.Id,
                Title = set.Title,
                Description = set.Description,
                CardCount = set.Flashcards.Count,
                CreatedAt = set.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ProfileTimelineItemViewModel>> LoadTimelineAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var items = new List<ProfileTimelineItemViewModel>();
        items.AddRange(await _db.StudySessions
            .AsNoTracking()
            .Where(session => session.UserId == userId && session.CompletedAt.HasValue)
            .Select(session => new ProfileTimelineItemViewModel
            {
                Kind = "study",
                Title = "Hoàn thành phiên học",
                Detail = session.Score.HasValue
                    ? $"{session.Mode} · Điểm: {session.Score}"
                    : session.Mode.ToString(),
                Timestamp = session.CompletedAt!.Value
            })
            .ToListAsync(cancellationToken));
        items.AddRange(await _db.UserAchievements
            .AsNoTracking()
            .Where(achievement => achievement.UserId == userId)
            .Select(achievement => new ProfileTimelineItemViewModel
            {
                Kind = "badge",
                Title = achievement.Title,
                Detail = achievement.Description,
                Timestamp = achievement.UnlockedAt
            })
            .ToListAsync(cancellationToken));
        items.AddRange(await _db.FlashcardSets
            .AsNoTracking()
            .Where(set =>
                set.UserId == userId
                && set.IsPublic
                && set.ModerationStatus == FlashcardSetModerationStatus.Active)
            .Select(set => new ProfileTimelineItemViewModel
            {
                Kind = "set",
                Title = $"Tạo bộ thẻ: {set.Title}",
                Detail = set.Description,
                Timestamp = set.CreatedAt
            })
            .ToListAsync(cancellationToken));

        return items
            .OrderByDescending(item => item.Timestamp)
            .Take(20)
            .ToList();
    }

    private async Task<List<DateTime>> LoadActiveDatesAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        List<DateTime> dates = await _db.StudySessions
            .Where(session => session.UserId == userId && session.CompletedAt.HasValue)
            .Select(session => session.CompletedAt!.Value)
            .ToListAsync(cancellationToken);
        dates.AddRange(await _db.UserAchievements
            .Where(achievement => achievement.UserId == userId)
            .Select(achievement => achievement.UnlockedAt)
            .ToListAsync(cancellationToken));
        dates.AddRange(await _db.FlashcardSets
            .Where(set =>
                set.UserId == userId
                && set.IsPublic
                && set.ModerationStatus == FlashcardSetModerationStatus.Active)
            .Select(set => set.CreatedAt)
            .ToListAsync(cancellationToken));
        return dates.Select(date => date.Date).Distinct().ToList();
    }

    private static int CalculateStreak(IEnumerable<DateTime> activeDates, DateTime today)
    {
        HashSet<DateTime> dates = activeDates.ToHashSet();
        DateTime cursor = dates.Contains(today) ? today : today.AddDays(-1);
        int streak = 0;
        while (dates.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    private static string AvatarInitial(string username)
    {
        string trimmed = username.Trim();
        return trimmed.Length == 0 ? "?" : trimmed[..1].ToUpperInvariant();
    }
}
