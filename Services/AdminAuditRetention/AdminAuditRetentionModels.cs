namespace ltwnc.Services.AdminAuditRetention;

public sealed record AdminAuditRetentionCleanupResult(
    int DeletedCount,
    DateTime CutoffUtc,
    int BatchSize);
