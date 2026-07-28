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
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_protector` để các phương thức khác sử dụng.
        _protector = dataProtection.CreateProtector("AiProvider.ApiKey.v1");
        // 3. Lưu dependency `_adapters` để các phương thức khác sử dụng.
        _adapters = adapters.ToDictionary(adapter => adapter.AdapterType, StringComparer.OrdinalIgnoreCase);
        // 4. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
        // 5. Lưu dependency `_overallTimeoutSeconds` để các phương thức khác sử dụng.
        _overallTimeoutSeconds = ReadOverallTimeoutSeconds(configuration);
    }

    // Chạy completion qua danh sách provider đủ điều kiện, có timeout tổng thể và fallback có giới hạn.
    public async Task<AiCompletionResult> CompleteAsync(
        AiCompletionRequest request,
        Func<string, bool>? responseValidator = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `LoadEligibleProvidersAsync` và lưu kết quả vào `providers`.
        List<AiProvider> providers = await LoadEligibleProvidersAsync(cancellationToken);
        // 2. Kiểm tra `providers.Count == 0` để chọn nhánh xử lý phù hợp.
        if (providers.Count == 0)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new AiProviderUnavailableException(LearnerSafeUnavailableMessage)`.
            throw new AiProviderUnavailableException(LearnerSafeUnavailableMessage);
        }

        // 4. Gọi `CreateLinkedTokenSource` và lưu kết quả vào `overallTimeout`.
        using CancellationTokenSource overallTimeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // 5. Gọi `CancelAfter` để thực hiện bước nghiệp vụ này.
        overallTimeout.CancelAfter(TimeSpan.FromSeconds(_overallTimeoutSeconds));

        // 6. Tính giá trị và lưu vào `fallbackAttempt` để dùng ở bước tiếp theo.
        int fallbackAttempt = 0;
        // 7. Duyệt từng `provider` trong `providers` để xử lý lần lượt.
        foreach (AiProvider provider in providers)
        {
            // 8. Kiểm tra `!_adapters.TryGetValue(provider.AdapterType, out IAiProviderAdapter...` để chọn nhánh xử lý phù hợp.
            if (!_adapters.TryGetValue(provider.AdapterType, out IAiProviderAdapter? adapter))
            {
                // 9. Gọi `RecordAttemptAsync` để thực hiện bước nghiệp vụ này.
                await RecordAttemptAsync(
                    provider,
                    succeeded: false,
                    failureKind: "UnsupportedAdapter",
                    latencyMs: 0,
                    fallbackAttempt: fallbackAttempt,
                    cancellationToken);
                // 10. Cập nhật bộ đếm hoặc trạng thái `fallbackAttempt`.
                fallbackAttempt++;
                // 11. Bỏ qua phần còn lại và chuyển sang lần lặp tiếp theo.
                continue;
            }

            // 12. Gọi `TryCompleteWithProviderAsync` và lưu kết quả vào `result`.
            AiCompletionResult? result = await TryCompleteWithProviderAsync(
                provider,
                adapter,
                request,
                responseValidator,
                fallbackAttempt,
                overallTimeout,
                cancellationToken);
            // 13. Kiểm tra `result != null` để chọn nhánh xử lý phù hợp.
            if (result != null)
            {
                // 14. Trả `result` cho nơi gọi.
                return result;
            }

            // 15. Kiểm tra `overallTimeout.IsCancellationRequested && !cancellationToken.IsCanc...` để chọn nhánh xử lý phù hợp.
            if (overallTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // 16. Thoát khỏi vòng lặp hoặc nhánh xử lý hiện tại.
                break;
            }

            // 17. Cập nhật bộ đếm hoặc trạng thái `fallbackAttempt`.
            fallbackAttempt++;
        }

        // 18. Dừng xử lý và phát sinh lỗi `new AiProviderUnavailableException(LearnerSafeUnavailableMessage)`.
        throw new AiProviderUnavailableException(LearnerSafeUnavailableMessage);
    }

    // Lấy snapshot provider đang bật, đã test thành công, theo provider chính rồi đến priority Admin cấu hình.
    private Task<List<AiProvider>> LoadEligibleProvidersAsync(CancellationToken cancellationToken)
    {
        // 1. Trả kết quả từ `ToListAsync` cho nơi gọi.
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
        // 1. Gọi `StartNew` và lưu kết quả vào `stopwatch`.
        Stopwatch stopwatch = Stopwatch.StartNew();
        // 2. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 3. Chuyển entity lưu trữ thành cấu hình runtime trước khi giải mã secret.
            var connection = new AiProviderConnection(
                provider.Name,
                provider.BaseUrl,
                provider.ModelId,
                provider.TimeoutSeconds);
            // 4. Giải mã khóa ngay trước khi gọi Adapter; khóa không đi vào cấu hình runtime.
            string? key = DecryptApiKey(provider);
            string content = await adapter.CompleteAsync(
                connection,
                key,
                request,
                overallTimeout.Token);
            // 5. Kiểm tra `responseValidator != null && !responseValidator(content)` để chọn nhánh xử lý phù hợp.
            if (responseValidator != null && !responseValidator(content))
            {
                // 6. Gọi `RecordAttemptAsync` để thực hiện bước nghiệp vụ này.
                await RecordAttemptAsync(
                    provider,
                    succeeded: false,
                    failureKind: "InvalidResponse",
                    latencyMs: ElapsedMilliseconds(stopwatch),
                    fallbackAttempt: fallbackAttempt,
                    cancellationToken);
                // 7. Trả `null` cho nơi gọi.
                return null;
            }

            // 8. Gọi `RecordAttemptAsync` để thực hiện bước nghiệp vụ này.
            await RecordAttemptAsync(
                provider,
                succeeded: true,
                failureKind: null,
                latencyMs: ElapsedMilliseconds(stopwatch),
                fallbackAttempt: fallbackAttempt,
                cancellationToken);
            // 9. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new AiCompletionResult(content, provider.Id, provider.Name, provider.ModelId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
            && overallTimeout.IsCancellationRequested)
        {
            // 10. Gọi `RecordAttemptAsync` để thực hiện bước nghiệp vụ này.
            await RecordAttemptAsync(
                provider,
                succeeded: false,
                failureKind: "TotalTimeout",
                latencyMs: ElapsedMilliseconds(stopwatch),
                fallbackAttempt: fallbackAttempt,
                CancellationToken.None);
            // 11. Trả `null` cho nơi gọi.
            return null;
        }
        catch (Exception exception) when (IsFallbackSafeFailure(exception))
        {
            // 12. Gọi `RecordAttemptAsync` để thực hiện bước nghiệp vụ này.
            await RecordAttemptAsync(
                provider,
                succeeded: false,
                failureKind: exception.GetType().Name,
                latencyMs: ElapsedMilliseconds(stopwatch),
                fallbackAttempt: fallbackAttempt,
                cancellationToken);
            // 13. Trả `null` cho nơi gọi.
            return null;
        }
    }

    // Giải mã khóa nội bộ ngay trước khi gọi adapter; khóa không bao giờ đi vào log vận hành.
    private string? DecryptApiKey(AiProvider provider)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(provider.EncryptedApiKey)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(provider.EncryptedApiKey))
        {
            // 2. Trả `null` cho nơi gọi.
            return null;
        }

        // 3. Trả kết quả từ `Unprotect` cho nơi gọi.
        return _protector.Unprotect(provider.EncryptedApiKey);
    }

    // Chỉ fallback các lỗi AI dự kiến; lỗi hủy request từ client thì để middleware xử lý.
    private static bool IsFallbackSafeFailure(Exception exception)
    {
        // 1. Trả `exception is AiProviderUnavailableException or AiProviderConfigurat...` cho nơi gọi.
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
        // 1. Gọi `ExecuteSqlInterpolatedAsync` để thực hiện bước nghiệp vụ này.
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
        // 1. Gọi `Stop` để thực hiện bước nghiệp vụ này.
        stopwatch.Stop();
        // 2. Trả `(int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds)` cho nơi gọi.
        return (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds);
    }

    // Đọc timeout tổng thể từ cấu hình hệ thống; giá trị sai quay về mặc định an toàn.
    private static int ReadOverallTimeoutSeconds(IConfiguration configuration)
    {
        // 1. Tính giá trị và lưu vào `configuredSeconds` để dùng ở bước tiếp theo.
        int configuredSeconds = DefaultOverallTimeoutSeconds;
        // 2. Gọi `GetValue` và lưu kết quả vào `configuredValue`.
        int? configuredValue = configuration.GetValue<int?>("AiProviders:Routing:OverallTimeoutSeconds");
        // 3. Kiểm tra `configuredValue.HasValue` để chọn nhánh xử lý phù hợp.
        if (configuredValue.HasValue)
        {
            // 4. Cập nhật `configuredSeconds` bằng giá trị mới.
            configuredSeconds = configuredValue.Value;
        }

        // 5. Trả kết quả từ `Clamp` cho nơi gọi.
        return Math.Clamp(configuredSeconds, 1, 300);
    }
}
