namespace ltwnc.Services.ContentReports;

public interface IContentReportService
{
    IReadOnlyList<ContentReportReasonOption> GetReasonOptions();

    Task<bool> HasOpenReportAsync(
        int flashcardSetId,
        string reporterUserId,
        CancellationToken cancellationToken = default);

    Task<ContentReportSubmitResult> SubmitAsync(
        SubmitContentReportCommand command,
        CancellationToken cancellationToken = default);

    Task<AdminContentReportPage> SearchForAdminAsync(
        AdminContentReportQuery query,
        CancellationToken cancellationToken = default);

    Task<int> CountPendingOlderThanAsync(
        TimeSpan age,
        CancellationToken cancellationToken = default);

    Task<ContentReportOperationResult> DismissAsync(
        DismissContentReportCommand command,
        CancellationToken cancellationToken = default);
}
