using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Profiles;

public sealed class ProfileService : IProfileService
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly TimeProvider _timeProvider;

    public ProfileService(
        AppDbContext db,
        UserManager<IdentityUser> userManager,
        TimeProvider timeProvider)
    {
        _db = db;
        _userManager = userManager;
        _timeProvider = timeProvider;
    }

    public async Task<PublicProfileViewModel?> GetPublicProfileAsync(
        string username,
        string? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        IdentityUser? user = await _userManager.FindByNameAsync(username.Trim());
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
                Username = user.UserName ?? username,
                AvatarPath = profile.AvatarPath,
                AvatarInitial = AvatarInitial(user.UserName ?? username),
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
            Username = user.UserName ?? username,
            Bio = profile.Bio,
            AvatarPath = profile.AvatarPath,
            AvatarInitial = AvatarInitial(user.UserName ?? username),
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
                set => set.UserId == userId && set.IsPublic, cancellationToken),
            TotalFlashcardCount = await _db.Flashcards.CountAsync(
                card => setIds.Contains(card.FlashcardSetId), cancellationToken),
            LearnedFlashcardCount = await _db.UserProgresses.CountAsync(
                progress => progress.UserId == userId && progress.IsLearned, cancellationToken),
            CompletedSessionCount = await _db.StudySessions.CountAsync(
                session => session.UserId == userId, cancellationToken),
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
            .Where(set => set.UserId == userId && set.IsPublic)
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
            .Where(session => session.UserId == userId)
            .Select(session => new ProfileTimelineItemViewModel
            {
                Kind = "study",
                Title = "Hoàn thành phiên học",
                Detail = session.Score.HasValue ? $"Điểm: {session.Score}" : session.Mode.ToString(),
                Timestamp = session.CompletedAt
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
            .Where(set => set.UserId == userId && set.IsPublic)
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
            .Where(session => session.UserId == userId)
            .Select(session => session.CompletedAt)
            .ToListAsync(cancellationToken);
        dates.AddRange(await _db.UserAchievements
            .Where(achievement => achievement.UserId == userId)
            .Select(achievement => achievement.UnlockedAt)
            .ToListAsync(cancellationToken));
        dates.AddRange(await _db.FlashcardSets
            .Where(set => set.UserId == userId && set.IsPublic)
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
