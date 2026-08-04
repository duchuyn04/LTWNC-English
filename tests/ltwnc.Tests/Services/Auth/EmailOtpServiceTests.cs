using ltwnc.Data;
using ltwnc.Models.Enums;
using ltwnc.Services.Auth;
using ltwnc.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Auth;

public sealed class EmailOtpServiceTests
{
    [Fact]
    public async Task ResendHonorsCooldownAndHourlyLimit()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using AppDbContext context = new(options);
        MutableTimeProvider clock = new();
        EmailOtpService service = new(
            context,
            new NoOpEmailSender(),
            new PasswordHasher<AppUser>(),
            new FixedCodeGenerator(),
            clock);

        OtpSendResult first = await service.SendAsync(
            EmailOtpPurpose.PasswordReset,
            "learner@example.com",
            requestIpAddress: "127.0.0.1");
        OtpSendResult tooSoon = await service.ResendAsync(first.ChallengeId!, "127.0.0.1");

        Assert.True(first.Succeeded);
        Assert.False(tooSoon.Succeeded);

        string challengeId = first.ChallengeId!;
        for (int send = 0; send < 4; send++)
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            OtpSendResult next = await service.ResendAsync(challengeId, "127.0.0.1");
            Assert.True(next.Succeeded);
            challengeId = next.ChallengeId!;
        }

        clock.Advance(TimeSpan.FromMinutes(1));
        OtpSendResult overLimit = await service.ResendAsync(challengeId, "127.0.0.1");
        Assert.False(overLimit.Succeeded);
    }

    private sealed class NoOpEmailSender : IEmailMessageSender
    {
        public Task SendAsync(
            string recipient,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FixedCodeGenerator : IOtpCodeGenerator
    {
        public string Generate() => "123456";
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; private set; } =
            new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;

        public void Advance(TimeSpan duration) => Now = Now.Add(duration);
    }
}
