using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Profile;
using ltwnc.Services.Achievements;
using ltwnc.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Profiles;

public sealed class ProfileService : IProfileService
{
    private readonly AppDbContext _db;
    private readonly IAuthService _authService;
    private readonly TimeProvider _timeProvider;
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    public ProfileService(
        AppDbContext db,
        IAuthService authService,
        TimeProvider timeProvider)
    {
        // 1. Lưu dependency `_db` để các phương thức khác sử dụng.
        _db = db;
        // 2. Lưu dependency `_authService` để các phương thức khác sử dụng.
        _authService = authService;
        // 3. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
    }

    public async Task<PublicProfileViewModel?> GetPublicProfileAsync(
        string username,
        string? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `ToUpperInvariant` và lưu kết quả vào `normalizedUserName`.
        string normalizedUserName = username.Trim().ToUpperInvariant();
        // 2. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `user`.
        AppUser? user = await _db.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.NormalizedUserName == normalizedUserName,
                cancellationToken);
        // 3. Kiểm tra `user == null` để chọn nhánh xử lý phù hợp.
        if (user == null)
        {
            // 4. Trả `null` cho nơi gọi.
            return null;
        }

        // 5. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `profile`.
        UserProfile? profile = await _db.UserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        // 6. Cập nhật `profile` bằng giá trị mới.
        profile ??= new UserProfile { UserId = user.Id };

        // 7. Gọi `Equals` và lưu kết quả vào `isOwner`.
        bool isOwner = string.Equals(user.Id, viewerUserId, StringComparison.Ordinal);
        // 8. Kiểm tra `!profile.IsPublic && !isOwner` để chọn nhánh xử lý phù hợp.
        if (!profile.IsPublic && !isOwner)
        {
            // 9. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new PublicProfileViewModel
            {
                Username = user.UserName,
                AvatarPath = profile.AvatarPath,
                AvatarInitial = AvatarInitial(user.UserName),
                IsPrivate = true
            };
        }

        // Thống kê luôn được hiển thị trên hồ sơ công khai.
        bool showStats = true;
        // 11. Tính giá trị và lưu vào `showBadges` để dùng ở bước tiếp theo.
        bool showBadges = isOwner || profile.ShowBadges;
        // 12. Tính giá trị và lưu vào `showActivity` để dùng ở bước tiếp theo.
        bool showActivity = isOwner || profile.ShowActivity;
        // 13. Tính giá trị và lưu vào `showPublicSets` để dùng ở bước tiếp theo.
        bool showPublicSets = isOwner || profile.ShowPublicSets;

        ReviewActivityStatistics? reviewActivity = showStats || showActivity
            ? await LoadReviewActivityStatisticsAsync(user.Id, cancellationToken)
            : null;

        // 14. Tính giá trị và lưu vào `statistics` để dùng ở bước tiếp theo.
        ProfileStatisticsViewModel? statistics = showStats
            ? await BuildStatisticsAsync(user.Id, reviewActivity!, cancellationToken)
            : null;
        // 15. Tính giá trị và lưu vào `badges` để dùng ở bước tiếp theo.
        IReadOnlyList<ProfileBadgeViewModel> badges = showBadges
            ? await LoadBadgesAsync(user.Id, cancellationToken)
            : [];
        // 16. Tính giá trị và lưu vào `timeline` để dùng ở bước tiếp theo.
        IReadOnlyList<ProfileTimelineItemViewModel> timeline = showActivity
            ? await LoadTimelineAsync(user.Id, reviewActivity!.Sessions, cancellationToken)
            : [];
        // 17. Tính giá trị và lưu vào `publicSets` để dùng ở bước tiếp theo.
        IReadOnlyList<ProfilePublicSetViewModel> publicSets = showPublicSets
            ? await LoadPublicSetsAsync(user.Id, cancellationToken)
            : [];

