using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.RateLimiting;
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
using ltwnc.Services.AdminExports;
using ltwnc.Services.AdminAuditRetention;
using ltwnc.Services.AdminAchievements;
using ltwnc.Services.AdminSearch;
using ltwnc.Services.AdminUsers;
using ltwnc.Services.AdminEnglishMissions;
using ltwnc.Services.Study;
using ltwnc.Services.StudyEvents;
using ltwnc.Services.StudyModes;
using ltwnc.Services.Profiles;
using ltwnc.Services.Leaderboard;

var builder = WebApplication.CreateBuilder(args);

// Giới hạn logging vào console/debug để môi trường local không bị lỗi quyền ghi Windows EventLog.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ASP.NET Core Identity (UserManager/SignInManager), không roles.
builder.Services.AddHttpContextAccessor();

builder.Services.AddIdentityCore<IdentityUser>(options =>
    {
        // Password policy giữ nguyên như custom auth cũ
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = UsernamePolicy.AllowedIdentityCharacters;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminAreaPolicy.Name, policy =>
    {
        policy.RequireRole(AdminRoleBootstrapper.AdminRole);
    });
});

builder.Services.Configure<CookieAuthenticationOptions>(
    IdentityConstants.ApplicationScheme,
    options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
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
            // Giữ kiểm tra security stamp mặc định của Identity để cookie cũ mất hiệu lực khi stamp đổi.
            await SecurityStampValidator.ValidatePrincipalAsync(context);
            if (context.Principal?.Identity?.IsAuthenticated != true)
            {
                return;
            }

            string? userId = context.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                return;
            }

            UserManager<IdentityUser> userManager =
                context.HttpContext.RequestServices.GetRequiredService<UserManager<IdentityUser>>();
            IdentityUser? user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                return;
            }

            // Tài khoản đã bị khóa không được tiếp tục dùng cookie còn tồn tại ở trình duyệt.
            if (await userManager.IsLockedOutAsync(user))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            }
        };
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ltwnc.Services.Audit.IAdminAuditService, ltwnc.Services.Audit.AdminAuditService>();
builder.Services.AddScoped<IAdminDashboardKpiService, AdminDashboardKpiService>();
builder.Services.AddScoped<IAdminExportService, AdminExportService>();
builder.Services.AddScoped<IAdminAuditRetentionService, AdminAuditRetentionService>();
builder.Services.AddSingleton<AdminAchievementSyncCoordinator>();
builder.Services.AddScoped<IAdminAchievementService, AdminAchievementService>();
builder.Services.AddScoped<IAdminGlobalSearchService, AdminGlobalSearchService>();
builder.Services.AddSingleton<AdminUserLockCoordinator>();
builder.Services.AddScoped<IAdminUserAccountService, AdminUserAccountService>();
builder.Services.AddScoped<ltwnc.Services.AdminStudyRecords.IAdminStudyRecordService,
    ltwnc.Services.AdminStudyRecords.AdminStudyRecordService>();
builder.Services.AddScoped<IAdminEnglishMissionService, AdminEnglishMissionService>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    // Tác vụ nền dọn transcript English Mission theo batch; bỏ qua Testing để test kiểm soát thời gian chủ động.
    builder.Services.AddHostedService<EnglishMissionConversationCleanupHostedService>();
    // Tác vụ nền dọn audit quá hạn theo batch; log chỉ chứa trạng thái vận hành và số lượng.
    builder.Services.AddHostedService<AdminAuditRetentionCleanupHostedService>();
}
builder.Services.AddScoped<AdminRoleBootstrapper>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IAvatarService, AvatarService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.Configure<RouteOptions>(options =>
    options.ConstraintMap["profileUsername"] = typeof(ProfileUsernameRouteConstraint));

// Application services — inject qua interface (swap/decorator sau này không sửa controller)
builder.Services.AddScoped<IFlashcardSetService, FlashcardSetService>();
builder.Services.AddScoped<IContentReportService, ContentReportService>();
builder.Services.AddScoped<IContentModerationService, ContentModerationService>();
builder.Services.AddScoped<IFlashcardImportService, FlashcardImportService>();
builder.Services.AddScoped<CsvFlashcardFileParser>();
builder.Services.AddScoped<XlsxFlashcardFileParser>();
builder.Services.AddScoped<FlashcardFileParserResolver>();
builder.Services.AddScoped<IStudyService, StudyService>();
// Service xử lý nghe chép chính tả
builder.Services.AddScoped<IDictationService, DictationService>();
builder.Services.AddScoped<ICardActionService, CardActionService>();
builder.Services.AddScoped<ICardActionCommandFactory, CardActionCommandFactory>();

// Study mode strategies
builder.Services.AddScoped<IStudyCardQueryService, StudyCardQueryService>();
builder.Services.AddScoped<IStudyModeStrategyResolver, StudyModeStrategyResolver>();
builder.Services.AddScoped<IStudyModeStrategy, FlashcardModeStrategy>();
builder.Services.AddScoped<IStudyModeStrategy, DictationModeStrategy>();
builder.Services.AddScoped<IStudyModeStrategy, EnglishMissionModeStrategy>();

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
builder.Services.AddScoped<ltwnc.Services.Ai.OpenAiCompatibleClient>();
builder.Services.AddScoped<ltwnc.Services.Ai.IAiProviderAdapter, ltwnc.Services.Ai.OpenAiCompatibleAdapter>();
builder.Services.AddScoped<ltwnc.Services.Ai.IAiCompletionRouter, ltwnc.Services.Ai.AiCompletionRouter>();
builder.Services.AddScoped<ltwnc.Services.Ai.IAiProviderService, ltwnc.Services.Ai.AiProviderService>();
builder.Services.AddScoped<ltwnc.Services.EnglishMission.IEnglishMissionService, ltwnc.Services.EnglishMission.EnglishMissionService>();


// Add MVC
builder.Services.AddControllersWithViews(options =>
    options.Conventions.Add(new AdminAreaAuthorizationConvention()));
builder.Services.AddScoped<ltwnc.Controllers.ApiExceptionFilter>();

var app = builder.Build();

string? bootstrapAdminUserId = app.Configuration["AdminBootstrap:UserId"];
if (!string.IsNullOrWhiteSpace(bootstrapAdminUserId))
{
    using IServiceScope scope = app.Services.CreateScope();
    AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    bool schemaIsCurrent = !(await dbContext.Database.GetPendingMigrationsAsync()).Any();
    AdminRoleBootstrapper adminBootstrapper =
        scope.ServiceProvider.GetRequiredService<AdminRoleBootstrapper>();
    await adminBootstrapper.BootstrapAsync(schemaIsCurrent);
}

// Cấu hình middleware pipeline
// Cấu hình pipeline middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCodePage");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

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

public partial class Program { }
