using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ltwnc.Services.Lessons;

/// <summary>
/// Development/smoke-only seed. Enabled when env SMOKE_FIXTURES=1.
/// </summary>
public static class SmokeLessonFixtures
{
    public const string LearnerUserName = "smoke_learner";
    public const string AdminUserName = "smoke_admin";
    public const string Password = "SmokeTest1a";
    public const string LessonTitle = "Smoke Lesson";
    public const string McqPrompt = "Smoke MCQ: which is correct?";
    public const string WritingPrompt = "Smoke writing: type works";
    public const string WritingAnswer = "works";

    public static async Task ApplyAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = services.CreateScope();
        ILogger logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SmokeLessonFixtures");
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        IPasswordHasher<AppUser> hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        TimeProvider time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        DateTime now = time.GetUtcNow().UtcDateTime;

        // Schema must already be current (run `dotnet ef database update` on the target DB).
        // Full MigrateAsync on a fresh DB can fail on legacy Identity drop migrations.
        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException("Smoke fixtures cannot connect to the database.");
        }

        AppUser learner = await EnsureUserAsync(
            db, hasher, LearnerUserName, "smoke.learner@example.com", isAdmin: false, now, cancellationToken);
        AppUser admin = await EnsureUserAsync(
            db, hasher, AdminUserName, "smoke.admin@example.com", isAdmin: true, now, cancellationToken);

        Lesson lesson = await db.Lessons
            .Include(row => row.Questions)
            .SingleOrDefaultAsync(row => row.Title == LessonTitle, cancellationToken)
            ?? new Lesson
            {
                Title = LessonTitle,
                Summary = "Fixture for Playwright smoke.",
                ContentMarkdown = "# Smoke\n\nBody for smoke lesson.",
                Status = LessonStatus.Published,
                SortOrder = 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CreatedByUserId = admin.Id,
                UpdatedByUserId = admin.Id
            };

        if (lesson.Id == 0)
        {
            db.Lessons.Add(lesson);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            lesson.Status = LessonStatus.Published;
            lesson.ContentMarkdown = "# Smoke\n\nBody for smoke lesson.";
            lesson.UpdatedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!lesson.Questions.Any(q => q.Type == LessonQuestionTypes.MultipleChoice && q.Prompt == McqPrompt))
        {
            db.LessonQuestions.Add(new LessonQuestion
            {
                LessonId = lesson.Id,
                Type = LessonQuestionTypes.MultipleChoice,
                Prompt = McqPrompt,
                SortOrder = 1,
                OptionsJson = """["wrong","right","also wrong"]""",
                CorrectOptionIndex = 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        if (!lesson.Questions.Any(q => q.Type == LessonQuestionTypes.Writing && q.Prompt == WritingPrompt))
        {
            db.LessonQuestions.Add(new LessonQuestion
            {
                LessonId = lesson.Id,
                Type = LessonQuestionTypes.Writing,
                Prompt = WritingPrompt,
                SortOrder = 2,
                AcceptedAnswersJson = """["works"]""",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Smoke fixtures ready. learner={Learner} admin={Admin} lesson={Lesson}",
            learner.UserName,
            admin.UserName,
            lesson.Title);
    }

    private static async Task<AppUser> EnsureUserAsync(
        AppDbContext db,
        IPasswordHasher<AppUser> hasher,
        string userName,
        string email,
        bool isAdmin,
        DateTime now,
        CancellationToken cancellationToken)
    {
        string normalized = userName.ToUpperInvariant();
        AppUser? user = await db.AppUsers
            .SingleOrDefaultAsync(row => row.NormalizedUserName == normalized, cancellationToken);

        if (user is null)
        {
            user = new AppUser
            {
                UserName = userName,
                NormalizedUserName = normalized,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                IsAdmin = isAdmin,
                CreditBalance = 10
            };
            user.PasswordHash = hasher.HashPassword(user, Password);
            db.AppUsers.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            return user;
        }

        user.IsAdmin = isAdmin;
        user.EmailConfirmed = true;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        user.PasswordHash = hasher.HashPassword(user, Password);
        user.SecurityStamp = Guid.NewGuid().ToString();
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }
}
