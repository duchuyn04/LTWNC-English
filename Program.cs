using Microsoft.EntityFrameworkCore;
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
using ltwnc.Services.Study;
using ltwnc.Services.StudyEvents;
using ltwnc.Services.StudyModes;
using ltwnc.Services.Profiles;
using ltwnc.Services.Leaderboard;

var builder = WebApplication.CreateBuilder(args);

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
        policy.RequireClaim(AdminAuthenticationSession.VerifiedAtClaim);
        policy.AddRequirements(new AdminTwoFactorEnabledRequirement());
    });
    options.AddPolicy(AdminAreaPolicy.RecentAuthenticationName, policy =>
    {
        policy.RequireRole(AdminRoleBootstrapper.AdminRole);
        policy.AddRequirements(new AdminTwoFactorEnabledRequirement());
        policy.AddRequirements(new RecentAdminAuthenticationRequirement(
            AdminAreaPolicy.RecentAuthenticationLifetime));
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
                if (context.HttpContext.User.IsInRole(AdminRoleBootstrapper.AdminRole))
                {
                    string returnUrl = context.Request.PathBase
                        + context.Request.Path
                        + context.Request.QueryString;
                    context.Response.Redirect(
                        $"/Account/AdminTwoFactor?returnUrl={Uri.EscapeDataString(returnUrl)}");
                    return Task.CompletedTask;
                }

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
    options.OnRefreshingPrincipal =
        AdminAuthenticationSession.PreserveVerificationClaimsAsync;
});

builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<AdminRoleBootstrapper>();
builder.Services.AddScoped<AdminAuthenticationSession>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    RecentAdminAuthenticationHandler>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    AdminTwoFactorEnabledHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IAvatarService, AvatarService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.Configure<RouteOptions>(options =>
    options.ConstraintMap["profileUsername"] = typeof(ProfileUsernameRouteConstraint));

// Application services — inject qua interface (swap/decorator sau này không sửa controller)
builder.Services.AddScoped<IFlashcardSetService, FlashcardSetService>();
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
