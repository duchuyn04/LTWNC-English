using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using ltwnc.Data;
using ltwnc.Services.Achievements;
using ltwnc.Services.Auth;
using ltwnc.Services.CardActions;
using ltwnc.Services.FlashcardSets;
using ltwnc.Services.Study;
using ltwnc.Services.StudyEvents;
using ltwnc.Services.StudyModes;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cookie authentication + custom auth services (no ASP.NET Identity)
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
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
    });

builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<ISignInService, CookieSignInService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Application services — inject qua interface (swap/decorator sau này không sửa controller)
builder.Services.AddScoped<IFlashcardSetService, FlashcardSetService>();
builder.Services.AddScoped<IFlashcardImportService, FlashcardImportService>();
builder.Services.AddScoped<CsvFlashcardFileParser>();
builder.Services.AddScoped<XlsxFlashcardFileParser>();
builder.Services.AddScoped<FlashcardFileParserResolver>();
builder.Services.AddScoped<IStudyService, StudyService>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<IQuizService, QuizService>();
// Service xử lý nghe chép chính tả
builder.Services.AddScoped<IDictationService, DictationService>();
builder.Services.AddScoped<ICardActionService, CardActionService>();
builder.Services.AddScoped<ICardActionCommandFactory, CardActionCommandFactory>();

// Study mode strategies
builder.Services.AddScoped<IStudyCardQueryService, StudyCardQueryService>();
builder.Services.AddScoped<IStudyModeStrategyResolver, StudyModeStrategyResolver>();
builder.Services.AddScoped<QuizQuestionFactory>();
builder.Services.AddScoped<IStudyModeStrategy, FlashcardModeStrategy>();
builder.Services.AddScoped<IStudyModeStrategy, DictationModeStrategy>();
builder.Services.AddScoped<IStudyModeStrategy, QuizModeStrategy>();
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


// Add MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Cấu hình middleware pipeline
// Cấu hình pipeline middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
