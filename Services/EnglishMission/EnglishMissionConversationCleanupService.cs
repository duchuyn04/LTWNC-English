using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;
using EnglishMissionEntity = ltwnc.Models.Entities.EnglishMission;

namespace ltwnc.Services.EnglishMission;

public sealed class EnglishMissionConversationCleanupService : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(90);
    private static readonly TimeSpan MaximumHold = TimeSpan.FromDays(365);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EnglishMissionConversationCleanupService> _logger;

    public EnglishMissionConversationCleanupService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<EnglishMissionConversationCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
            using var timer = new PeriodicTimer(RunInterval);
            do
            {
                await CleanupAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            DateTime defaultCutoffUtc = nowUtc - Retention;
            DateTime maximumHoldCutoffUtc = nowUtc - MaximumHold;

            List<EnglishMissionEntity> missions = await context.EnglishMissions
                .Include(mission => mission.Turns)
                .Where(mission => mission.ConversationContentDeletedAtUtc == null
                    && mission.CreatedAt <= defaultCutoffUtc
                    && (mission.ConversationRetentionHoldUntilUtc == null
                        || mission.ConversationRetentionHoldUntilUtc <= nowUtc
                        || mission.CreatedAt <= maximumHoldCutoffUtc))
                .OrderBy(mission => mission.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            foreach (EnglishMissionEntity mission in missions)
            {
                foreach (EnglishMissionTurn turn in mission.Turns)
                {
                    turn.UserText = string.Empty;
                    turn.NpcText = string.Empty;
                    turn.FeedbackVi = null;
                    turn.CorrectionEn = null;
                    turn.CorrectionExplanationVi = null;
                    turn.ProviderName = null;
                    turn.ModelId = null;
                }

                mission.Situation = string.Empty;
                mission.OpeningLine = string.Empty;
                mission.ConversationContentDeletedAtUtc = nowUtc;
            }

            if (missions.Count > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Cleared expired conversation content for {MissionCount} English missions.",
                    missions.Count);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "English mission conversation cleanup failed.");
        }
    }
}
