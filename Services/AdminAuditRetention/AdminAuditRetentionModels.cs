namespace ltwnc.Services.AdminAuditRetention;

// Kết quả dọn dẹp audit gồm số bản ghi đã xóa, mốc thời gian và kích thước lô.
public sealed record AdminAuditRetentionCleanupResult(
    int DeletedCount,
    DateTime CutoffUtc,
    int BatchSize);
