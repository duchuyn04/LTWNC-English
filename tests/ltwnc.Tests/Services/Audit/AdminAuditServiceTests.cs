using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using ltwnc.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Audit;

public sealed class AdminAuditServiceTests
{
    [Fact]
    public async Task Record_PersistsEntryWithTimestampFromTimeProvider()
    {
        await using AppDbContext context = CreateContext();
        var clock = new AdjustableTimeProvider();
        var service = new AdminAuditService(context, clock);

        AdminAuditLog log = await service.RecordAsync(new AdminAuditEntry(
            ActorUserId: "admin-1",
            ActorDisplay: "admin@example.com",
            Action: AdminAuditActions.AdminAreaSignIn,
            Outcome: AdminAuditOutcome.Success,
            TargetType: "FlashcardSet",
            TargetId: "42",
            Reason: "Hỗ trợ vụ #7",
            CorrelationId: "corr-1",
            Metadata: new Dictionary<string, string?> { ["method"] = "authenticator" }));

        AdminAuditLog persisted = await context.AdminAuditLogs.SingleAsync();
        Assert.Equal(log.Id, persisted.Id);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, persisted.OccurredAtUtc);
        Assert.Equal("admin-1", persisted.ActorUserId);
        Assert.Equal("admin@example.com", persisted.ActorDisplay);
        Assert.Equal(AdminAuditActions.AdminAreaSignIn, persisted.Action);
        Assert.Equal(AdminAuditOutcome.Success, persisted.Outcome);
        Assert.Equal("FlashcardSet", persisted.TargetType);
        Assert.Equal("42", persisted.TargetId);
        Assert.Equal("Hỗ trợ vụ #7", persisted.Reason);
        Assert.Equal("corr-1", persisted.CorrelationId);
        Assert.Contains("authenticator", persisted.MetadataJson);
    }

    [Fact]
    public async Task Record_RequiresActorActionAndOutcome()
    {
        await using AppDbContext context = CreateContext();
        var service = new AdminAuditService(context, new AdjustableTimeProvider());

        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordAsync(new AdminAuditEntry(
            ActorUserId: "",
            ActorDisplay: "admin@example.com",
            Action: AdminAuditActions.AdminAreaSignIn,
            Outcome: AdminAuditOutcome.Success)));

        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordAsync(new AdminAuditEntry(
            ActorUserId: "admin-1",
            ActorDisplay: "admin@example.com",
            Action: " ",
            Outcome: AdminAuditOutcome.Success)));

        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordAsync(new AdminAuditEntry(
            ActorUserId: "admin-1",
            ActorDisplay: "admin@example.com",
            Action: AdminAuditActions.AdminAreaSignIn,
            Outcome: "")));
    }

    [Fact]
    public async Task Record_DropsMetadataKeysOutsideAllowlist()
    {
        await using AppDbContext context = CreateContext();
        var service = new AdminAuditService(context, new AdjustableTimeProvider());

        AdminAuditLog log = await service.RecordAsync(new AdminAuditEntry(
            ActorUserId: "admin-1",
            ActorDisplay: "admin@example.com",
            Action: AdminAuditActions.AdminAreaSignIn,
            Outcome: AdminAuditOutcome.Success,
            Metadata: new Dictionary<string, string?>
            {
                ["method"] = "authenticator",
                ["internalNote"] = "không được lưu"
            }));

        Assert.Contains("method", log.MetadataJson);
        Assert.DoesNotContain("internalNote", log.MetadataJson);
        Assert.DoesNotContain("không được lưu", log.MetadataJson);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("apiKey")]
    [InlineData("secret")]
    [InlineData("accessToken")]
    [InlineData("systemPrompt")]
    [InlineData("conversation")]
    public async Task Record_NeverPersistsSensitiveMetadataEvenWhenKeyLooksAllowed(string sensitiveKey)
    {
        await using AppDbContext context = CreateContext();
        var service = new AdminAuditService(context, new AdjustableTimeProvider());

        AdminAuditLog log = await service.RecordAsync(new AdminAuditEntry(
            ActorUserId: "admin-1",
            ActorDisplay: "admin@example.com",
            Action: AdminAuditActions.AdminAreaSignIn,
            Outcome: AdminAuditOutcome.Success,
            Metadata: new Dictionary<string, string?>
            {
                ["method"] = "authenticator",
                [sensitiveKey] = "giá-trị-bí-mật"
            }));

        Assert.DoesNotContain(sensitiveKey, log.MetadataJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("giá-trị-bí-mật", log.MetadataJson);
    }

    [Fact]
    public async Task Record_TruncatesOversizedMetadata()
    {
        await using AppDbContext context = CreateContext();
        var service = new AdminAuditService(context, new AdjustableTimeProvider());

        AdminAuditLog log = await service.RecordAsync(new AdminAuditEntry(
            ActorUserId: "admin-1",
            ActorDisplay: "admin@example.com",
            Action: AdminAuditActions.AdminAreaSignIn,
            Outcome: AdminAuditOutcome.Success,
            Metadata: new Dictionary<string, string?>
            {
                ["userAgent"] = new string('a', 5000)
            }));

        Assert.NotNull(log.MetadataJson);
        Assert.True(log.MetadataJson!.Length <= 2000);
    }

    [Fact]
    public async Task Enqueue_AddsEntryWithoutSavingSoCallerControlsTransaction()
    {
        await using AppDbContext context = CreateContext();
        var service = new AdminAuditService(context, new AdjustableTimeProvider());

        service.Enqueue(new AdminAuditEntry(
            ActorUserId: "admin-1",
            ActorDisplay: "admin@example.com",
            Action: AdminAuditActions.AdminAreaSignIn,
            Outcome: AdminAuditOutcome.Success));

        Assert.Equal(0, await context.AdminAuditLogs.CountAsync());

        await context.SaveChangesAsync();

        Assert.Equal(1, await context.AdminAuditLogs.CountAsync());
    }

    [Fact]
    public async Task Search_FiltersByActionOutcomeAndFreeText()
    {
        await using AppDbContext context = CreateContext();
        var service = new AdminAuditService(context, new AdjustableTimeProvider());
        await SeedAsync(service,
            ("AdminArea.SignIn", "Success", "alice@example.com"),
            ("AdminArea.SignIn", "Failure", "bob@example.com"),
            ("Users.Lock", "Success", "carol@example.com"));

        AdminAuditLogPage byAction = await service.SearchAsync(
            new AdminAuditQuery(Action: "Users.Lock"));
        Assert.Single(byAction.Items);
        Assert.Equal("carol@example.com", byAction.Items[0].ActorDisplay);

        AdminAuditLogPage byOutcome = await service.SearchAsync(
            new AdminAuditQuery(Outcome: "Failure"));
        Assert.Single(byOutcome.Items);
        Assert.Equal("bob@example.com", byOutcome.Items[0].ActorDisplay);

        AdminAuditLogPage byText = await service.SearchAsync(
            new AdminAuditQuery(Search: "alice"));
        Assert.Single(byText.Items);
        Assert.Equal("AdminArea.SignIn", byText.Items[0].Action);

        AdminAuditLogPage noMatch = await service.SearchAsync(
            new AdminAuditQuery(Search: "không-tồn-tại"));
        Assert.Empty(noMatch.Items);
        Assert.Equal(0, noMatch.TotalCount);
    }

    [Fact]
    public async Task Search_OrdersNewestFirstAndPaginates()
    {
        await using AppDbContext context = CreateContext();
        var clock = new AdjustableTimeProvider();
        var service = new AdminAuditService(context, clock);

        await service.RecordAsync(new AdminAuditEntry(
            "admin-1", "admin@example.com", "First", AdminAuditOutcome.Success));
        clock.Advance(TimeSpan.FromMinutes(1));
        await service.RecordAsync(new AdminAuditEntry(
            "admin-1", "admin@example.com", "Second", AdminAuditOutcome.Success));
        clock.Advance(TimeSpan.FromMinutes(1));
        await service.RecordAsync(new AdminAuditEntry(
            "admin-1", "admin@example.com", "Third", AdminAuditOutcome.Success));

        AdminAuditLogPage pageOne = await service.SearchAsync(
            new AdminAuditQuery(Page: 1, PageSize: 2));
        Assert.Equal(3, pageOne.TotalCount);
        Assert.Equal(2, pageOne.Items.Count);
        Assert.Equal("Third", pageOne.Items[0].Action);
        Assert.Equal("Second", pageOne.Items[1].Action);

        AdminAuditLogPage pageTwo = await service.SearchAsync(
            new AdminAuditQuery(Page: 2, PageSize: 2));
        Assert.Single(pageTwo.Items);
        Assert.Equal("First", pageTwo.Items[0].Action);
    }

    [Fact]
    public async Task Search_ClampsPageSizeToOneHundredAndDefaultsToTwentyFive()
    {
        await using AppDbContext context = CreateContext();
        var service = new AdminAuditService(context, new AdjustableTimeProvider());

        AdminAuditLogPage oversized = await service.SearchAsync(
            new AdminAuditQuery(PageSize: 500));
        Assert.Equal(100, oversized.PageSize);

        AdminAuditLogPage defaulted = await service.SearchAsync(new AdminAuditQuery());
        Assert.Equal(25, defaulted.PageSize);
    }

    private static async Task SeedAsync(
        AdminAuditService service,
        params (string Action, string Outcome, string Actor)[] entries)
    {
        foreach ((string action, string outcome, string actor) in entries)
        {
            await service.RecordAsync(new AdminAuditEntry(
                ActorUserId: actor,
                ActorDisplay: actor,
                Action: action,
                Outcome: outcome));
        }
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
