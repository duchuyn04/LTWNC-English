using System.Diagnostics;
using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Ai;

public class AiCompletionRouter : IAiCompletionRouter
{
    private const string LearnerSafeUnavailableMessage =
        "Dịch vụ AI tạm thời không sẵn sàng. Vui lòng thử lại sau.";
    private const int DefaultOverallTimeoutSeconds = 90;

    private readonly AppDbContext _context;
    private readonly IDataProtector _protector;
    private readonly IReadOnlyDictionary<string, IAiProviderAdapter> _adapters;
    private readonly TimeProvider _timeProvider;
    private readonly int _overallTimeoutSeconds;

    // Router nhận snapshot provider, adapter và cấu hình timeout tổng thể từ DI.
    public AiCompletionRouter(
        AppDbContext context,
        IDataProtectionProvider dataProtection,
        IEnumerable<IAiProviderAdapter> adapters,
        TimeProvider timeProvider,
        IConfiguration configuration)
    {
        _context = context;
        _protector = dataProtection.CreateProtector("AiProvider.ApiKey.v1");
        _adapters = adapters.ToDictionary(adapter => adapter.AdapterType, StringComparer.OrdinalIgnoreCase);
        _timeProvider = timeProvider;
        _overallTimeoutSeconds = ReadOverallTimeoutSeconds(configuration);
    }

    // Chạy completion qua danh sách provider đủ điều kiện, có timeout tổng thể và fallback có giới hạn.
    public async Task<AiCompletionResult> CompleteAsync(
        AiCompletionRequest request,
        Func<string, bool>? responseValidator = null,
        CancellationToken cancellationToken = default)
    {
        List<AiProvider> providers = await LoadEligibleProvidersAsync(cancellationToken);
        if (providers.Count == 0)
        {
            throw new AiProviderUnavailableException(LearnerSafeUnavailableMessage);
        }

        using CancellationTokenSource overallTimeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overallTimeout.CancelAfter(TimeSpan.FromSeconds(_overallTimeoutSeconds));

        int fallbackAttempt = 0;
        foreach (AiProvider provider in providers)
        {
            if (!_adapters.TryGetValue(provider.AdapterType, out IAiProviderAdapter? adapter))
            {
                await RecordAttemptAsync(
                    provider,
                    succeeded: false,
                    failureKind: "UnsupportedAdapter",
                    latencyMs: 0,
                    fallbackAttempt: fallbackAttempt,
                    cancellationToken);
                fallbackAttempt++;
                continue;
            }

            AiCompletionResult? result = await TryCompleteWithProviderAsync(
                provider,
                adapter,
                request,
                responseValidator,
                fallbackAttempt,
                overallTimeout,
                cancellationToken);
            if (result != null)
            {
                return result;
            }

            if (overallTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                break;
            }

            fallbackAttempt++;
        }

        throw new AiProviderUnavailableException(LearnerSafeUnavailableMessage);
    }

    // Lấy snapshot provider đang bật, đã test thành công, theo provider chính rồi đến priority Admin cấu hình.
    private Task<List<AiProvider>> LoadEligibleProvidersAsync(CancellationToken cancellationToken)
    {
        return _context.AiProviders
            .AsNoTracking()
            .Where(provider => provider.IsEnabled && provider.LastCheckSucceeded == true)
            .OrderByDescending(provider => provider.IsPrimary)
            .ThenBy(provider => provider.Priority)
            .ThenBy(provider => provider.Id)
            .ToListAsync(cancellationToken);
    }

    // Thử một provider và trả null khi cần fallback sang provider tiếp theo.
    private async Task<AiCompletionResult?> TryCompleteWithProviderAsync(
        AiProvider provider,
        IAiProviderAdapter adapter,
        AiCompletionRequest request,
        Func<string, bool>? responseValidator,
        int fallbackAttempt,
        CancellationTokenSource overallTimeout,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            string? key = DecryptApiKey(provider);
            string content = await adapter.CompleteAsync(
                provider,
                key,
                request,
                overallTimeout.Token);
            if (responseValidator != null && !responseValidator(content))
            {
                await RecordAttemptAsync(
                    provider,
                    succeeded: false,
                    failureKind: "InvalidResponse",
                    latencyMs: ElapsedMilliseconds(stopwatch),
                    fallbackAttempt: fallbackAttempt,
                    cancellationToken);
                return null;
            }

            await RecordAttemptAsync(
                provider,
                succeeded: true,
                failureKind: null,
                latencyMs: ElapsedMilliseconds(stopwatch),
                fallbackAttempt: fallbackAttempt,
                cancellationToken);
            return new AiCompletionResult(content, provider.Id, provider.Name, provider.ModelId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
            && overallTimeout.IsCancellationRequested)
        {
            await RecordAttemptAsync(
                provider,
                succeeded: false,
                failureKind: "TotalTimeout",
                latencyMs: ElapsedMilliseconds(stopwatch),
                fallbackAttempt: fallbackAttempt,
                CancellationToken.None);
            return null;
        }
        catch (Exception exception) when (IsFallbackSafeFailure(exception))
        {
            await RecordAttemptAsync(
                provider,
                succeeded: false,
                failureKind: exception.GetType().Name,
                latencyMs: ElapsedMilliseconds(stopwatch),
                fallbackAttempt: fallbackAttempt,
                cancellationToken);
            return null;
        }
    }

    // Giải mã khóa nội bộ ngay trước khi gọi adapter; khóa không bao giờ đi vào log vận hành.
    private string? DecryptApiKey(AiProvider provider)
    {
        if (string.IsNullOrWhiteSpace(provider.EncryptedApiKey))
        {
            return null;
        }

        return _protector.Unprotect(provider.EncryptedApiKey);
    }

    // Chỉ fallback các lỗi AI dự kiến; lỗi hủy request từ client thì để middleware xử lý.
    private static bool IsFallbackSafeFailure(Exception exception)
    {
        return exception is AiProviderUnavailableException
            or AiProviderConfigurationException
            or JsonException
            or System.Security.Cryptography.CryptographicException;
    }

    // Ghi log từng lần thử provider; chỉ lưu metadata vận hành, không lưu prompt hay hội thoại.
    private async Task RecordAttemptAsync(
        AiProvider provider,
        bool succeeded,
        string? failureKind,
        int latencyMs,
        int fallbackAttempt,
        CancellationToken cancellationToken)
    {
        // Insert trực tiếp để không flush entity nghiệp vụ đang tracked trong request học.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO AiOperationLogs
                (OccurredAtUtc, ProviderId, ProviderName, ModelId, Operation, Succeeded, FailureKind, LatencyMs, FallbackAttempt)
            VALUES
                ({_timeProvider.GetUtcNow().UtcDateTime}, {provider.Id}, {provider.Name}, {provider.ModelId}, {"Completion"}, {succeeded}, {failureKind}, {latencyMs}, {fallbackAttempt})
            """,
            cancellationToken);
    }

    // Chốt thời gian chạy và ép về int an toàn cho cột LatencyMs.
    private static int ElapsedMilliseconds(Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds);
    }

    // Đọc timeout tổng thể từ cấu hình hệ thống; giá trị sai quay về mặc định an toàn.
    private static int ReadOverallTimeoutSeconds(IConfiguration configuration)
    {
        int configuredSeconds = DefaultOverallTimeoutSeconds;
        int? configuredValue = configuration.GetValue<int?>("AiProviders:Routing:OverallTimeoutSeconds");
        if (configuredValue.HasValue)
        {
            configuredSeconds = configuredValue.Value;
        }

        return Math.Clamp(configuredSeconds, 1, 300);
    }
}
