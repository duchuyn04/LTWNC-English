using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ltwnc.Tests.Services.Ai;

public sealed class AiCompletionRouterHardeningTests
{
    [Fact]
    public async Task CompleteAsync_UsesEnabledPrimaryPriorityAndDeclarationOrder()
    {
        List<string> callOrder = [];
        StubAdapter declaredFirst = FailingAdapter("declared-first", callOrder);
        StubAdapter declaredSecond = FailingAdapter("declared-second", callOrder);
        StubAdapter primary = FailingAdapter("primary", callOrder);
        StubAdapter disabled = FailingAdapter("disabled", callOrder);
        AiProviderOptions[] providers =
        [
            Provider(declaredFirst, "Declared first", priority: 1),
            Provider(declaredSecond, "Declared second", priority: 1),
            Provider(primary, "Primary", isPrimary: true, priority: 99),
            Provider(disabled, "Disabled", isPrimary: true, priority: 0, isEnabled: false)
        ];
        await using AppDbContext context = CreateContext();
        AiCompletionRouter router = CreateRouter(
            context,
            [declaredFirst, declaredSecond, primary, disabled],
            providers);

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() => router.CompleteAsync(Request()));

        Assert.Equal(new[] { "primary", "declared-first", "declared-second" }, callOrder);
        Assert.Equal(0, disabled.CallCount);
        List<AiOperationLog> logs = await ReadLogsAsync(context);
        Assert.Equal(new[] { "Primary", "Declared first", "Declared second" },
            logs.Select(log => log.ProviderName).ToArray());
        Assert.Equal(new[] { "primary-model", "declared-first-model", "declared-second-model" },
            logs.Select(log => log.ModelId).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, logs.Select(log => log.FallbackAttempt).ToArray());
        Assert.All(logs, log =>
        {
            Assert.False(log.Succeeded);
            Assert.Equal(nameof(AiProviderUnavailableException), log.FailureKind);
            Assert.True(log.LatencyMs >= 0);
        });
    }

    [Fact]
    public async Task CompleteAsync_LogsUnsupportedAdapterAndFallsThrough()
    {
        StubAdapter supported = SuccessAdapter("supported", "backup result");
        AiProviderOptions[] providers =
        [
            new()
            {
                Name = "Missing",
                AdapterType = "not-registered",
                ModelId = "missing-model",
                BaseUrl = "https://missing.example/v1"
            },
            Provider(supported, "Supported", priority: 2)
        ];
        await using AppDbContext context = CreateContext();
        AiCompletionRouter router = CreateRouter(context, [supported], providers);

        AiCompletionResult result = await router.CompleteAsync(Request());

        Assert.Equal("backup result", result.Content);
        Assert.Equal(1, supported.CallCount);
        List<AiOperationLog> logs = await ReadLogsAsync(context);
        Assert.Equal(2, logs.Count);
        Assert.Equal("Missing", logs[0].ProviderName);
        Assert.Equal("missing-model", logs[0].ModelId);
        Assert.Equal("UnsupportedAdapter", logs[0].FailureKind);
        Assert.Equal(0, logs[0].FallbackAttempt);
        Assert.Equal("Supported", logs[1].ProviderName);
        Assert.True(logs[1].Succeeded);
        Assert.Null(logs[1].FailureKind);
        Assert.Equal(1, logs[1].FallbackAttempt);
    }

    [Fact]
    public async Task CompleteAsync_FallsThroughConfigurationUnavailableAndInvalidResponseFailures()
    {
        StubAdapter configurationFailure = new(
            "configuration",
            (_, _) => Task.FromResult("unused"),
            _ => throw new AiProviderConfigurationException("bad configuration"));
        StubAdapter unavailable = FailingAdapter("unavailable");
        StubAdapter invalid = SuccessAdapter("invalid", "invalid response");
        StubAdapter valid = SuccessAdapter("valid", "valid response");
        await using AppDbContext context = CreateContext();
        AiCompletionRouter router = CreateRouter(
            context,
            [configurationFailure, unavailable, invalid, valid],
            [
                Provider(configurationFailure, "Configuration", priority: 1),
                Provider(unavailable, "Unavailable", priority: 2),
                Provider(invalid, "Invalid", priority: 3),
                Provider(valid, "Valid", priority: 4)
            ]);

        AiCompletionResult result = await router.CompleteAsync(
            Request(),
            responseValidator: response => response == "valid response");

        Assert.Equal("valid response", result.Content);
        Assert.Equal(0, configurationFailure.CallCount);
        Assert.Equal(1, unavailable.CallCount);
        Assert.Equal(1, invalid.CallCount);
        Assert.Equal(1, valid.CallCount);
        List<AiOperationLog> logs = await ReadLogsAsync(context);
        Assert.Equal(
            new[]
            {
                nameof(AiProviderConfigurationException),
                nameof(AiProviderUnavailableException),
                "InvalidResponse",
                null
            },
            logs.Select(log => log.FailureKind).ToArray());
        Assert.Equal(new[] { 0, 1, 2, 3 }, logs.Select(log => log.FallbackAttempt).ToArray());
    }

    [Fact]
    public async Task CompleteAsync_StopsAfterFirstSuccessfulProvider()
    {
        StubAdapter primary = SuccessAdapter("primary", "first result");
        StubAdapter backup = FailingAdapter("backup");
        await using AppDbContext context = CreateContext();
        AiCompletionRouter router = CreateRouter(
            context,
            [primary, backup],
            [
                Provider(primary, "Primary", isPrimary: true),
                Provider(backup, "Backup", priority: 2)
            ]);

        AiCompletionResult result = await router.CompleteAsync(Request());

        Assert.Equal("first result", result.Content);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(0, backup.CallCount);
        List<AiOperationLog> logs = await ReadLogsAsync(context);
        Assert.Single(logs);
        Assert.True(logs[0].Succeeded);
        Assert.Equal("Primary", logs[0].ProviderName);
        Assert.Null(logs[0].FailureKind);
    }

    [Fact]
    public async Task CompleteAsync_AllFailuresReturnSafeUnavailableMessage()
    {
        const string secret = "provider-secret-details";
        StubAdapter failing = new(
            "failing",
            (_, _) => Task.FromException<string>(
                new AiProviderUnavailableException(secret)));
        await using AppDbContext context = CreateContext();
        AiCompletionRouter router = CreateRouter(
            context,
            [failing],
            [Provider(failing, "Failing")]);

        AiProviderUnavailableException exception = await Assert.ThrowsAsync<AiProviderUnavailableException>(
            () => router.CompleteAsync(Request()));

        Assert.Equal("Dịch vụ AI tạm thời không sẵn sàng. Vui lòng thử lại sau.", exception.Message);
        Assert.DoesNotContain(secret, exception.Message);
    }

    [Fact]
    public async Task CompleteAsync_TotalTimeoutStopsChainAndLogsDistinctFailure()
    {
        TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StubAdapter blocking = new(
            "blocking",
            async (_, cancellationToken) =>
            {
                started.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return "never";
            });
        StubAdapter backup = SuccessAdapter("backup", "backup result");
        await using AppDbContext context = CreateContext();
        AiCompletionRouter router = CreateRouter(
            context,
            [blocking, backup],
            [
                Provider(blocking, "Blocking", priority: 1),
                Provider(backup, "Backup", priority: 2)
            ],
            overallTimeoutSeconds: 1);

        Task<AiProviderUnavailableException> completion = Assert.ThrowsAsync<AiProviderUnavailableException>(
            () => router.CompleteAsync(Request()));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        AiProviderUnavailableException exception = await completion;

        Assert.Equal("Dịch vụ AI tạm thời không sẵn sàng. Vui lòng thử lại sau.", exception.Message);
        Assert.Equal(1, blocking.CallCount);
        Assert.Equal(0, backup.CallCount);
        List<AiOperationLog> logs = await ReadLogsAsync(context);
        Assert.Single(logs);
        Assert.Equal("TotalTimeout", logs[0].FailureKind);
        Assert.False(logs[0].Succeeded);
    }

    [Fact]
    public async Task CompleteAsync_CallerCancellationPropagatesAndSkipsFallback()
    {
        using CancellationTokenSource cancellation = new();
        TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StubAdapter blocking = new(
            "blocking",
            async (_, cancellationToken) =>
            {
                started.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return "never";
            });
        StubAdapter backup = SuccessAdapter("backup", "backup result");
        await using AppDbContext context = CreateContext();
        AiCompletionRouter router = CreateRouter(
            context,
            [blocking, backup],
            [
                Provider(blocking, "Blocking", priority: 1),
                Provider(backup, "Backup", priority: 2)
            ]);

        Task<AiCompletionResult> completion = router.CompleteAsync(Request(), cancellationToken: cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => completion);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, blocking.CallCount);
        Assert.Equal(0, backup.CallCount);
        Assert.Empty(await context.AiOperationLogs.ToListAsync());
    }

    private static StubAdapter FailingAdapter(string name, List<string>? callOrder = null)
        => new(
            name,
            (_, _) =>
            {
                callOrder?.Add(name);
                return Task.FromException<string>(new AiProviderUnavailableException("unavailable"));
            });

    private static StubAdapter SuccessAdapter(string name, string result)
        => new(name, (_, _) => Task.FromResult(result));

    private static AiProviderOptions Provider(
        StubAdapter adapter,
        string name,
        bool isPrimary = false,
        int priority = 1,
        bool isEnabled = true)
        => new()
        {
            Name = name,
            AdapterType = adapter.AdapterType,
            BaseUrl = $"https://{name.ToLowerInvariant().Replace(' ', '-')}.example/v1",
            ModelId = $"{name.ToLowerInvariant().Replace(' ', '-')}-model",
            IsPrimary = isPrimary,
            Priority = priority,
            IsEnabled = isEnabled
        };

    private static AiCompletionRouter CreateRouter(
        AppDbContext context,
        IEnumerable<IAiProviderAdapter> adapters,
        IEnumerable<AiProviderOptions> providers,
        int overallTimeoutSeconds = 90)
        => new(
            context,
            adapters,
            TimeProvider.System,
            Options.Create(new AiProvidersOptions
            {
                Routing = new AiRoutingOptions
                {
                    OverallTimeoutSeconds = overallTimeoutSeconds
                },
                Providers = providers.ToList()
            }));

    private static AiCompletionRequest Request()
        => new("system prompt", "user prompt");

    private static async Task<List<AiOperationLog>> ReadLogsAsync(AppDbContext context)
        => await context.AiOperationLogs
            .OrderBy(log => log.FallbackAttempt)
            .ToListAsync();

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private sealed class StubAdapter(
        string name,
        Func<AiProviderConnection, CancellationToken, Task<string>> completion,
        Action<AiProviderConnection>? validate = null) : IAiProviderAdapter
    {
        private readonly Func<AiProviderConnection, CancellationToken, Task<string>> _completion = completion;
        private readonly Action<AiProviderConnection>? _validate = validate;

        public string Name { get; } = name;

        public string AdapterType { get; } = $"Test-{name}-{Guid.NewGuid():N}";

        public int CallCount { get; private set; }

        public void ValidateConfiguration(AiProviderConnection connection)
        {
            _validate?.Invoke(connection);
        }

        public Task<string> CompleteAsync(
            AiProviderConnection connection,
            string? apiKey,
            AiCompletionRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return _completion(connection, cancellationToken);
        }
    }
}
