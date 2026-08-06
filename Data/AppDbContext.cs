using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;

namespace ltwnc.Data;

// DbContext chính của ứng dụng — auth tự quản qua bảng AppUsers.
public class AppDbContext : DbContext
{
    // Constructor — nhận DbContextOptions từ DI container (connection string, provider...)
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Định nghĩa các bảng trong database
    public DbSet<FlashcardSet> FlashcardSets => Set<FlashcardSet>();
    public DbSet<Flashcard> Flashcards => Set<Flashcard>();
    public DbSet<StudySession> StudySessions => Set<StudySession>();
    public DbSet<QuizSessionQuestion> QuizSessionQuestions => Set<QuizSessionQuestion>();
    public DbSet<UserProgress> UserProgresses => Set<UserProgress>();
    public DbSet<UserStudySettings> UserStudySettings => Set<UserStudySettings>();
    public DbSet<ReviewSettings> ReviewSettings => Set<ReviewSettings>();
    public DbSet<ReviewProgress> ReviewProgresses => Set<ReviewProgress>();
    public DbSet<ReviewSession> ReviewSessions => Set<ReviewSession>();
    public DbSet<ReviewSessionItem> ReviewSessionItems => Set<ReviewSessionItem>();
    public DbSet<DictationSessionDetail> DictationSessionDetails => Set<DictationSessionDetail>();
    public DbSet<DictationSessionQuestion> DictationSessionQuestions => Set<DictationSessionQuestion>();
    public DbSet<CardActionLog> CardActionLogs => Set<CardActionLog>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<PendingRegistration> PendingRegistrations => Set<PendingRegistration>();
    public DbSet<EmailOtpChallenge> EmailOtpChallenges => Set<EmailOtpChallenge>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    // Bảng thành tích (huy hiệu) user đã mở khóa — do Observer ghi khi có sự kiện học
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<EnglishMission> EnglishMissions => Set<EnglishMission>();
    public DbSet<EnglishMissionTargetWord> EnglishMissionTargetWords => Set<EnglishMissionTargetWord>();
    public DbSet<EnglishMissionTurn> EnglishMissionTurns => Set<EnglishMissionTurn>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<AiOperationLog> AiOperationLogs => Set<AiOperationLog>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<CreditPackage> CreditPackages => Set<CreditPackage>();
    public DbSet<CreditPurchase> CreditPurchases => Set<CreditPurchase>();
    public DbSet<CreditLedgerEntry> CreditLedgerEntries => Set<CreditLedgerEntry>();
    public DbSet<Lesson> Lessons => Set<Lesson>();

    // Cấu hình model — indexes, relationships, constraints
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(entity =>
        {
            entity.Property(user => user.Id).HasMaxLength(450);
            entity.Property(user => user.Email).HasMaxLength(256);
            entity.Property(user => user.NormalizedEmail).HasMaxLength(256);
            entity.Property(user => user.UserName).HasMaxLength(256);
            entity.Property(user => user.NormalizedUserName).HasMaxLength(256);
            entity.Property(user => user.GoogleSubjectId).HasMaxLength(256);
            entity.HasIndex(user => user.GoogleSubjectId)
                .IsUnique()
                .HasDatabaseName("AppUserGoogleSubjectIndex")
                .HasFilter("[GoogleSubjectId] IS NOT NULL");
            entity.HasIndex(user => user.NormalizedEmail)
                .IsUnique()
                .HasDatabaseName("AppUserEmailIndex")
                .HasFilter("[NormalizedEmail] IS NOT NULL");
            entity.HasIndex(user => user.NormalizedUserName)
                .IsUnique()
                .HasDatabaseName("AppUserNameIndex")
                .HasFilter("[NormalizedUserName] IS NOT NULL");
            entity.Property(user => user.CreditBalance).HasDefaultValue(10);
            entity.Property(user => user.CreditVersion).IsConcurrencyToken();
        });

