using System.Net;
using ltwnc.Data;
using ltwnc.Services.AdminUsers;
using ltwnc.Services.Audit;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ltwnc.Tests.Integration;

public sealed class AdminUserAccountTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    // Reset đồng hồ dùng chung để mỗi test đọc trạng thái lockout ổn định.
    public AdminUserAccountTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Clock.Reset();
    }

    // Danh sách người dùng hỗ trợ tìm kiếm và lọc phía máy chủ.
    [Fact]
    public async Task Users_SearchAndFilter_ReturnsMatchingAccounts()
    {
        const string adminEmail = "admin-users-list@example.com";
        const string learnerEmail = "learner-users-list@example.com";
        await _factory.SeedUserAsync(
            "admin_users_list",
            adminEmail,
            isAdmin: true,
            twoFactorEnabled: true);
        await _factory.SeedUserAsync("learner_users_list", learnerEmail);
        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync(
            "/Admin/Users?search=learner-users-list&status=unlocked&pageSize=25");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Danh sách tài khoản", html);
        Assert.Contains(learnerEmail, html);
        Assert.DoesNotContain(adminEmail, html);
    }

    // Trang chi tiết chỉ hiện trạng thái cần thiết, không có form sửa mật khẩu/hồ sơ/role/xóa tài khoản.
    [Fact]
    public async Task Details_DoesNotExposeOutOfScopeAccountActions()
    {
        const string adminEmail = "admin-users-details@example.com";
        const string learnerEmail = "learner-users-details@example.com";
        await _factory.SeedUserAsync(
            "admin_users_details",
            adminEmail,
            isAdmin: true,
            twoFactorEnabled: true);
        await _factory.SeedUserAsync("learner_users_details", learnerEmail);
        string learnerId = await _factory.GetUserIdAsync(learnerEmail);
        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await client.GetAsync($"/Admin/Users/{learnerId}");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Thao tác bảo vệ tài khoản", html);
        Assert.Contains("Khóa tài khoản", html);
        Assert.Contains("Thu hồi phiên", html);
        Assert.DoesNotContain("Đặt mật khẩu", html);
        Assert.DoesNotContain("Đổi vai trò", html);
        Assert.DoesNotContain("Xóa tài khoản", html);
    }

    // Khóa tài khoản dùng POST, ghi audit, vô hiệu cookie cũ và hiển thị thông báo chung khi đăng nhập lại.
    [Fact]
    public async Task Lock_UserRevokesExistingCookieAndShowsGenericLoginMessage()
    {
        const string adminEmail = "admin-users-lock@example.com";
        const string learnerEmail = "learner-users-lock@example.com";
        await _factory.SeedUserAsync(
            "admin_users_lock",
            adminEmail,
            isAdmin: true,
            twoFactorEnabled: true);
        await _factory.SeedUserAsync("learner_users_lock", learnerEmail);
        string learnerId = await _factory.GetUserIdAsync(learnerEmail);
        string stamp = await _factory.GetSecurityStampAsync(learnerEmail);

        using HttpClient learnerClient = CreateClient();
        await AdminWebApplicationFactory.SignInAsync(learnerClient, learnerEmail);

        using HttpClient adminClient = CreateClient();
        await _factory.SignInVerifiedAdminAsync(adminClient, adminEmail);
        HttpResponseMessage lockResponse = await AdminWebApplicationFactory.SubmitFormAsync(
            adminClient,
            $"/Admin/Users/{learnerId}",
            $"/Admin/Users/{learnerId}/Lock",
            new Dictionary<string, string>
            {
                ["Reason"] = "Tài khoản có dấu hiệu bị chiếm quyền.",
                ["ConcurrencyStamp"] = stamp
            });

        HttpResponseMessage staleCookieResponse = await learnerClient.GetAsync("/Account/Profile/Edit");
        using HttpClient lockedLoginClient = CreateClient();
        HttpResponseMessage loginResponse =
            await AdminWebApplicationFactory.SubmitLoginAsync(lockedLoginClient, learnerEmail);
        string loginHtml = WebUtility.HtmlDecode(
            await loginResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Redirect, lockResponse.StatusCode);
        Assert.True(await _factory.IsLockedOutAsync(learnerEmail));
        Assert.Equal(HttpStatusCode.Redirect, staleCookieResponse.StatusCode);
        Assert.Equal("/Account/Login", staleCookieResponse.Headers.Location?.AbsolutePath);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Contains("Tài khoản hiện không thể đăng nhập", loginHtml);
        await AssertAuditExistsAsync(AdminAuditActions.UsersLock, AdminAuditOutcome.Success, learnerId);
    }

    // Hệ thống từ chối tự khóa và vẫn ghi audit kết quả Denied.
    [Fact]
    public async Task Lock_Self_IsDeniedAndAudited()
    {
        const string adminEmail = "admin-users-self-lock@example.com";
        await _factory.SeedUserAsync(
            "admin_users_self_lock",
            adminEmail,
            isAdmin: true,
            twoFactorEnabled: true);
        string adminId = await _factory.GetUserIdAsync(adminEmail);
        string stamp = await _factory.GetSecurityStampAsync(adminEmail);
        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            $"/Admin/Users/{adminId}",
            $"/Admin/Users/{adminId}/Lock",
            new Dictionary<string, string>
            {
                ["Reason"] = "Thử tự khóa để kiểm tra bất biến.",
                ["ConcurrencyStamp"] = stamp
            });
        HttpResponseMessage detailsResponse = await client.GetAsync($"/Admin/Users/{adminId}");
        string html = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await _factory.IsLockedOutAsync(adminEmail));
        Assert.Contains("không thể tự khóa", html);
        await AssertAuditExistsAsync(AdminAuditActions.UsersLock, AdminAuditOutcome.Denied, adminId);
    }

    // Concurrency stamp cũ bị từ chối để tránh ghi đè thay đổi mới hơn.
    [Fact]
    public async Task RevokeSessions_StaleConcurrencyStamp_IsDenied()
    {
        const string adminEmail = "admin-users-conflict@example.com";
        const string learnerEmail = "learner-users-conflict@example.com";
        await _factory.SeedUserAsync(
            "admin_users_conflict",
            adminEmail,
            isAdmin: true,
            twoFactorEnabled: true);
        await _factory.SeedUserAsync("learner_users_conflict", learnerEmail);
        string learnerId = await _factory.GetUserIdAsync(learnerEmail);
        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, adminEmail);

        HttpResponseMessage response = await AdminWebApplicationFactory.SubmitFormAsync(
            client,
            $"/Admin/Users/{learnerId}",
            $"/Admin/Users/{learnerId}/RevokeSessions",
            new Dictionary<string, string>
            {
                ["Reason"] = "Kiểm tra xung đột đồng thời.",
                ["ConcurrencyStamp"] = "stamp-cu"
            });
        HttpResponseMessage detailsResponse = await client.GetAsync($"/Admin/Users/{learnerId}");
        string html = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Tài khoản đã thay đổi", html);
        await AssertAuditExistsAsync(
            AdminAuditActions.UsersRevokeSessions,
            AdminAuditOutcome.Denied,
            learnerId);
    }

    // Service từ chối khóa tài khoản Admin khởi tạo dù người thao tác là Admin khác.
    [Fact]
    public async Task Lock_BootstrapAdmin_IsDenied()
    {
        using var factory = new AdminWebApplicationFactory();
        const string bootstrapEmail = "admin-users-bootstrap@example.com";
        await factory.SeedUserAsync(
            "admin_users_bootstrap",
            bootstrapEmail,
            isAdmin: true,
            twoFactorEnabled: true);
        string bootstrapId = await factory.GetUserIdAsync(bootstrapEmail);
        string stamp = await factory.GetSecurityStampAsync(bootstrapEmail);
        (IServiceScope scope, IAdminUserAccountService service) =
            CreateServiceWithBootstrap(factory, bootstrapId);
        using (scope)
        {
            AdminUserOperationResult result = await service.LockAsync(new AdminUserAccountCommand(
                ActorUserId: "other-admin",
                ActorDisplay: "other-admin@example.com",
                TargetUserId: bootstrapId,
                Reason: "Kiểm tra bất biến bootstrap.",
                ConcurrencyStamp: stamp));

            Assert.False(result.Succeeded);
            Assert.Contains("khởi tạo", result.Message);
        }
        Assert.False(await factory.IsLockedOutAsync(bootstrapEmail));
    }

    // Service từ chối khóa khi thao tác sẽ làm hệ thống không còn Admin hoạt động.
    [Fact]
    public async Task Lock_LastActiveAdmin_IsDenied()
    {
        using var factory = new AdminWebApplicationFactory();
        const string adminEmail = "admin-users-last-active@example.com";
        await factory.SeedUserAsync(
            "admin_users_last_active",
            adminEmail,
            isAdmin: true,
            twoFactorEnabled: true);
        string adminId = await factory.GetUserIdAsync(adminEmail);
        string stamp = await factory.GetSecurityStampAsync(adminEmail);
        (IServiceScope scope, IAdminUserAccountService service) =
            CreateServiceWithBootstrap(factory, null);
        using (scope)
        {
            AdminUserOperationResult result = await service.LockAsync(new AdminUserAccountCommand(
                ActorUserId: "other-admin",
                ActorDisplay: "other-admin@example.com",
                TargetUserId: adminId,
                Reason: "Kiểm tra bất biến Admin cuối cùng.",
                ConcurrencyStamp: stamp));

            Assert.False(result.Succeeded);
            Assert.Contains("cuối cùng", result.Message);
        }
        Assert.False(await factory.IsLockedOutAsync(adminEmail));
    }

    // Kiểm tra audit từ database để xác nhận thao tác Admin có dấu vết.
    private async Task AssertAuditExistsAsync(string action, string outcome, string targetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        bool exists = await context.AdminAuditLogs.AnyAsync(log =>
            log.Action == action
            && log.Outcome == outcome
            && log.TargetId == targetId);
        Assert.True(exists);
    }

    // Tạo service thủ công để test cấu hình AdminBootstrap riêng cho từng fixture cô lập.
    private static (IServiceScope Scope, IAdminUserAccountService Service) CreateServiceWithBootstrap(
        AdminWebApplicationFactory factory,
        string? bootstrapUserId)
    {
        IServiceScope scope = factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        IAdminAuditService auditService =
            scope.ServiceProvider.GetRequiredService<IAdminAuditService>();
        TimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminBootstrap:UserId"] = bootstrapUserId
            })
            .Build();

        IAdminUserAccountService service = new AdminUserAccountService(
            context,
            userManager,
            auditService,
            configuration,
            timeProvider);
        return (scope, service);
    }

    // Tạo HttpClient không tự follow redirect để test đúng contract HTTP.
    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }
}
