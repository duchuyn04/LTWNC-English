using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Threading.RateLimiting;
using ltwnc.Areas.Admin;
using ltwnc.Data;
using ltwnc.Services.Achievements;
using ltwnc.Services.Ai;
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

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<GoogleAuthSettings>(
    builder.Configuration.GetSection("Authentication:Google"));
AuthenticationBuilder authenticationBuilder = builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie()
    .AddCookie(AuthSchemes.ExternalCookie, options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        options.SlidingExpiration = false;
    });
string googleClientId = builder.Configuration["Authentication:Google:ClientId"] ?? string.Empty;
string googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
if (!string.IsNullOrWhiteSpace(googleClientId) &&
    !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authenticationBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = AuthSchemes.ExternalCookie;
        options.Scope.Add("email");
        options.Scope.Add("profile");
        options.ClaimActions.MapJsonKey("urn:google:verified_email", "verified_email");
        options.ClaimActions.MapJsonKey("email_verified", "email_verified");
    });
}
builder.Services.AddScoped<IPasswordHasher<ltwnc.Models.Entities.AppUser>,
    PasswordHasher<ltwnc.Models.Entities.AppUser>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailMessageSender, SmtpEmailMessageSender>();
builder.Services.AddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
builder.Services.AddScoped<IEmailOtpService, EmailOtpService>();
builder.Services.AddScoped<IAccountSecurityService, AccountSecurityService>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminAreaPolicy.Name, policy =>
    {
        policy.RequireClaim(AppClaimTypes.IsAdmin, "true");
    });
});

// Named cookie options — dùng PostConfigure để tránh nhầm overload Configure + BinderOptions.
builder.Services.PostConfigure<CookieAuthenticationOptions>(
    CookieAuthenticationDefaults.AuthenticationScheme,
    options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Production/HTTPS: Always. Local HTTP dev: SameAsRequest.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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
            // Kiểm tra: user còn tồn tại, security stamp khớp, không bị khóa.
            // Cache ngắn (15s) theo userId+stamp để giảm query DB mỗi request.
            // Force-logout sau đổi stamp có thể trễ tối đa ~15s.
            string? userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            string? stamp = context.Principal?.FindFirstValue(AppClaimTypes.SecurityStamp);
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(stamp))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            IMemoryCache memoryCache =
                context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            string cacheKey = $"auth:principal-ok:{userId}:{stamp}";
            if (memoryCache.TryGetValue(cacheKey, out object? cached) && cached is true)
            {
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
                memoryCache.Remove(cacheKey);
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            memoryCache.Set(
                cacheKey,
                true,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15)
                });
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
builder.Services.AddScoped<ltwnc.Services.Lessons.ILessonService, ltwnc.Services.Lessons.LessonService>();
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
string dataProtectionKeyPath;
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    dataProtectionKeyPath = Path.Combine(
        builder.Environment.ContentRootPath,
        ".tmp",
        "data-protection-keys");
}
else
{
    string configuredKeyPath = builder.Configuration["DataProtection:Path"]
        ?? Path.Combine("App_Data", "DataProtection-Keys");
    dataProtectionKeyPath = Path.IsPathRooted(configuredKeyPath)
        ? configuredKeyPath
        : Path.Combine(builder.Environment.ContentRootPath, configuredKeyPath);
}

Directory.CreateDirectory(dataProtectionKeyPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
builder.Services.AddOptions<AiProvidersOptions>()
    .Bind(builder.Configuration.GetSection("AiProviders"));
builder.Services.AddScoped<OpenAiCompatibleApiClient>();
builder.Services.AddScoped<IAiProviderAdapter, OpenAiCompatibleAdapter>();
builder.Services.AddScoped<IAiCompletionRouter, AiCompletionRouter>();
builder.Services.AddScoped<ltwnc.Services.EnglishMission.IEnglishMissionService, ltwnc.Services.EnglishMission.EnglishMissionService>();


// Add MVC
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".ltwnc.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(2);
});
builder.Services.AddControllersWithViews(options =>
    options.Conventions.Add(new AdminAreaAuthorizationConvention()));
builder.Services.AddScoped<ltwnc.Controllers.ApiExceptionFilter>();

var app = builder.Build();

// Temporary one-time production repair endpoint; remove after migration.
app.MapPost("/__ltwnc_repair_migration", async (HttpRequest request, AppDbContext dbContext) =>
{
    if (!string.Equals(
            request.Headers["X-LTWNC-Repair"].ToString(),
            "1",
            StringComparison.Ordinal))
    {
        return Results.NotFound();
    }

    await dbContext.Database.MigrateAsync();
    return Results.Ok();
});

string? applyDatabaseMigrations = builder.Configuration["APPLY_DATABASE_MIGRATIONS"]
    ?? Environment.GetEnvironmentVariable("APPLY_DATABASE_MIGRATIONS");
if (string.Equals(
        applyDatabaseMigrations,
        "1",
        StringComparison.Ordinal))
{
    string migrationProbePath = Path.Combine(
        app.Environment.ContentRootPath,
        "App_Data",
        "migration-probe.txt");
    await File.WriteAllTextAsync(migrationProbePath, DateTimeOffset.UtcNow.ToString("O"));

    using IServiceScope scope = app.Services.CreateScope();
    AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (string.Equals(
        Environment.GetEnvironmentVariable("SMOKE_FIXTURES"),
        "1",
        StringComparison.Ordinal))
{
    await ltwnc.Services.Lessons.SmokeLessonFixtures.ApplyAsync(app.Services);
}

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
app.Use(async (context, next) =>
{
    // Security headers tối thiểu cho mọi response.
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    // CSP: self + CDN font/icon hiện dùng (Google Fonts, Phosphor unpkg); form SePay.
    context.Response.Headers.TryAdd(
        "Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://unpkg.com; " +
        "style-src-elem 'self' 'unsafe-inline' https://fonts.googleapis.com https://unpkg.com; " +
        "img-src 'self' data: blob: https:; " +
        "font-src 'self' data: https://fonts.gstatic.com https://unpkg.com; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self' https://pay.sepay.vn https://pay-sandbox.sepay.vn");
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapAreaControllerRoute(
    name: "admin-root",
    areaName: "Admin",
    pattern: "Admin",
    defaults: new { controller = "Dashboard", action = "Index" });
app.MapAreaControllerRoute(
    name: "admin",
    areaName: "Admin",
    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapStaticAssets();

app.Run();
