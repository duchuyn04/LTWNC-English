using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;

namespace ltwnc.Data;

public class AppDbContext : IdentityDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<FlashcardSet> FlashcardSets => Set<FlashcardSet>();
    public DbSet<Flashcard> Flashcards => Set<Flashcard>();
    public DbSet<StudySession> StudySessions => Set<StudySession>();
    public DbSet<UserProgress> UserProgresses => Set<UserProgress>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<FlashcardSet>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsPublic);
        });

        builder.Entity<Flashcard>(entity =>
        {
            entity.HasIndex(e => e.FlashcardSetId);
            entity.HasOne(e => e.FlashcardSet)
                  .WithMany(s => s.Flashcards)
                  .HasForeignKey(e => e.FlashcardSetId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StudySession>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.FlashcardSetId });
        });

        builder.Entity<UserProgress>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.FlashcardId }).IsUnique();
        });
    }
}