        // 18. Tạo và trả đối tượng kết quả cho nơi gọi.
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
        // 1. Gọi `FindUserAsync` và lưu kết quả vào `user`.
        AppUser user = await FindUserAsync(userId);
        // 2. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `profile`.
        UserProfile? profile = await _db.UserProfiles
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        // 3. Kiểm tra `profile == null` để chọn nhánh xử lý phù hợp.
        if (profile == null)
        {
            // 4. Tính giá trị và lưu vào `now` để dùng ở bước tiếp theo.
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            // 5. Cập nhật `profile` bằng giá trị mới.
            profile = new UserProfile
            {
                UserId = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            // 6. Gọi `Add` để thực hiện bước nghiệp vụ này.
            _db.UserProfiles.Add(profile);
            // 7. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _db.SaveChangesAsync(cancellationToken);
        }

        // 8. Trả kết quả từ `ToEditModel` cho nơi gọi.
        return ToEditModel(user, profile);
    }

    public async Task<ProfileOperationResult> UpdateProfileAsync(
        string userId,
        ProfileEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        // 1. Tính giá trị và lưu vào `username` để dùng ở bước tiếp theo.
        string username = model.Username?.Trim() ?? string.Empty;
        // 2. Gọi `GetValidationError` và lưu kết quả vào `usernameError`.
        string? usernameError = UsernamePolicy.GetValidationError(username);
        // 3. Kiểm tra `usernameError != null` để chọn nhánh xử lý phù hợp.
        if (usernameError != null)
        {
            // 4. Trả kết quả từ `Failure` cho nơi gọi.
            return ProfileOperationResult.Failure(new ProfileFieldError(
                nameof(ProfileEditViewModel.Username),
                usernameError));
        }

        // 5. Gọi `FindUserAsync` và lưu kết quả vào `user`.
        AppUser user = await FindUserAsync(userId);
        // 6. Gọi `GetOrCreateProfileAsync` và lưu kết quả vào `profile`.
        UserProfile profile = await GetOrCreateProfileAsync(userId, cancellationToken);
        // 7. Tính giá trị và lưu vào `now` để dùng ở bước tiếp theo.
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

        // 8. Kiểm tra `!string.Equals(user.UserName, username, StringComparison.Ordinal)` để chọn nhánh xử lý phù hợp.
        if (!string.Equals(user.UserName, username, StringComparison.Ordinal))
        {
            // 9. Kiểm tra `profile.LastUsernameChangedAt.HasValue && now - profile.LastUsernam...` để chọn nhánh xử lý phù hợp.
            if (profile.LastUsernameChangedAt.HasValue &&
                now - profile.LastUsernameChangedAt.Value < TimeSpan.FromDays(30))
            {
                // 10. Trả kết quả từ `Failure` cho nơi gọi.
                return ProfileOperationResult.Failure(new ProfileFieldError(
                    nameof(ProfileEditViewModel.Username),
                    "Bạn chỉ có thể đổi tên đăng nhập sau mỗi 30 ngày."));
            }

            // 11. Gọi `ToUpperInvariant` và lưu kết quả vào `normalizedUserName`.
            string normalizedUserName = username.ToUpperInvariant();
            // 12. Gọi `AnyAsync` và lưu kết quả vào `duplicated`.
            bool duplicated = await _db.AppUsers.AnyAsync(
                item => item.NormalizedUserName == normalizedUserName && item.Id != userId,
                cancellationToken);
            // 13. Kiểm tra `duplicated` để chọn nhánh xử lý phù hợp.
            if (duplicated)
            {
                // 14. Trả kết quả từ `Failure` cho nơi gọi.
                return ProfileOperationResult.Failure(new ProfileFieldError(
                    nameof(ProfileEditViewModel.Username),
                    "Tên đăng nhập đã được sử dụng."));
            }

            // 15. Cập nhật `user.UserName` bằng giá trị mới.
            user.UserName = username;
            // 16. Cập nhật `user.NormalizedUserName` bằng giá trị mới.
            user.NormalizedUserName = normalizedUserName;
            // 17. Cập nhật `user.SecurityStamp` bằng giá trị mới.
            user.SecurityStamp = Guid.NewGuid().ToString();
            // 18. Cập nhật `user.ConcurrencyStamp` bằng giá trị mới.
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
            // 19. Cập nhật `profile.LastUsernameChangedAt` bằng giá trị mới.
            profile.LastUsernameChangedAt = now;
        }

        // 20. Cập nhật `profile.Bio` bằng giá trị mới.
        profile.Bio = string.IsNullOrWhiteSpace(model.Bio) ? null : model.Bio.Trim();
        // 21. Cập nhật `profile.IsPublic` bằng giá trị mới.
        profile.IsPublic = model.IsPublic;
        // Thống kê là thông tin mặc định, không còn phụ thuộc vào tùy chọn hồ sơ.
        profile.ShowStats = true;
        // 23. Cập nhật `profile.ShowBadges` bằng giá trị mới.
        profile.ShowBadges = model.ShowBadges;
        // 24. Cập nhật `profile.ShowActivity` bằng giá trị mới.
        profile.ShowActivity = model.ShowActivity;
        // 25. Cập nhật `profile.ShowPublicSets` bằng giá trị mới.
        profile.ShowPublicSets = model.ShowPublicSets;
        // 26. Cập nhật `profile.UpdatedAt` bằng giá trị mới.
        profile.UpdatedAt = now;

        // 27. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _db.SaveChangesAsync(cancellationToken);
        // 28. Trả kết quả từ `Success` cho nơi gọi.
        return ProfileOperationResult.Success();
    }

