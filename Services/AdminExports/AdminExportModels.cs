namespace ltwnc.Services.AdminExports;

// Thông tin Admin thực hiện thao tác xuất dữ liệu để ghi audit.
public sealed record AdminExportActor(
    string UserId,
    string DisplayName);

// Tệp CSV đã tạo cùng tên tệp và số dòng dữ liệu.
public sealed record AdminCsvExport(
    string FileName,
    byte[] Content,
    int RowCount);

// Bộ lọc áp dụng khi xuất lịch sử audit.
public sealed record AdminAuditExportQuery(
    string? Search = null,
    string? Action = null,
    string? Outcome = null);
