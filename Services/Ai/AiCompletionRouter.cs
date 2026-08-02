using ltwnc.Data;
using Microsoft.Extensions.Options;

namespace ltwnc.Services.Ai;

public class AiCompletionRouter : IAiCompletionRouter
{
    private const string LearnerSafeUnavailableMessage =
        "Dịch vụ AI tạm thời không sẵn sàng. Vui lòng thử lại sau.";
    private const int DefaultOverallTimeoutSeconds = 90;

    private readonly AiProviderFallbackHandler? _chain;
    private readonly int _overallTimeoutSeconds;

    public AiCompletionRouter(
        AppDbContext context,
        IEnumerable<IAiProviderAdapter> adapters,
        TimeProvider timeProvider,
        IOptions<AiProvidersOptions> options)
    {
        _overallTimeoutSeconds = ReadOverallTimeoutSeconds(options.Value);
        IReadOnlyDictionary<string, IAiProviderAdapter> adaptersByType =
            adapters.ToDictionary(adapter => adapter.AdapterType, StringComparer.OrdinalIgnoreCase);

        AiProviderOptions[] providers = (options.Value.Providers ?? [])
            .Select((provider, index) => (Provider: provider, Index: index))
            .Where(item => item.Provider.IsEnabled)
            .OrderByDescending(item => item.Provider.IsPrimary)
            .ThenBy(item => item.Provider.Priority)
            .ThenBy(item => item.Index)
            .Select(item => item.Provider)
            .ToArray();

        AiProviderFallbackHandler? next = null;
        for (int index = providers.Length - 1; index >= 0; index--)
        {
            AiProviderOptions provider = providers[index];
            adaptersByType.TryGetValue(provider.AdapterType, out IAiProviderAdapter? adapter);
            next = new AiProviderFallbackHandler(
                provider,
                adapter,
                next,
                context,
                timeProvider);
        }

        _chain = next;
    }

    public async Task<AiCompletionResult> CompleteAsync(
        AiCompletionRequest request,
        Func<string, bool>? responseValidator = null,
        CancellationToken cancellationToken = default)
    {
        if (_chain == null)
        {
            throw new AiProviderUnavailableException(LearnerSafeUnavailableMessage);
        }

        using CancellationTokenSource overallTimeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overallTimeout.CancelAfter(TimeSpan.FromSeconds(_overallTimeoutSeconds));

        AiCompletionResult? result = await _chain.HandleAsync(
            request,
            responseValidator,
            fallbackAttempt: 0,
            overallTimeout: overallTimeout,
            cancellationToken: cancellationToken);

        return result
            ?? throw new AiProviderUnavailableException(LearnerSafeUnavailableMessage);
    }

    private static int ReadOverallTimeoutSeconds(AiProvidersOptions options)
    {
        int configuredSeconds = options.Routing?.OverallTimeoutSeconds ?? DefaultOverallTimeoutSeconds;
        return Math.Clamp(configuredSeconds, 1, 300);
    }
}
