using System.Security.Claims;
using ltwnc.Areas.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace ltwnc.Services.AdminExports;

// Bảo vệ service xuất dữ liệu bằng cùng policy với Admin Area trước khi gọi Real Subject.
public sealed class AdminExportProtectionProxy : IAdminExportService
{
    private readonly IAdminExportService _subject;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminExportProtectionProxy(
        IAdminExportService subject,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _subject = subject;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<AdminCsvExport> ExportKpisAsync(
        int? days,
        AdminExportActor actor,
        CancellationToken cancellationToken = default)
    {
        AdminExportActor authorizedActor = await AuthorizeAsync(actor);
        return await _subject.ExportKpisAsync(days, authorizedActor, cancellationToken);
    }

    public async Task<AdminCsvExport> ExportAuditLogsAsync(
        AdminAuditExportQuery query,
        AdminExportActor actor,
        CancellationToken cancellationToken = default)
    {
        AdminExportActor authorizedActor = await AuthorizeAsync(actor);
        return await _subject.ExportAuditLogsAsync(query, authorizedActor, cancellationToken);
    }

    private async Task<AdminExportActor> AuthorizeAsync(AdminExportActor actor)
    {
        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null
            || !(await _authorizationService.AuthorizeAsync(
                httpContext.User,
                AdminAreaPolicy.Name)).Succeeded)
        {
            throw new UnauthorizedAccessException("Không có quyền xuất dữ liệu quản trị.");
        }

        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !string.Equals(userId, actor.UserId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Danh tính xuất dữ liệu không hợp lệ.");
        }

        string displayName = httpContext.User.FindFirstValue(ClaimTypes.Name) ?? "Admin";
        return new AdminExportActor(userId, displayName);
    }
}
