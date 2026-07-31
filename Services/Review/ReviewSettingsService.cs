using System.Collections.Concurrent;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Review;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Review;

public sealed class ReviewSettingsService : IReviewSettingsService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CreationLocks = new();
    private readonly AppDbContext _context;

    public ReviewSettingsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ReviewSettingsViewModel?> GetAsync(
        string userId,
        int flashcardSetId,
        CancellationToken cancellationToken = default)
    {
        FlashcardSet? set = await GetOwnedSetAsync(userId, flashcardSetId, cancellationToken);
        if (set == null)
        {
            return null;
        }

        ReviewSettings? settings = await _context.ReviewSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.UserId == userId && value.FlashcardSetId == set.Id,
                cancellationToken);

        return settings == null ? null : ReviewSettingsMapper.ToViewModel(settings);
    }

    public async Task<ReviewSettingsViewModel?> GetOrCreateAsync(
        string userId,
        int flashcardSetId,
        CancellationToken cancellationToken = default)
    {
        FlashcardSet? set = await GetOwnedSetAsync(userId, flashcardSetId, cancellationToken);
        if (set == null)
        {
            return null;
        }

        string lockKey = $"{userId}\u001f{set.Id}";
        SemaphoreSlim gate = CreationLocks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            ReviewSettings? existing = await FindAsync(userId, set.Id, cancellationToken);
            if (existing != null)
            {
                return ReviewSettingsMapper.ToViewModel(existing);
            }

            ReviewSettings created = await CreateFromLegacyOrDefaultAsync(
                userId,
                set,
                cancellationToken);
            _context.ReviewSettings.Add(created);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return ReviewSettingsMapper.ToViewModel(created);
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                ReviewSettings? winner = await FindAsync(userId, set.Id, cancellationToken);
                if (winner == null)
                {
                    throw;
                }

                return ReviewSettingsMapper.ToViewModel(winner);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ReviewSettingsViewModel?> SaveAsync(
        string userId,
        int flashcardSetId,
        ReviewSettingsViewModel input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(input);

        FlashcardSet? set = await GetOwnedSetAsync(userId, flashcardSetId, cancellationToken);
        if (set == null)
        {
            return null;
        }

        string lockKey = $"{userId}\u001f{set.Id}";
        SemaphoreSlim gate = CreationLocks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            ReviewSettings? settings = await FindAsync(userId, set.Id, cancellationToken);
            if (settings == null)
            {
                settings = ReviewSettingsMapper.ToEntity(userId, set.Id, input);
                _context.ReviewSettings.Add(settings);
            }
            else
            {
                ReviewSettingsMapper.Apply(settings, input);
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                ReviewSettings? winner = await FindAsync(userId, set.Id, cancellationToken);
                if (winner == null)
                {
                    throw;
                }

                ReviewSettingsMapper.Apply(winner, input);
                await _context.SaveChangesAsync(cancellationToken);
                settings = winner;
            }

            return ReviewSettingsMapper.ToViewModel(settings);
        }
        finally
        {
            gate.Release();
        }
    }

    private Task<FlashcardSet?> GetOwnedSetAsync(
        string userId,
        int flashcardSetId,
        CancellationToken cancellationToken) =>
        _context.FlashcardSets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == flashcardSetId && value.UserId == userId,
                cancellationToken);

    private Task<ReviewSettings?> FindAsync(
        string userId,
        int flashcardSetId,
        CancellationToken cancellationToken) =>
        _context.ReviewSettings
            .SingleOrDefaultAsync(
                value => value.UserId == userId && value.FlashcardSetId == flashcardSetId,
            cancellationToken);

    private async Task<ReviewSettings> CreateFromLegacyOrDefaultAsync(
        string userId,
        FlashcardSet set,
        CancellationToken cancellationToken)
    {
        UserStudySettings? legacy = await _context.UserStudySettings
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.UserId == userId, cancellationToken);

        return ReviewSettings.CreateFromLegacy(
            userId,
            set.Id,
            set.NewCardQuota,
            legacy);
    }

    private static void Validate(ReviewSettingsViewModel input)
    {
        ReviewSettingsPolicy.ValidateSessionSize(input.ReviewSessionSize);
        ReviewSettingsPolicy.ValidateNewCardQuota(input.NewCardQuota);
        ReviewSettingsPolicy.ValidateMaxIntervalDays(input.ReviewMaxIntervalDays);
    }
}
