namespace ltwnc.Services.AdminExports;

public sealed record AdminExportActor(
    string UserId,
    string DisplayName);

public sealed record AdminCsvExport(
    string FileName,
    byte[] Content,
    int RowCount);

public sealed record AdminAuditExportQuery(
    string? Search = null,
    string? Action = null,
    string? Outcome = null);
