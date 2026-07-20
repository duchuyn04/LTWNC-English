using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;

namespace ltwnc.Data;

// DbContext chính của ứng dụng — cookie auth dùng bảng Users (AppUser), không Identity.
// Quản lý kết nối database và cấu hình các bảng (entities)
public class AppDbContext : DbContext
{
    // Constructor — nhận DbContextOptions từ DI container (connection string, provider...)
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Định nghĩa các bảng trong database
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<FlashcardSet> FlashcardSets => Set<FlashcardSet>();
    public DbSet<Flashcard> Flashcards => Set<Flashcard>();
    public DbSet<StudySession> StudySessions => Set<StudySession>();
    public DbSet<QuizSessionQuestion> QuizSessionQuestions => Set<QuizSessionQuestion>();
    public DbSet<UserProgress> UserProgresses => Set<UserProgress>();
    public DbSet<UserStudySettings> UserStudySettings => Set<UserStudySettings>();
    public DbSet<DictationSessionDetail> DictationSessionDetails => Set<DictationSessionDetail>();
    public DbSet<CardActionLog> CardActionLogs => Set<CardActionLog>();

    // Bảng thành tích (huy hiệu) user đã mở khóa — do Observer ghi khi có sự kiện học
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();

    // Cấu hình model — indexes, relationships, constraints
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // DbContext chuẩn — không gọi Identity model configuration
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.UserName).IsUnique();
        });

        // Cấu hình bảng FlashcardSets
        builder.Entity<FlashcardSet>(entity =>
        {
            // Index cho UserId — tăng tốc truy vấn "lấy bộ thẻ theo người dùng"
            entity.HasIndex(e => e.UserId);
            // Index cho IsPublic — tăng tốc truy vấn "lấy bộ thẻ public"
            entity.HasIndex(e => e.IsPublic);
            entity.HasIndex(e => new { e.UserId, e.SourceSetId })
                .IsUnique()
                .HasFilter("[SourceSetId] IS NOT NULL");
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
            entity.HasIndex(e => new { e.UserId, e.FlashcardSetId, e.Mode })
                .IsUnique()
                .HasFilter("[Mode] = 1 AND [Score] IS NULL AND [CompletedAt] IS NULL");
            entity.HasIndex(e => new { e.CompletedAt, e.UserId });
            entity.HasIndex(e => e.StartedAt);
            // Quan hệ: nhiều StudySession thuộc về 1 FlashcardSet
            // Restrict = không cho xóa bộ thẻ nếu còn phiên học (tránh mất dữ liệu)
            entity.HasOne(e => e.FlashcardSet)
                  .WithMany()
                  .HasForeignKey(e => e.FlashcardSetId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Cấu hình bảng QuizSessionQuestions
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
    }
}
