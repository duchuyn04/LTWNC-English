using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;

namespace ltwnc.Data;

// DbContext chính của ứng dụng — dùng ASP.NET Core Identity không roles.
// Quản lý kết nối database và cấu hình các bảng (entities)
public class AppDbContext : IdentityDbContext<IdentityUser>
{
    // Constructor — nhận DbContextOptions từ DI container (connection string, provider...)
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Định nghĩa các bảng trong database
    public DbSet<FlashcardSet> FlashcardSets => Set<FlashcardSet>();
    public DbSet<Flashcard> Flashcards => Set<Flashcard>();
    public DbSet<StudySession> StudySessions => Set<StudySession>();
    public DbSet<UserProgress> UserProgresses => Set<UserProgress>();
    public DbSet<UserStudySettings> UserStudySettings => Set<UserStudySettings>();
    public DbSet<DictationSessionDetail> DictationSessionDetails => Set<DictationSessionDetail>();
    public DbSet<CardActionLog> CardActionLogs => Set<CardActionLog>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    // Bảng thành tích (huy hiệu) user đã mở khóa — do Observer ghi khi có sự kiện học
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<AiProvider> AiProviders => Set<AiProvider>();
    public DbSet<EnglishMission> EnglishMissions => Set<EnglishMission>();
    public DbSet<EnglishMissionTargetWord> EnglishMissionTargetWords => Set<EnglishMissionTargetWord>();
    public DbSet<EnglishMissionTurn> EnglishMissionTurns => Set<EnglishMissionTurn>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<AiOperationLog> AiOperationLogs => Set<AiOperationLog>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();

    // Cấu hình model — indexes, relationships, constraints
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<IdentityUser>()
            .HasIndex(u => u.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("EmailIndex")
            .HasFilter("[NormalizedEmail] IS NOT NULL");

        builder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.UserId).HasMaxLength(450);
            entity.Property(profile => profile.Bio).HasMaxLength(500);
            entity.HasOne<IdentityUser>()
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
            // Index composite (UserId + FlashcardSetId) — tăng tốc truy vấn theo người dùng và bộ thẻ
            entity.HasIndex(e => new { e.UserId, e.FlashcardSetId });
            entity.HasIndex(e => new { e.CompletedAt, e.UserId });
            entity.HasIndex(e => e.StartedAt);
            // Quan hệ: nhiều StudySession thuộc về 1 FlashcardSet
            // Restrict = không cho xóa bộ thẻ nếu còn phiên học (tránh mất dữ liệu)
            entity.HasOne(e => e.FlashcardSet)
                  .WithMany()
                  .HasForeignKey(e => e.FlashcardSetId)
                  .OnDelete(DeleteBehavior.Restrict);
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

        builder.Entity<AiProvider>(entity =>
        {
            entity.HasIndex(provider => new { provider.IsEnabled, provider.Priority });
            // Chỉ cho phép tối đa một provider chính trong database.
            entity.HasIndex(provider => provider.IsPrimary)
                .IsUnique()
                .HasFilter("[IsPrimary] = 1");
            entity.Property(provider => provider.Name).HasMaxLength(120).IsRequired();
            entity.Property(provider => provider.AdapterType).HasMaxLength(80).IsRequired();
            entity.Property(provider => provider.BaseUrl).HasMaxLength(500).IsRequired();
            entity.Property(provider => provider.ModelId).HasMaxLength(200).IsRequired();
            entity.Property(provider => provider.ApiKeyLastFour).HasMaxLength(4);
            // Khóa phiên bản lạc quan: mọi lệnh UPDATE đều kiểm tra giá trị cũ
            // để chặn hai quản trị viên ghi đè thay đổi của nhau.
            entity.Property(provider => provider.Version).IsConcurrencyToken();
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
    }
}
