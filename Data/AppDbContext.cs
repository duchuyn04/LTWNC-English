using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;

namespace ltwnc.Data;

// DbContext chính của ứng dụng — kế thừa IdentityDbContext để hỗ trợ ASP.NET Identity
// Quản lý kết nối database và cấu hình các bảng (entities)
public class AppDbContext : IdentityDbContext
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

    // Cấu hình model — indexes, relationships, constraints
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Gọi base để tạo các bảng của ASP.NET Identity (Users, Roles, Claims...)
        base.OnModelCreating(builder);

        // Cấu hình bảng FlashcardSets
        builder.Entity<FlashcardSet>(entity =>
        {
            // Index cho UserId — tăng tốc truy vấn "lấy bộ thẻ theo người dùng"
            entity.HasIndex(e => e.UserId);
            // Index cho IsPublic — tăng tốc truy vấn "lấy bộ thẻ public"
            entity.HasIndex(e => e.IsPublic);
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
