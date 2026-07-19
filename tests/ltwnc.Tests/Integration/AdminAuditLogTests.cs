using System.Net;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ltwnc.Tests.Integration;

public sealed class AdminAuditLogTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public AdminAuditLogTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Clock.Reset();
    }

    [Fact]
    public async Task AuditLogs_GuestIsRedirectedToLogin()
    {
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await client.GetAsync("/Admin/AuditLogs");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task AuditLogs_LearnerReceivesForbiddenPage()
    {
        const string email = "learner-audit@example.com";
        await _factory.SeedUserAsync("learner_audit", email);
        using HttpClient client = CreateClient();
        await AdminWebApplicationFactory.SignInAsync(client, email);

        HttpResponseMessage response = await client.GetAsync("/Admin/AuditLogs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuditLogs_AdminSignInCreatesAuditRecordVisibleOnPage()
    {
        const string email = "admin-audit-signin@example.com";
        await _factory.SeedUserAsync(
            "admin_audit_signin",
            email,
            isAdmin: true,
            twoFactorEnabled: true);
        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, email);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            AdminAuditLog log = await context.AdminAuditLogs
                .SingleAsync(entry => entry.Action == AdminAuditActions.AdminAreaSignIn
                    && entry.ActorDisplay == email);
            Assert.Equal(AdminAuditOutcome.Success, log.Outcome);
            Assert.DoesNotContain("password", log.MetadataJson ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        HttpResponseMessage response = await client.GetAsync("/Admin/AuditLogs");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Bản ghi kiểm toán quản trị", html);
        Assert.Contains(email, html);
        Assert.Contains(AdminAuditActions.AdminAreaSignIn, html);
    }

    [Fact]
    public async Task AuditLogs_FilterByActionNarrowsResults()
    {
        const string email = "admin-audit-filter@example.com";
        await _factory.SeedUserAsync(
            "admin_audit_filter",
            email,
            isAdmin: true,
            twoFactorEnabled: true);
        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, email);
        await RecordAsync(new AdminAuditEntry(
            ActorUserId: "admin-seed",
            ActorDisplay: "seed@example.com",
            Action: "Users.Lock",
            Outcome: AdminAuditOutcome.Success,
            Reason: "Vi phạm điều khoản"));

        HttpResponseMessage response = await client.GetAsync(
            "/Admin/AuditLogs?action=Users.Lock");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Users.Lock", html);
        Assert.Contains("seed@example.com", html);
        Assert.DoesNotContain(
            $"<code>{AdminAuditActions.AdminAreaSignIn}</code>", html);
    }

    [Fact]
    public async Task AuditLogs_PaginatesServerSideWithDefaultPageSize()
    {
        const string email = "admin-audit-paging@example.com";
        await _factory.SeedUserAsync(
            "admin_audit_paging",
            email,
            isAdmin: true,
            twoFactorEnabled: true);
        using HttpClient client = CreateClient();
        await _factory.SignInVerifiedAdminAsync(client, email);

        for (int index = 0; index < 30; index++)
        {
            await RecordAsync(new AdminAuditEntry(
                ActorUserId: $"seed-{index}",
                ActorDisplay: $"seed{index:D2}@example.com",
                Action: "Users.Unlock",
                Outcome: AdminAuditOutcome.Success));
        }

        HttpResponseMessage pageOne = await client.GetAsync("/Admin/AuditLogs?action=Users.Unlock");
        string htmlOne = WebUtility.HtmlDecode(
            await pageOne.Content.ReadAsStringAsync());
        Assert.Contains("seed29@example.com", htmlOne);
        Assert.DoesNotContain("seed00@example.com", htmlOne);

        HttpResponseMessage pageTwo = await client.GetAsync(
            "/Admin/AuditLogs?action=Users.Unlock&page=2");
        string htmlTwo = WebUtility.HtmlDecode(
            await pageTwo.Content.ReadAsStringAsync());
        Assert.Contains("seed00@example.com", htmlTwo);
        Assert.DoesNotContain("seed29@example.com", htmlTwo);
    }

    private async Task RecordAsync(AdminAuditEntry entry)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IAdminAuditService auditService =
            scope.ServiceProvider.GetRequiredService<IAdminAuditService>();
        await auditService.RecordAsync(entry);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
}
