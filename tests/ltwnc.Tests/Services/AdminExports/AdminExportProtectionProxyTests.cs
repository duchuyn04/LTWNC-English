using System.Security.Claims;
using ltwnc.Areas.Admin;
using ltwnc.Services.AdminExports;
using ltwnc.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ltwnc.Tests.Services.AdminExports;

public sealed class AdminExportProtectionProxyTests
{
    // Caller không đạt policy Admin bị từ chối trước khi Real Subject tạo CSV.
    [Fact]
    public async Task ExportKpisAsync_NonAdmin_DoesNotReachRealSubject()
    {
        var subject = new RecordingAdminExportService();
        IAdminExportService service = CreateProxy(subject, CreatePrincipal("admin-1", "Admin One"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ExportKpisAsync(7, new AdminExportActor("admin-1", "Admin One")));

        Assert.False(subject.WasCalled);
    }

    // Caller đạt policy nhưng khai actor khác danh tính hiện tại vẫn bị từ chối.
    [Fact]
    public async Task ExportAuditLogsAsync_MismatchedActor_DoesNotReachRealSubject()
    {
        var subject = new RecordingAdminExportService();
        IAdminExportService service = CreateProxy(
            subject,
            CreatePrincipal("admin-1", "Admin One", isAdmin: true));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ExportAuditLogsAsync(
                new AdminAuditExportQuery(),
                new AdminExportActor("admin-2", "Admin Two")));

        Assert.False(subject.WasCalled);
    }

    // Admin hợp lệ nhận kết quả từ Real Subject và audit actor luôn lấy tên đáng tin cậy từ claim.
    [Fact]
    public async Task ExportKpisAsync_Admin_DelegatesWithActorFromClaims()
    {
        var subject = new RecordingAdminExportService();
        IAdminExportService service = CreateProxy(
            subject,
            CreatePrincipal("admin-1", "Admin One", isAdmin: true));

        AdminCsvExport result = await service.ExportKpisAsync(
            7,
            new AdminExportActor("admin-1", "Forged Name"));

        Assert.Same(subject.Result, result);
        Assert.Equal(new AdminExportActor("admin-1", "Admin One"), subject.ReceivedActor);
    }

    private static IAdminExportService CreateProxy(
        IAdminExportService subject,
        ClaimsPrincipal principal)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AdminAreaPolicy.Name, policy =>
                policy.RequireClaim(AppClaimTypes.IsAdmin, "true"));
        });
        ServiceProvider provider = services.BuildServiceProvider();
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return new AdminExportProtectionProxy(
            subject,
            provider.GetRequiredService<IAuthorizationService>(),
            httpContextAccessor);
    }

    private static ClaimsPrincipal CreatePrincipal(
        string userId,
        string displayName,
        bool isAdmin = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, displayName)
        };
        if (isAdmin)
        {
            claims.Add(new Claim(AppClaimTypes.IsAdmin, "true"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private sealed class RecordingAdminExportService : IAdminExportService
    {
        public bool WasCalled { get; private set; }
        public AdminExportActor? ReceivedActor { get; private set; }
        public AdminCsvExport Result { get; } = new("export.csv", [], 0);

        public Task<AdminCsvExport> ExportKpisAsync(
            int? days,
            AdminExportActor actor,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            ReceivedActor = actor;
            return Task.FromResult(Result);
        }

        public Task<AdminCsvExport> ExportAuditLogsAsync(
            AdminAuditExportQuery query,
            AdminExportActor actor,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            ReceivedActor = actor;
            return Task.FromResult(Result);
        }
    }
}
