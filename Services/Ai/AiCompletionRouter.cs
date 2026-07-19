using System.Text.Json;
using System.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Services.Ai;

public class AiCompletionRouter : IAiCompletionRouter
{
    private readonly AppDbContext _context;
    private readonly IDataProtector _protector;
    private readonly IReadOnlyDictionary<string, IAiProviderAdapter> _adapters;
    private readonly TimeProvider _timeProvider;

    public AiCompletionRouter(
        AppDbContext context,
        IDataProtectionProvider dataProtection,
        IEnumerable<IAiProviderAdapter> adapters,
        TimeProvider timeProvider)
    {
        _context = context;
        _protector = dataProtection.CreateProtector("AiProvider.ApiKey.v1");
        _adapters = adapters.ToDictionary(adapter => adapter.AdapterType, StringComparer.OrdinalIgnoreCase);
        _timeProvider = timeProvider;
    }

    // Chạy completion qua provider chính trước, sau đó fallback theo priority nếu provider chính lỗi tạm thời.
    public async Task<AiCompletionResult> CompleteAsync(
        AiCompletionRequest request,
        Func<string, bool>? responseValidator = null,
        CancellationToken cancellationToken = default)
    {
        List<AiProvider> providers = await _context.AiProviders
            .AsNoTracking()
            .Where(provider => provider.IsEnabled)
            .OrderByDescending(provider => provider.IsPrimary)
            .ThenBy(provider => provider.Priority)
            .ThenBy(provider => provider.Id)
            .ToListAsync(cancellationToken);

        if (providers.Count == 0)
        {
            throw new AiProviderUnavailableException("Chưa có AI provider nào được bật.");
        }

        List<string> failures = new();
        foreach (AiProvider provider in providers)
        {
            if (!_adapters.TryGetValue(provider.AdapterType, out IAiProviderAdapter? adapter))
            {
                failures.Add($"{provider.Name}: adapter chưa được hỗ trợ");
                await RecordAttemptAsync(
                    provider,
                    succeeded: false,
                    failureKind: "UnsupportedAdapter",
                    latencyMs: 0,
                    cancellationToken);
                continue;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                string? key = null;
                if (!string.IsNullOrWhiteSpace(provider.EncryptedApiKey))
                {
                    key = _protector.Unprotect(provider.EncryptedApiKey);
                }

                string content = await adapter.CompleteAsync(provider, key, request, cancellationToken);
                if (responseValidator != null && !responseValidator(content))
                {
                    failures.Add($"{provider.Name}: output không đúng định dạng");
                    await RecordAttemptAsync(
                        provider,
                        succeeded: false,
                        failureKind: "InvalidResponse",
                        latencyMs: ElapsedMilliseconds(stopwatch),
                        cancellationToken);
                    continue;
                }

                await RecordAttemptAsync(
                    provider,
                    succeeded: true,
                    failureKind: null,
                    latencyMs: ElapsedMilliseconds(stopwatch),
                    cancellationToken);
                return new AiCompletionResult(content, provider.Id, provider.Name, provider.ModelId);
            }
            catch (AiProviderConfigurationException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is AiProviderUnavailableException
                or JsonException
                or System.Security.Cryptography.CryptographicException)
            {
                failures.Add($"{provider.Name}: {exception.Message}");
                await RecordAttemptAsync(
                    provider,
                    succeeded: false,
                    failureKind: exception.GetType().Name,
                    latencyMs: ElapsedMilliseconds(stopwatch),
                    cancellationToken);
            }
        }

        throw new AiProviderUnavailableException(
            "Không provider nào phản hồi thành công. " + string.Join(" | ", failures));
    }

    // Ghi log từng lần thử provider để vẫn có lịch sử khi provider bị vô hiệu hóa sau này.
    private async Task RecordAttemptAsync(
        AiProvider provider,
        bool succeeded,
        string? failureKind,
        int latencyMs,
        CancellationToken cancellationToken)
    {
        // Chỉ ghi metadata vận hành an toàn; insert trực tiếp để không flush entity nghiệp vụ đang tracked.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO AiOperationLogs
                (OccurredAtUtc, ProviderId, ProviderName, ModelId, Operation, Succeeded, FailureKind, LatencyMs)
            VALUES
                ({_timeProvider.GetUtcNow().UtcDateTime}, {provider.Id}, {provider.Name}, {provider.ModelId}, {"Completion"}, {succeeded}, {failureKind}, {latencyMs})
            """,
            cancellationToken);
    }

    // Chốt thời gian chạy và ép về int an toàn cho cột LatencyMs.
    private static int ElapsedMilliseconds(Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds);
    }
}
