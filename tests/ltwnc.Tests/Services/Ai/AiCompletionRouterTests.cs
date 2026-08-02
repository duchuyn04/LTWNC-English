using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ltwnc.Tests.Services.Ai;

public sealed class AiCompletionRouterTests
{
    [Fact]
    public async Task CompleteAsync_FallsThroughFailedPrimaryAndLogsBothAttempts()
    {
        await using AppDbContext context = CreateContext();
        StubAdapter primary = new(
            "primary failed",
            new AiProviderUnavailableException("primary unavailable"));
        StubAdapter secondary = new("secondary result");
        AiCompletionRouter router = new(
            context,
            [primary, secondary],
            TimeProvider.System,
            Options.Create(new AiProvidersOptions
            {
                Providers =
                [
                    new AiProviderOptions
                    {
                        Name = "Primary",
                        AdapterType = primary.AdapterType,
                        BaseUrl = "https://primary.example/v1",
                        ModelId = "primary-model",
                        IsPrimary = true,
                        Priority = 1
                    },
                    new AiProviderOptions
                    {
                        Name = "Secondary",
                        AdapterType = secondary.AdapterType,
                        BaseUrl = "https://secondary.example/v1",
                        ModelId = "secondary-model",
                        Priority = 2
                    }
                ]
            }));

        AiCompletionResult result = await router.CompleteAsync(
            new AiCompletionRequest("system", "user"));

        Assert.Equal("secondary result", result.Content);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, secondary.CallCount);

        List<AiOperationLog> logs = await context.AiOperationLogs
            .OrderBy(log => log.FallbackAttempt)
            .ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.False(logs[0].Succeeded);
        Assert.Equal(0, logs[0].FallbackAttempt);
        Assert.True(logs[1].Succeeded);
        Assert.Equal(1, logs[1].FallbackAttempt);
        Assert.All(logs, log => Assert.Null(log.ProviderId));
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private sealed class StubAdapter : IAiProviderAdapter
    {
        private readonly string? _result;
        private readonly Exception? _exception;

        public StubAdapter(string result)
        {
            AdapterType = Guid.NewGuid().ToString();
            _result = result;
        }

        public StubAdapter(string adapterType, Exception exception)
        {
            AdapterType = adapterType;
            _exception = exception;
        }

        public string AdapterType { get; }

        public int CallCount { get; private set; }

        public void ValidateConfiguration(AiProviderConnection connection)
        {
        }

        public Task<string> CompleteAsync(
            AiProviderConnection connection,
            string? apiKey,
            AiCompletionRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (_exception != null)
            {
                throw _exception;
            }

            return Task.FromResult(_result!);
        }
    }
}
