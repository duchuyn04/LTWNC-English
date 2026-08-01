using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Threading.RateLimiting;
using ltwnc.Areas.Admin;
using ltwnc.Data;
using ltwnc.Services.Achievements;
using ltwnc.Services.Auth;
using ltwnc.Services.CardActions;
using ltwnc.Services.FlashcardSets;
using ltwnc.Services.ContentReports;
using ltwnc.Services.ContentModeration;
using ltwnc.Services.AdminDashboard;
using ltwnc.Services.AdminUsers;
using ltwnc.Services.Study;
using ltwnc.Services.StudyEvents;
using ltwnc.Services.StudyModes;
using ltwnc.Services.EnglishMission;
using ltwnc.Services.Profiles;
using ltwnc.Services.Leaderboard;
using ltwnc.Services.PublicLibrary;
using ltwnc.Services.Review;
using ltwnc.Services.Credits;

var builder = WebApplication.CreateBuilder(args);

// Giới hạn logging vào console/debug để môi trường local không bị lỗi quyền ghi Windows EventLog.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Auth tự quản dùng cookie và bảng AppUsers.
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();
builder.Services.AddScoped<IPasswordHasher<ltwnc.Models.Entities.AppUser>,
    PasswordHasher<ltwnc.Models.Entities.AppUser>>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminAreaPolicy.Name, policy =>
    {
        policy.RequireClaim(AppClaimTypes.IsAdmin, "true");
    });
});

builder.Services.Configure<CookieAuthenticationOptions>(
    CookieAuthenticationDefaults.AuthenticationScheme,
    options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/Admin"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            // Kiểm tra mỗi request: user còn tồn tại, security stamp khớp, không bị khóa.
            string? userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            string? stamp = context.Principal?.FindFirstValue(AppClaimTypes.SecurityStamp);
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(stamp))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            AppDbContext dbContext =
                context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            TimeProvider timeProvider =
                context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
            ltwnc.Models.Entities.AppUser? user = await dbContext.AppUsers
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == userId);

            DateTimeOffset now = timeProvider.GetUtcNow();
            bool locked = user?.LockoutEnd != null && user.LockoutEnd > now;
            if (user == null || user.SecurityStamp != stamp || locked)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
});

builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ltwnc.Services.Audit.IAdminAuditService, ltwnc.Services.Audit.AdminAuditService>();
builder.Services.AddScoped<AdminDashboardService>();
builder.Services.AddSingleton<AdminUserLockCoordinator>();
builder.Services.AddScoped<IAdminUserAccountService, AdminUserAccountService>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<EnglishMissionConversationCleanupService>();
}
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ICreditService, CreditService>();
builder.Services.AddScoped<IAdminCreditService, AdminCreditService>();
builder.Services.AddScoped<IAvatarService, AvatarService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.Configure<RouteOptions>(options =>
    options.ConstraintMap["profileUsername"] = typeof(ProfileUsernameRouteConstraint));

// Application services — inject qua interface (swap/decorator sau này không sửa controller)
builder.Services.AddScoped<IFlashcardSetService, FlashcardSetService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<PublicLibraryService>();
// IPublicLibraryService được resolve thành Decorator; Concrete Component được
// resolve riêng để tránh Decorator phụ thuộc vòng lại chính interface của nó.
builder.Services.AddScoped<IPublicLibraryService>(provider =>
    new CachedPublicLibraryServiceDecorator(
        provider.GetRequiredService<PublicLibraryService>(),
        provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()));
builder.Services.AddScoped<IContentReportService, ContentReportService>();
builder.Services.AddScoped<IContentModerationService, ContentReportModerationService>();
builder.Services.AddScoped<IFlashcardImportService, FlashcardImportService>();
builder.Services.AddScoped<CsvFlashcardFileParser>();
builder.Services.AddScoped<XlsxFlashcardFileParser>();
builder.Services.AddScoped<FlashcardFileParserResolver>();
builder.Services.AddScoped<IStudyService, StudyService>();
builder.Services.AddScoped<ReviewStateMachine>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IReviewSettingsService, ReviewSettingsService>();
// Service xử lý nghe chép chính tả
builder.Services.AddScoped<IDictationService, DictationService>();
builder.Services.AddScoped<ICardActionService, CardActionService>();
builder.Services.AddScoped<ICardActionCommandFactory, CardActionCommandFactory>();
builder.Services.AddScoped<CardActionCommandCreator, DeleteCardsCommandCreator>();
builder.Services.AddScoped<CardActionCommandCreator, StarCardsCommandCreator>();
builder.Services.AddScoped<CardActionCommandCreator, UnstarCardsCommandCreator>();