    public async Task<ProfileOperationResult> ChangePasswordAsync(
        string userId,
        ChangePasswordViewModel model,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `FindUserAsync` và lưu kết quả vào `user`.
        AppUser user = await FindUserAsync(userId);
        // 2. Gọi `ChangePasswordAsync` và lưu kết quả vào `result`.
        AuthResult result = await _authService.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword,
            cancellationToken);
        // 3. Kiểm tra `result.Succeeded` để chọn nhánh xử lý phù hợp.
        if (result.Succeeded)
        {
            // 4. Trả kết quả từ `Success` cho nơi gọi.
            return ProfileOperationResult.Success();
        }

        // 5. Gọi `ToArray` và lưu kết quả vào `errors`.
        ProfileFieldError[] errors = result.Errors
            .Select(error => new ProfileFieldError(
                error.Code == "PasswordMismatch"
                    ? nameof(ChangePasswordViewModel.CurrentPassword)
                    : nameof(ChangePasswordViewModel.NewPassword),
                error.Message))
            .ToArray();
        // 6. Trả kết quả từ `Failure` cho nơi gọi.
        return ProfileOperationResult.Failure(errors);
    }

    private async Task<AppUser> FindUserAsync(string userId)
    {
        // 1. Trả `await _db.AppUsers.SingleOrDefaultAsync(item => item.Id == userId) ...` cho nơi gọi.
        return await _db.AppUsers.SingleOrDefaultAsync(item => item.Id == userId)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
    }

    private async Task<UserProfile> GetOrCreateProfileAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `profile`.
        UserProfile? profile = await _db.UserProfiles
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        // 2. Kiểm tra `profile != null` để chọn nhánh xử lý phù hợp.
        if (profile != null)
        {
            // 3. Trả `profile` cho nơi gọi.
            return profile;
        }

        // 4. Tính giá trị và lưu vào `now` để dùng ở bước tiếp theo.
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        // 5. Cập nhật `profile` bằng giá trị mới.
        profile = new UserProfile { UserId = userId, CreatedAt = now, UpdatedAt = now };
        // 6. Gọi `Add` để thực hiện bước nghiệp vụ này.
        _db.UserProfiles.Add(profile);
        // 7. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _db.SaveChangesAsync(cancellationToken);
        // 8. Trả `profile` cho nơi gọi.
        return profile;
    }

    private ProfileEditViewModel ToEditModel(AppUser user, UserProfile profile)
    {
        // 1. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new()
    {
        Username = user.UserName,
        Email = user.Email,
        Bio = profile.Bio,
        AvatarPath = profile.AvatarPath,
        AvatarInitial = AvatarInitial(user.UserName),
        IsPublic = profile.IsPublic,
        ShowStats = true,
        ShowBadges = profile.ShowBadges,
        ShowActivity = profile.ShowActivity,
        ShowPublicSets = profile.ShowPublicSets
    };
    }

    private async Task<ProfileStatisticsViewModel> BuildStatisticsAsync(
        string userId,
        ReviewActivityStatistics reviewStatistics,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `ToListAsync` và lưu kết quả vào `setIds`.
        List<int> setIds = await _db.FlashcardSets
            .Where(set => set.UserId == userId)
            .Select(set => set.Id)
            .ToListAsync(cancellationToken);

        // 2. Tính giá trị và lưu vào `now` để dùng ở bước tiếp theo.
        DateTimeOffset now = _timeProvider.GetUtcNow();
        // 3. Gọi `LoadActiveDatesAsync` và lưu kết quả vào `activeDates`.
        List<DateTime> activeDates = await LoadActiveDatesAsync(
            userId,
            reviewStatistics.ActivityDates,
            cancellationToken);
        DateTime streakToday = reviewStatistics.ActivityDates.Count > 0
            ? ToVietnamDate(now)
            : now.UtcDateTime.Date;

        // 4. Tạo và trả đối tượng kết quả cho nơi gọi.
        int completedStudySessionCount = await _db.StudySessions.CountAsync(
            session => session.UserId == userId && session.CompletedAt.HasValue,
            cancellationToken);

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
            CompletedSessionCount = completedStudySessionCount + reviewStatistics.CompletedReviewSessionCount,
            CompletedReviewSessionCount = reviewStatistics.CompletedReviewSessionCount,
            ReviewActivityDayCount = reviewStatistics.ActivityDates.Count,
            UnlockedBadgeCount = await _db.UserAchievements.CountAsync(
                achievement => achievement.UserId == userId, cancellationToken),
            CurrentStreak = CalculateStreak(activeDates, streakToday)
        };
    }

    private async Task<IReadOnlyList<ProfileBadgeViewModel>> LoadBadgesAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        List<UserAchievement> userAchievements = await _db.UserAchievements
            .AsNoTracking()
            .Where(achievement => achievement.UserId == userId)
            .OrderByDescending(achievement => achievement.UnlockedAt)
            .ToListAsync(cancellationToken);

        return userAchievements
            .Select(achievement => new ProfileBadgeViewModel
            {
                Code = achievement.Code,
                Title = achievement.Title,
                Description = achievement.Description,
                UnlockedAt = achievement.UnlockedAt,
                IconClass = AchievementCatalog.GetIconClass(achievement.Code)
            })
            .ToList();
    }

    private async Task<IReadOnlyList<ProfilePublicSetViewModel>> LoadPublicSetsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        // 1. Trả kết quả từ `ToListAsync` cho nơi gọi.
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
        IReadOnlyList<ReviewSession> reviewSessions,
        CancellationToken cancellationToken)
    {
        // 1. Khởi tạo `items` với dữ liệu ban đầu cần thiết.
        var items = new List<ProfileTimelineItemViewModel>();
        // 2. Gọi `AddRange` để thực hiện bước nghiệp vụ này.
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
        // 3. Gọi `AddRange` để thực hiện bước nghiệp vụ này.
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
        // 4. Gọi `AddRange` để thực hiện bước nghiệp vụ này.
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

        items.AddRange(reviewSessions
            .Where(session => session.Items.Any(item => item.Rating.HasValue))
            .Select(session =>
            {
                int ratedCount = session.Items.Count(item => item.Rating.HasValue);
                DateTime timestamp = session.Items
                    .Where(item => item.RatedAtUtc.HasValue)
                    .Select(item => item.RatedAtUtc!.Value.UtcDateTime)
                    .DefaultIfEmpty(session.CompletedAtUtc?.UtcDateTime
                        ?? session.EndedAtUtc?.UtcDateTime
                        ?? session.StartedAtUtc.UtcDateTime)
                    .Max();
                string title = session.CompletedAtUtc.HasValue
                    ? "Hoàn thành lượt ôn"
                    : session.EndedAtUtc.HasValue
                        ? "Kết thúc lượt ôn"
                        : "Ôn tập đến hạn";
                return new ProfileTimelineItemViewModel
                {
                    Kind = "review",
                    Title = title,
                    Detail = $"{ratedCount} thẻ đã đánh giá",
                    Timestamp = timestamp
                };
            }));

        // 5. Trả kết quả từ `ToList` cho nơi gọi.
        return items
            .OrderByDescending(item => item.Timestamp)
            .Take(20)
            .ToList();
    }

    private async Task<List<DateTime>> LoadActiveDatesAsync(
        string userId,
        IReadOnlyList<DateTime> reviewActivityDates,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `ToListAsync` và lưu kết quả vào `dates`.
        List<DateTime> dates = await _db.StudySessions
            .Where(session => session.UserId == userId && session.CompletedAt.HasValue)
            .Select(session => session.CompletedAt!.Value)
            .ToListAsync(cancellationToken);
        // 2. Gọi `AddRange` để thực hiện bước nghiệp vụ này.
        dates.AddRange(await _db.UserAchievements
            .Where(achievement => achievement.UserId == userId)
            .Select(achievement => achievement.UnlockedAt)
            .ToListAsync(cancellationToken));
        // 3. Gọi `AddRange` để thực hiện bước nghiệp vụ này.
        dates.AddRange(await _db.FlashcardSets
            .Where(set =>
                set.UserId == userId
                && set.IsPublic
                && set.ModerationStatus == FlashcardSetModerationStatus.Active)
            .Select(set => set.CreatedAt)
            .ToListAsync(cancellationToken));

        bool useVietnamCalendar = reviewActivityDates.Count > 0;
        dates = dates
            .Select(date => useVietnamCalendar ? ToVietnamDate(date) : date.Date)
            .ToList();

        dates.AddRange(reviewActivityDates);

        // 4. Trả kết quả từ `ToList` cho nơi gọi.
        return dates.Distinct().ToList();
    }

    private async Task<ReviewActivityStatistics> LoadReviewActivityStatisticsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        List<ReviewSession> sessions = await _db.ReviewSessions
            .AsNoTracking()
            .Include(session => session.Items)
            .Where(session => session.UserId == userId)
            .ToListAsync(cancellationToken);
        List<DateTime> activityDates = sessions
            .SelectMany(session => session.Items)
            .Where(item => item.Rating.HasValue && item.RatedAtUtc.HasValue)
            .Select(item => ToVietnamDate(item.RatedAtUtc!.Value))
            .Distinct()
            .ToList();

        return new ReviewActivityStatistics(
            sessions.Count(session => session.CompletedAtUtc.HasValue),
            activityDates,
            sessions);
    }

    private static int CalculateStreak(IEnumerable<DateTime> activeDates, DateTime today)
    {
        // 1. Gọi `ToHashSet` và lưu kết quả vào `dates`.
        HashSet<DateTime> dates = activeDates.ToHashSet();
        // 2. Tính giá trị và lưu vào `cursor` để dùng ở bước tiếp theo.
        DateTime cursor = dates.Contains(today) ? today : today.AddDays(-1);
        // 3. Tính giá trị và lưu vào `streak` để dùng ở bước tiếp theo.
        int streak = 0;
        // 4. Tiếp tục lặp khi `dates.Contains(cursor)` còn đúng.
        while (dates.Contains(cursor))
        {
            // 5. Cập nhật bộ đếm hoặc trạng thái `streak`.
            streak++;
            // 6. Cập nhật `cursor` bằng giá trị mới.
            cursor = cursor.AddDays(-1);
        }

        // 7. Trả `streak` cho nơi gọi.
        return streak;
    }

    private static DateTime ToVietnamDate(DateTime utc)
    {
        return ToVietnamDate(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));
    }

    private static DateTime ToVietnamDate(DateTimeOffset utc)
    {
        return TimeZoneInfo.ConvertTime(utc, VietnamTimeZone).Date;
    }

    private sealed record ReviewActivityStatistics(
        int CompletedReviewSessionCount,
        IReadOnlyList<DateTime> ActivityDates,
        IReadOnlyList<ReviewSession> Sessions);

    private static string AvatarInitial(string username)
    {
        // 1. Gọi `Trim` và lưu kết quả vào `trimmed`.
        string trimmed = username.Trim();
        // 2. Trả `trimmed.Length == 0 ? "?" : trimmed[..1].ToUpperInvariant()` cho nơi gọi.
        return trimmed.Length == 0 ? "?" : trimmed[..1].ToUpperInvariant();
    }
}