        builder.Entity<PendingRegistration>(entity =>
        {
            entity.Property(item => item.Id).HasMaxLength(450);
            entity.Property(item => item.Email).HasMaxLength(256).IsRequired();
            entity.Property(item => item.NormalizedEmail).HasMaxLength(256).IsRequired();
            entity.Property(item => item.UserName).HasMaxLength(256).IsRequired();
            entity.Property(item => item.NormalizedUserName).HasMaxLength(256).IsRequired();
            entity.Property(item => item.PasswordHash).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => item.NormalizedEmail).IsUnique();
            entity.HasIndex(item => item.NormalizedUserName).IsUnique();
        });

        builder.Entity<EmailOtpChallenge>(entity =>
        {
            entity.Property(item => item.Id).HasMaxLength(450);
            entity.Property(item => item.Purpose).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.Email).HasMaxLength(256).IsRequired();
            entity.Property(item => item.NormalizedEmail).HasMaxLength(256).IsRequired();
            entity.Property(item => item.UserId).HasMaxLength(450);
            entity.Property(item => item.PendingRegistrationId).HasMaxLength(450);
            entity.Property(item => item.GoogleSubjectId).HasMaxLength(256);
            entity.Property(item => item.CodeHash).HasMaxLength(500).IsRequired();
            entity.Property(item => item.RequestIpAddress).HasMaxLength(64);
            entity.HasIndex(item => new { item.NormalizedEmail, item.CreatedAtUtc });
            entity.HasIndex(item => new { item.RequestIpAddress, item.CreatedAtUtc });
        });

        builder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.UserId).HasMaxLength(450);
            entity.Property(profile => profile.Bio).HasMaxLength(500);
            entity.HasOne<AppUser>()
                .WithOne()
                .HasForeignKey<UserProfile>(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(profile => new { profile.IsPublic, profile.ShowStats });
            entity.HasIndex(profile => profile.CreatedAt);
        });

        // Cấu hình bảng FlashcardSets
        builder.Entity<FlashcardSet>(entity =>
        {
            // Index cho UserId — tăng tốc truy vấn "lấy bộ thẻ theo người dùng"
            entity.HasIndex(e => e.UserId);
            // Index cho IsPublic — tăng tốc truy vấn "lấy bộ thẻ public"
            entity.HasIndex(e => e.IsPublic);
            // Index ghép cho truy vấn nội dung công khai: public và chưa bị cách ly.
            entity.HasIndex(e => new { e.IsPublic, e.ModerationStatus, e.UpdatedAt });
            // Index phục vụ tìm kiếm Admin theo prefix tiêu đề mà không đọc nội dung thẻ trong bộ.
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => new { e.UserId, e.SourceSetId })
                .IsUnique()
                .HasFilter("[SourceSetId] IS NOT NULL");
            entity.Property(e => e.ModerationStatus).HasMaxLength(40).IsRequired();
            entity.Property(e => e.ModerationPublicReason).HasMaxLength(500);
            entity.Property(e => e.ModerationInternalNote).HasMaxLength(1000);
            entity.Property(e => e.ModerationEvidence).HasMaxLength(1000);
            entity.Property(e => e.ModeratedByUserId).HasMaxLength(450);
            entity.Property(e => e.ModerationVersion).IsConcurrencyToken();
            entity.Property(e => e.NewCardQuota)
                .HasDefaultValue(ReviewSettingsPolicy.DefaultNewCardQuota);
            entity.Property(e => e.ReviewPaused)
                .HasDefaultValue(false);
        });

        // Cấu hình bảng Flashcards
        builder.Entity<Flashcard>(entity =>
        {
            // Index cho FlashcardSetId — tăng tốc truy vấn "lấy thẻ theo bộ"
            entity.HasIndex(e => e.FlashcardSetId);
            entity.HasIndex(e => new { e.FlashcardSetId, e.IsStarred });
            // Quan hệ: 1 FlashcardSet có nhiều Flashcards
            // Cascade = xóa bộ thẻ sẽ xóa tất cả thẻ bên trong
            entity.HasOne(e => e.FlashcardSet)
                  .WithMany(s => s.Flashcards)
                  .HasForeignKey(e => e.FlashcardSetId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng StudySessions
        builder.Entity<StudySession>(entity =>
        {
            entity.Property(e => e.PlannedItemCount).HasDefaultValue(0);
            if (Database.IsSqlServer())
            {
                entity.Property(e => e.StartedAt).HasDefaultValueSql("GETUTCDATE()");
            }
            else
            {
                entity.Property(e => e.StartedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            }
            // Index composite (UserId + FlashcardSetId) — tăng tốc truy vấn theo người dùng và bộ thẻ
            entity.HasIndex(e => new { e.UserId, e.FlashcardSetId });
            entity.HasIndex(e => new { e.CompletedAt, e.UserId });
            entity.HasIndex(e => e.StartedAt);
            entity.Property(e => e.CompletedAt).IsConcurrencyToken();
            entity.HasIndex(e => new { e.UserId, e.FlashcardSetId, e.Mode })
                .IsUnique()
                .HasFilter("[Mode] = 1 AND [Score] IS NULL AND [CompletedAt] IS NULL");
            // Quan hệ: nhiều StudySession thuộc về 1 FlashcardSet
            // Restrict = không cho xóa bộ thẻ nếu còn phiên học (tránh mất dữ liệu)
            entity.HasOne(e => e.FlashcardSet)
                  .WithMany()
                  .HasForeignKey(e => e.FlashcardSetId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<QuizSessionQuestion>(entity =>
        {
            entity.HasIndex(e => e.StudySessionId);
            entity.HasIndex(e => new { e.StudySessionId, e.OrderIndex }).IsUnique();
            entity.HasIndex(e => new { e.StudySessionId, e.FlashcardId }).IsUnique();
            entity.HasOne(e => e.StudySession)
                .WithMany()
                .HasForeignKey(e => e.StudySessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng UserProgresses
        builder.Entity<UserProgress>(entity =>
        {
            // Unique index (UserId + FlashcardId) — mỗi người chỉ có 1 tiến trình cho mỗi thẻ
            entity.HasIndex(e => new { e.UserId, e.FlashcardId }).IsUnique();
            entity.HasIndex(e => e.LastReviewed);
            // Quan hệ: nhiều UserProgress thuộc về 1 Flashcard
            // Restrict = không cho xóa thẻ nếu còn tiến trình học
            entity.HasOne(e => e.Flashcard)
                  .WithMany()
                  .HasForeignKey(e => e.FlashcardId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserStudySettings>(entity =>
        {
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.ReviewSessionSize)
                .HasDefaultValue(ReviewSettingsPolicy.DefaultSessionSize);
            entity.Property(e => e.ReviewMaxIntervalDays)
                .HasDefaultValue(ReviewSettingsPolicy.DefaultMaxIntervalDays);
        });

        builder.Entity<ReviewSettings>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.FlashcardSetId })
                .IsUnique();
            entity.Property(e => e.ReviewSessionSize)
                .HasDefaultValue(ReviewSettingsPolicy.DefaultSessionSize);
            entity.Property(e => e.NewCardQuota)
                .HasDefaultValue(ReviewSettingsPolicy.DefaultNewCardQuota);
            entity.Property(e => e.ReviewMaxIntervalDays)
                .HasDefaultValue(ReviewSettingsPolicy.DefaultMaxIntervalDays);
            entity.HasOne(e => e.FlashcardSet)
                .WithMany()
                .HasForeignKey(e => e.FlashcardSetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ReviewProgress>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.FlashcardId }).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.NextReviewAtUtc });
            entity.HasOne(e => e.Flashcard)
                .WithMany()
                .HasForeignKey(e => e.FlashcardId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReviewSession>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.CompletedAtUtc });
            entity.HasIndex(e => new { e.UserId, e.FlashcardSetId, e.CompletedAtUtc });
            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasFilter("[CompletedAtUtc] IS NULL AND [EndedAtUtc] IS NULL");
        });

        builder.Entity<ReviewSessionItem>(entity =>
        {
            entity.HasIndex(e => e.NewCardAssignedDate);
            entity.HasIndex(e => new { e.ReviewSessionId, e.OrderIndex }).IsUnique();
            entity.HasIndex(e => new { e.ReviewSessionId, e.FlashcardId }).IsUnique();
            entity.Property(e => e.RatedAtUtc).IsConcurrencyToken();
            entity.HasOne(e => e.ReviewSession)
                .WithMany(session => session.Items)
                .HasForeignKey(e => e.ReviewSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Flashcard)
                .WithMany()
                .HasForeignKey(e => e.FlashcardId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CardActionLog>(entity =>
        {
            entity.HasIndex(e => new { e.SetId, e.UserId, e.UndoneAt });
        });

        // Mỗi user chỉ nhận mỗi mã huy hiệu một lần (không trùng)
        builder.Entity<UserAchievement>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Code }).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        // Cấu hình bảng DictationSessionDetails
        builder.Entity<DictationSessionDetail>(entity =>
        {
            // Index để lấy nhanh các câu trả lời của một phiên
            entity.HasIndex(e => e.StudySessionId);
            entity.HasIndex(e => new { e.StudySessionId, e.FlashcardId }).IsUnique();

            // Quan hệ: nhiều detail thuộc về 1 session
            // Cascade xóa: xóa phiên sẽ xóa luôn chi tiết
            entity.HasOne(e => e.StudySession)
                  .WithMany()
                  .HasForeignKey(e => e.StudySessionId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Quan hệ: nhiều detail thuộc về 1 flashcard
            // Restrict: không cho xóa thẻ nếu còn lịch sử trả lời
            entity.HasOne(e => e.Flashcard)
                  .WithMany()
                  .HasForeignKey(e => e.FlashcardId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DictationSessionQuestion>(entity =>
        {
            entity.HasIndex(e => new { e.StudySessionId, e.OrderIndex }).IsUnique();
            entity.HasIndex(e => new { e.StudySessionId, e.FlashcardId }).IsUnique();
            entity.Property(e => e.IsCorrect).IsConcurrencyToken();
            entity.HasOne(e => e.StudySession)
                  .WithMany()
                  .HasForeignKey(e => e.StudySessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AiOperationLog>(entity =>
        {
            entity.HasIndex(log => log.OccurredAtUtc);
            entity.HasIndex(log => new { log.OccurredAtUtc, log.Succeeded });
            entity.HasIndex(log => new { log.ProviderId, log.OccurredAtUtc, log.Succeeded });
            entity.Property(log => log.ProviderName).HasMaxLength(120);
            entity.Property(log => log.ModelId).HasMaxLength(200);
            entity.Property(log => log.Operation).HasMaxLength(80).IsRequired();
            entity.Property(log => log.FailureKind).HasMaxLength(80);
        });

        builder.Entity<EnglishMission>(entity =>
        {
            entity.HasIndex(mission => mission.StudySessionId).IsUnique();
            entity.HasIndex(mission => mission.CreatedAt);
            // Index phục vụ tác vụ dọn nội dung hội thoại đã quá hạn theo lô.
            entity.HasIndex(mission => new
            {
                mission.ConversationContentDeletedAtUtc,
                mission.CreatedAt
            });
            entity.HasOne(mission => mission.StudySession)
                .WithOne()
                .HasForeignKey<EnglishMission>(mission => mission.StudySessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(mission => mission.Topic).HasMaxLength(80).IsRequired();
            entity.Property(mission => mission.Title).HasMaxLength(200).IsRequired();
            entity.Property(mission => mission.Status).HasMaxLength(40).IsRequired();
            entity.Property(mission => mission.ConversationRetentionCaseType).HasMaxLength(80);
            entity.Property(mission => mission.ConversationRetentionCaseReference).HasMaxLength(120);
            entity.Property(mission => mission.GoalsJson).IsRequired();
            entity.Property(mission => mission.RowVersion).IsRowVersion();
        });

        builder.Entity<EnglishMissionTargetWord>(entity =>
        {
            entity.HasIndex(word => word.EnglishMissionId);
            entity.HasOne(word => word.Mission)
                .WithMany(mission => mission.TargetWords)
                .HasForeignKey(word => word.EnglishMissionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(word => word.Flashcard)
                .WithMany()
                .HasForeignKey(word => word.FlashcardId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(word => word.Term).HasMaxLength(160).IsRequired();
            entity.Property(word => word.Definition).HasMaxLength(500).IsRequired();
        });

        builder.Entity<EnglishMissionTurn>(entity =>
        {
            entity.HasIndex(turn => turn.EnglishMissionId);
            entity.HasIndex(turn => new { turn.EnglishMissionId, turn.ClientTurnId }).IsUnique();
            entity.HasOne(turn => turn.Mission)
                .WithMany(mission => mission.Turns)
                .HasForeignKey(turn => turn.EnglishMissionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(turn => turn.UserText).HasMaxLength(1000).IsRequired();
            entity.Property(turn => turn.ClientTurnId).HasMaxLength(64).IsRequired();
            entity.Property(turn => turn.NpcText).HasMaxLength(2000).IsRequired();
            entity.Property(turn => turn.UsedWordsJson).IsRequired();
            entity.Property(turn => turn.AchievedGoalsJson).IsRequired();
        });

        builder.Entity<AdminAuditLog>(entity =>
        {
            entity.HasIndex(log => log.OccurredAtUtc);
            entity.HasIndex(log => log.Action);
            entity.HasIndex(log => log.ActorUserId);
            entity.HasIndex(log => new { log.TargetType, log.TargetId });
        });

        builder.Entity<ContentReport>(entity =>
        {
            entity.HasIndex(report => report.CreatedAtUtc);
            entity.HasIndex(report => new { report.Status, report.CreatedAtUtc });
            entity.HasIndex(report => new { report.FlashcardSetId, report.Status });
            entity.HasIndex(report => new { report.ReporterUserId, report.FlashcardSetId, report.Status })
                .IsUnique()
                .HasFilter("[Status] = 'Pending'");
            entity.Property(report => report.Reason).HasMaxLength(80).IsRequired();
            entity.Property(report => report.Status).HasMaxLength(40).IsRequired();
            entity.Property(report => report.Description).HasMaxLength(1000);
            entity.Property(report => report.ResolutionReason).HasMaxLength(500);
            entity.Property(report => report.Version).IsConcurrencyToken();
            entity.HasOne(report => report.FlashcardSet)
                .WithMany()
                .HasForeignKey(report => report.FlashcardSetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CreditPackage>(entity =>
        {
            entity.Property(package => package.Name).HasMaxLength(120).IsRequired();
            entity.Property(package => package.Description).HasMaxLength(500);
            entity.Property(package => package.Version).IsConcurrencyToken();
            entity.HasIndex(package => new { package.IsArchived, package.IsActive, package.DisplayOrder });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_CreditPackages_PriceVnd", "[PriceVnd] > 0");
                table.HasCheckConstraint("CK_CreditPackages_Credits", "[Credits] > 0");
            });
        });

        builder.Entity<CreditPurchase>(entity =>
        {
            entity.Property(purchase => purchase.UserId).HasMaxLength(450).IsRequired();
            entity.Property(purchase => purchase.InvoiceNumber).HasMaxLength(64).IsRequired();
            entity.Property(purchase => purchase.PackageName).HasMaxLength(120).IsRequired();
            entity.Property(purchase => purchase.Currency).HasMaxLength(3).IsRequired();
            entity.Property(purchase => purchase.Status).HasMaxLength(32).IsRequired();
            entity.Property(purchase => purchase.Version).IsConcurrencyToken();
            entity.HasIndex(purchase => purchase.InvoiceNumber).IsUnique();
            entity.HasIndex(purchase => purchase.SePayTransactionId)
                .IsUnique()
                .HasFilter("[SePayTransactionId] IS NOT NULL");
            entity.HasIndex(purchase => new { purchase.UserId, purchase.Status, purchase.ExpiresAtUtc });
            entity.HasIndex(purchase => purchase.UserId)
                .IsUnique()
                .HasFilter("[Status] = 'Pending'");
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(purchase => purchase.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(purchase => purchase.Package)
                .WithMany()
                .HasForeignKey(purchase => purchase.CreditPackageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CreditLedgerEntry>(entity =>
        {
            entity.Property(entry => entry.UserId).HasMaxLength(450).IsRequired();
            entity.Property(entry => entry.Type).HasMaxLength(40).IsRequired();
            entity.Property(entry => entry.SourceType).HasMaxLength(60).IsRequired();
            entity.Property(entry => entry.SourceId).HasMaxLength(100).IsRequired();
            entity.Property(entry => entry.Description).HasMaxLength(500).IsRequired();
            entity.HasIndex(entry => new { entry.SourceType, entry.SourceId }).IsUnique();
            entity.HasIndex(entry => new { entry.UserId, entry.CreatedAtUtc });
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(entry => entry.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
                table.HasCheckConstraint("CK_CreditLedgerEntries_BalanceAfter", "[BalanceAfter] >= 0"));
        });

        builder.Entity<Lesson>(entity =>
        {
            entity.Property(lesson => lesson.Title).HasMaxLength(200).IsRequired();
            entity.Property(lesson => lesson.Summary).HasMaxLength(500);
            entity.Property(lesson => lesson.ContentMarkdown).IsRequired();
            entity.Property(lesson => lesson.Status).HasMaxLength(40).IsRequired();
            entity.Property(lesson => lesson.CreatedByUserId).HasMaxLength(450);
            entity.Property(lesson => lesson.UpdatedByUserId).HasMaxLength(450);
            entity.HasIndex(lesson => new { lesson.Status, lesson.SortOrder });
        });
    }
}