// Study mode strategies
builder.Services.AddScoped<IStudyCardQueryService, StudyCardQueryService>();
builder.Services.AddScoped<IStudyModeStrategyResolver, StudyModeStrategyResolver>();
builder.Services.AddScoped<QuizQuestionFactory>();
builder.Services.AddScoped<IStudyModeStrategy, FlashcardModeStrategy>();
builder.Services.AddScoped<IStudyModeStrategy, DictationModeStrategy>();
builder.Services.AddScoped<IStudyModeStrategy, QuizModeStrategy>();
builder.Services.AddScoped<IStudyModeStrategy, EnglishMissionModeStrategy>();
builder.Services.AddScoped<IStudyModeStrategy, ReviewModeStrategy>();
builder.Services.AddScoped<IQuizService, QuizService>();

// ============================================================
// Mẫu Observer — đăng ký "trạm phát" và các "người theo dõi"
// Thêm observer mới: tạo class implement IStudyEventObserver + một dòng AddScoped dưới đây.
// Không cần sửa StudyService hay DictationService.
// ============================================================
builder.Services.AddScoped<IStudyEventPublisher, StudyEventPublisher>();
builder.Services.AddScoped<IStudyEventObserver, AchievementStudyObserver>();
builder.Services.AddScoped<IStudyEventObserver, LoggingStudyObserver>();
// Service đọc thành tích cho trang UI (không phải observer)
builder.Services.AddScoped<IAchievementService, AchievementService>();
// Service đếm metric tiến độ huy hiệu (snapshot live)
builder.Services.AddScoped<IAchievementProgressService, AchievementProgressService>();
// Service đồng bộ mở khóa huy hiệu đủ điều kiện (Observer + rescan trang)
builder.Services.AddScoped<IAchievementUnlockService, AchievementUnlockService>();

builder.Services.AddHttpClient("AiProvider")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false
    });
int authRateLimit = builder.Environment.IsEnvironment("Testing") ? 10_000 : 10;
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context =>
    {
        string key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"auth:{key}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authRateLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.AddPolicy("ai", context =>
    {
        string key = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"ai:{key}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.AddPolicy("uploads", context =>
    {
        string key = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"uploads:{key}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.AddPolicy("payments", context =>
    {
        string key = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"payments:{key}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    string dataProtectionKeyPath = Path.Combine(
        builder.Environment.ContentRootPath,
        ".tmp",
        "data-protection-keys");
    Directory.CreateDirectory(dataProtectionKeyPath);

    // Lưu key local trong workspace để tránh lỗi quyền AppData khi chạy app bằng tool/CI/dev shell.
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
}
else
{
    builder.Services.AddDataProtection();
}
builder.Services.AddScoped<ltwnc.Services.Ai.OpenAiCompatibleApiClient>();
builder.Services.AddScoped<ltwnc.Services.Ai.IAiProviderAdapter, ltwnc.Services.Ai.OpenAiCompatibleAdapter>();
builder.Services.AddScoped<ltwnc.Services.Ai.IAiCompletionRouter, ltwnc.Services.Ai.AiCompletionRouter>();
builder.Services.AddScoped<ltwnc.Services.Ai.IAiProviderService, ltwnc.Services.Ai.AiProviderService>();
builder.Services.AddScoped<ltwnc.Services.EnglishMission.IEnglishMissionService, ltwnc.Services.EnglishMission.EnglishMissionService>();


// Add MVC
builder.Services.AddControllersWithViews(options =>
    options.Conventions.Add(new AdminAreaAuthorizationConvention()));
builder.Services.AddScoped<ltwnc.Controllers.ApiExceptionFilter>();

var app = builder.Build();

// Cấu hình middleware pipeline
// Cấu hình pipeline middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCodePage");
app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
    {
        context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>()
            ?.Enabled = false;
    }
});
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapStaticAssets();
app.MapAreaControllerRoute(
    name: "admin-root",
    areaName: "Admin",
    pattern: "Admin",
    defaults: new { controller = "Dashboard", action = "Index" })
    .WithStaticAssets();
app.MapAreaControllerRoute(
    name: "admin",
    areaName: "Admin",
    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
