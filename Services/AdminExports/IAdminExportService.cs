namespace ltwnc.Services.AdminExports;

public interface IAdminExportService
{
    // Xuất KPI dashboard theo bộ lọc ngày đang được chọn và ghi audit chỉ chứa metadata tổng hợp.
    Task<AdminCsvExport> ExportKpisAsync(
        int? days,
        AdminExportActor actor,
        CancellationToken cancellationToken = default);

    // Xuất bản ghi kiểm toán theo bộ lọc hiện tại, giới hạn thời gian và số dòng để tránh tệp quá lớn.
    Task<AdminCsvExport> ExportAuditLogsAsync(
        AdminAuditExportQuery query,
        AdminExportActor actor,
        CancellationToken cancellationToken = default);
}
