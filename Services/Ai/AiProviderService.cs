using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;

namespace ltwnc.Services.Ai;

public class AiProviderService : IAiProviderService
{
    private const int DefaultHealthWindowMinutes = 5;
    private const int DefaultMinimumSampleSize = 20;
    private const decimal DefaultErrorRateThresholdPercent = 10m;
    private const int DefaultUnstableFailureThreshold = 3;

    private readonly AppDbContext _context;
    private readonly IDataProtector _protector;
    private readonly IReadOnlyDictionary<string, IAiProviderAdapter> _adapters;
    private readonly IAdminAuditService _auditService;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly bool _allowPrivateNetworks;

    public AiProviderService(
        AppDbContext context,
        IDataProtectionProvider dataProtection,
        IEnumerable<IAiProviderAdapter> adapters,
        IAdminAuditService auditService,
        IConfiguration configuration,
        TimeProvider? timeProvider = null)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_protector` để các phương thức khác sử dụng.
        _protector = dataProtection.CreateProtector("AiProvider.ApiKey.v1");
        // 3. Lưu dependency `_adapters` để các phương thức khác sử dụng.
        _adapters = adapters.ToDictionary(adapter => adapter.AdapterType, StringComparer.OrdinalIgnoreCase);
        // 4. Lưu dependency `_auditService` để các phương thức khác sử dụng.
        _auditService = auditService;
        // 5. Lưu dependency `_configuration` để các phương thức khác sử dụng.
        _configuration = configuration;
        // 6. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider ?? TimeProvider.System;
        // 7. Lưu dependency `_allowPrivateNetworks` để các phương thức khác sử dụng.
        _allowPrivateNetworks = configuration.GetValue<bool>("AiProviders:AllowPrivateNetworks");
    }

    // Lấy danh sách provider cho trang Admin, ưu tiên hiển thị provider chính trước.
    public Task<List<AiProvider>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // 1. Trả kết quả từ `ToListAsync` cho nơi gọi.
        return _context.AiProviders
            .OrderByDescending(provider => provider.IsPrimary)
            .ThenBy(provider => provider.Priority)
            .ThenBy(provider => provider.Id)
            .ToListAsync(cancellationToken);
    }

    // Lấy một provider theo mã định danh để mở form chỉnh sửa hoặc thao tác lifecycle.
    public Task<AiProvider?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        // 1. Trả kết quả từ `FirstOrDefaultAsync` cho nơi gọi.
        return _context.AiProviders.FirstOrDefaultAsync(provider => provider.Id == id, cancellationToken);
    }

    // Tính trạng thái sức khỏe provider từ bộ đếm test liên tiếp và log vận hành trong cửa sổ cấu hình.
    public async Task<IReadOnlyList<AiProviderHealthSnapshot>> GetHealthSnapshotsAsync(
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `ReadHealthWindowMinutes` và lưu kết quả vào `windowMinutes`.
        int windowMinutes = ReadHealthWindowMinutes();
        // 2. Gọi `ReadMinimumSampleSize` và lưu kết quả vào `minimumSampleSize`.
        int minimumSampleSize = ReadMinimumSampleSize();
        // 3. Gọi `ReadErrorRateThresholdPercent` và lưu kết quả vào `thresholdPercent`.
        decimal thresholdPercent = ReadErrorRateThresholdPercent();
        // 4. Gọi `ReadUnstableFailureThreshold` và lưu kết quả vào `unstableFailureThreshold`.
        int unstableFailureThreshold = ReadUnstableFailureThreshold();
        // 5. Gọi `AddMinutes` và lưu kết quả vào `windowStartUtc`.
        DateTime windowStartUtc = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-windowMinutes);

        // 6. Gọi `ToListAsync` và lưu kết quả vào `providers`.
        List<AiProvider> providers = await _context.AiProviders
            .AsNoTracking()
            .OrderBy(provider => provider.Id)
            .ToListAsync(cancellationToken);
        // 7. Gọi `ToDictionaryAsync` và lưu kết quả vào `aggregates`.
        Dictionary<int, AiOperationWindowAggregate> aggregates = await _context.AiOperationLogs
            .AsNoTracking()
            .Where(log => log.ProviderId != null && log.OccurredAtUtc >= windowStartUtc)
            .GroupBy(log => log.ProviderId!.Value)
            .Select(group => new AiOperationWindowAggregate(
                group.Key,
                group.Count(),
                group.Count(log => !log.Succeeded)))
            .ToDictionaryAsync(item => item.ProviderId, item => item, cancellationToken);

        // 8. Khởi tạo `snapshots` với dữ liệu ban đầu cần thiết.
        List<AiProviderHealthSnapshot> snapshots = new();
        // 9. Duyệt từng `provider` trong `providers` để xử lý lần lượt.
        foreach (AiProvider provider in providers)
        {
            // 10. Gọi `TryGetValue` để thực hiện bước nghiệp vụ này.
            aggregates.TryGetValue(provider.Id, out AiOperationWindowAggregate? aggregate);
            // 11. Tính giá trị và lưu vào `sampleSize` để dùng ở bước tiếp theo.
            int sampleSize = 0;
            // 12. Tính giá trị và lưu vào `failedCount` để dùng ở bước tiếp theo.
            int failedCount = 0;
            // 13. Kiểm tra `aggregate != null` để chọn nhánh xử lý phù hợp.
            if (aggregate != null)
            {
                // 14. Cập nhật `sampleSize` bằng giá trị mới.
                sampleSize = aggregate.SampleSize;
                // 15. Cập nhật `failedCount` bằng giá trị mới.
                failedCount = aggregate.FailedCount;
            }

            // 16. Tính giá trị và lưu vào `errorRatePercent` để dùng ở bước tiếp theo.
            decimal? errorRatePercent = null;
            // 17. Tính giá trị và lưu vào `errorRateExceeded` để dùng ở bước tiếp theo.
            bool errorRateExceeded = false;
            // 18. Kiểm tra `sampleSize >= minimumSampleSize` để chọn nhánh xử lý phù hợp.
            if (sampleSize >= minimumSampleSize)
            {
                // 19. Tính giá trị và lưu vào `rawRate` để dùng ở bước tiếp theo.
                decimal rawRate = failedCount * 100m / sampleSize;
                // 20. Cập nhật `errorRatePercent` bằng giá trị mới.
                errorRatePercent = decimal.Round(rawRate, 1);
                // 21. Cập nhật `errorRateExceeded` bằng giá trị mới.
                errorRateExceeded = errorRatePercent.Value > thresholdPercent;
            }

            // 22. Tính giá trị và lưu vào `isUnstable` để dùng ở bước tiếp theo.
            bool isUnstable = false;
            // 23. Kiểm tra `provider.ConsecutiveFailureCount >= unstableFailureThreshold` để chọn nhánh xử lý phù hợp.
            if (provider.ConsecutiveFailureCount >= unstableFailureThreshold)
            {
                // 24. Cập nhật `isUnstable` bằng giá trị mới.
                isUnstable = true;
            }

            // 25. Kiểm tra `errorRateExceeded` để chọn nhánh xử lý phù hợp.
            if (errorRateExceeded)
            {
                // 26. Cập nhật `isUnstable` bằng giá trị mới.
                isUnstable = true;
            }

            // 27. Gọi `Add` để thực hiện bước nghiệp vụ này.
            snapshots.Add(new AiProviderHealthSnapshot(
                provider.Id,
                provider.ConsecutiveFailureCount,
                isUnstable,
                sampleSize,
                errorRatePercent,
                errorRateExceeded));
        }

        // 28. Trả `snapshots` cho nơi gọi.
        return snapshots;
    }

    // Tạo mới hoặc cập nhật provider, kèm lý do, khóa phiên bản và audit.
    public async Task<AiProviderOperationResult> SaveAsync(
        int? id,
        AiProviderInput input,
        AiProviderActorContext actor,
        CancellationToken cancellationToken = default)
    {
        // Chặn sớm dữ liệu form không hợp lệ để không ghi thay đổi dở dang.
        // 1. Gọi `Validate` và lưu kết quả vào `validationError`.
        string? validationError = Validate(input);
        // 2. Kiểm tra `validationError != null` để chọn nhánh xử lý phù hợp.
        if (validationError != null)
        {
            // 3. Trả kết quả từ `Failure` cho nơi gọi.
            return AiProviderOperationResult.Failure(validationError);
        }

        // Lý do là bắt buộc vì mọi thay đổi cấu hình đều phải truy vết được.
        // 4. Kiểm tra `string.IsNullOrWhiteSpace(input.Reason)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            // 5. Trả kết quả từ `Failure` cho nơi gọi.
            return AiProviderOperationResult.Failure("Lý do thay đổi là bắt buộc.");
        }

        // 6. Khai báo `provider` để lưu dữ liệu dùng ở các bước sau.
        AiProvider provider;
        // 7. Khai báo `isCreate` để lưu dữ liệu dùng ở các bước sau.
        bool isCreate;

        // 8. Kiểm tra `id.HasValue` để chọn nhánh xử lý phù hợp.
        if (id.HasValue)
        {
            // 9. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `existing`.
            AiProvider? existing = await _context.AiProviders
                .FirstOrDefaultAsync(candidate => candidate.Id == id.Value, cancellationToken);
            // 10. Kiểm tra `existing == null` để chọn nhánh xử lý phù hợp.
            if (existing == null)
            {
                // 11. Trả kết quả từ `Failure` cho nơi gọi.
                return AiProviderOperationResult.Failure("Provider không tồn tại.");
            }

            // So khóa phiên bản từ form để phát hiện tab cũ hoặc thao tác đồng thời.
            // 12. Kiểm tra `existing.Version != input.Version` để chọn nhánh xử lý phù hợp.
            if (existing.Version != input.Version)
            {
                // 13. Trả kết quả từ `Failure` cho nơi gọi.
                return AiProviderOperationResult.Failure(
                    "Cấu hình đã bị người khác thay đổi. Hãy tải lại trang trước khi lưu.");
            }

            // Không cho tắt nhà cung cấp chính qua đường lưu cấu hình;
            // phải chọn nhà cung cấp chính khác trước.
            // 14. Kiểm tra `existing.IsPrimary && !input.IsEnabled` để chọn nhánh xử lý phù hợp.
            if (existing.IsPrimary && !input.IsEnabled)
            {
                // 15. Trả kết quả từ `Failure` cho nơi gọi.
                return AiProviderOperationResult.Failure(
                    "Không thể tắt nhà cung cấp chính. Hãy chọn nhà cung cấp chính khác trước.");
            }

            // 16. Cập nhật `provider` bằng giá trị mới.
            provider = existing;
            // 17. Cập nhật `isCreate` bằng giá trị mới.
            isCreate = false;
        }
        else
        {
            // 18. Cập nhật `provider` bằng giá trị mới.
            provider = new AiProvider { CreatedAt = DateTime.UtcNow };
            // 19. Gọi `Add` để thực hiện bước nghiệp vụ này.
            _context.AiProviders.Add(provider);
            // 20. Cập nhật `isCreate` bằng giá trị mới.
            isCreate = true;
        }

        // 21. Cập nhật `provider.Name` bằng giá trị mới.
        provider.Name = input.Name.Trim();
        // 22. Cập nhật `provider.AdapterType` bằng giá trị mới.
        provider.AdapterType = input.AdapterType.Trim();
        // 23. Cập nhật `provider.BaseUrl` bằng giá trị mới.
        provider.BaseUrl = input.BaseUrl.TrimEnd('/');
        // 24. Cập nhật `provider.ModelId` bằng giá trị mới.
        provider.ModelId = input.ModelId.Trim();
        // 25. Cập nhật `provider.IsEnabled` bằng giá trị mới.
        provider.IsEnabled = input.IsEnabled;
        // 26. Cập nhật `provider.Priority` bằng giá trị mới.
        provider.Priority = input.Priority;
        // 27. Cập nhật `provider.TimeoutSeconds` bằng giá trị mới.
        provider.TimeoutSeconds = input.TimeoutSeconds;
        // 28. Cập nhật `provider.UpdatedAt` bằng giá trị mới.
        provider.UpdatedAt = DateTime.UtcNow;

        // Tăng khóa phiên bản để lần sửa tiếp theo phải đọc giá trị mới nhất.
        // 29. Cập nhật `provider.Version` bằng giá trị mới.
        provider.Version = provider.Version + 1;

        // 30. Kiểm tra `input.ClearApiKey` để chọn nhánh xử lý phù hợp.
        if (input.ClearApiKey)
        {
            // 31. Cập nhật `provider.EncryptedApiKey` bằng giá trị mới.
            provider.EncryptedApiKey = null;
            // 32. Cập nhật `provider.ApiKeyLastFour` bằng giá trị mới.
            provider.ApiKeyLastFour = null;
        }
        else if (!string.IsNullOrWhiteSpace(input.ApiKey))
        {
            // Khóa bí mật chỉ được mã hóa rồi lưu; giá trị gốc không bao giờ
            // được ghi log, audit hay trả về giao diện.
            // 33. Gọi `Trim` và lưu kết quả vào `key`.
            string key = input.ApiKey.Trim();
            // 34. Cập nhật `provider.EncryptedApiKey` bằng giá trị mới.
            provider.EncryptedApiKey = _protector.Protect(key);
            // 35. Kiểm tra `key.Length <= 4` để chọn nhánh xử lý phù hợp.
            if (key.Length <= 4)
            {
                // 36. Cập nhật `provider.ApiKeyLastFour` bằng giá trị mới.
                provider.ApiKeyLastFour = key;
            }
            else
            {
                // 37. Cập nhật `provider.ApiKeyLastFour` bằng giá trị mới.
                provider.ApiKeyLastFour = key[^4..];
            }
        }

        // Audit chỉ chứa metadata công khai của provider sau khi áp dụng thay đổi.
        // 38. Khai báo `action` để lưu dữ liệu dùng ở các bước sau.
        string action;
        // 39. Kiểm tra `isCreate` để chọn nhánh xử lý phù hợp.
        if (isCreate)
        {
            // 40. Cập nhật `action` bằng giá trị mới.
            action = AdminAuditActions.AiProvidersCreate;
        }
        else
        {
            // 41. Cập nhật `action` bằng giá trị mới.
            action = AdminAuditActions.AiProvidersUpdate;
        }

        // 42. Kiểm tra `isCreate` để chọn nhánh xử lý phù hợp.
        if (isCreate)
        {
            // 43. Gọi `SaveNewProviderWithAuditAsync` để thực hiện bước nghiệp vụ này.
            await SaveNewProviderWithAuditAsync(
                actor,
                action,
                provider,
                input.Reason,
                cancellationToken);
        }
        else
        {
            // 44. Gọi `BuildAuditEntry` và lưu kết quả vào `auditEntry`.
            AdminAuditEntry auditEntry = BuildAuditEntry(
                actor,
                action,
                AdminAuditOutcome.Success,
                provider,
                input.Reason);
            // 45. Gọi `Enqueue` để thực hiện bước nghiệp vụ này.
            _auditService.Enqueue(auditEntry);
            // 46. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
        }

        // 47. Trả kết quả từ `Success` cho nơi gọi.
        return AiProviderOperationResult.Success($"Đã lưu nhà cung cấp {provider.Name}.");
    }

    // Lưu provider mới để lấy Id rồi ghi audit trong cùng transaction, tránh cấu hình không có dấu vết.
    private async Task SaveNewProviderWithAuditAsync(
        AiProviderActorContext actor,
        string action,
        AiProvider provider,
        string reason,
        CancellationToken cancellationToken)
    {
        // InMemory chỉ dùng trong unit test và không hỗ trợ transaction; database thật luôn đi nhánh relational.
        // 1. Kiểm tra `!_context.Database.IsRelational()` để chọn nhánh xử lý phù hợp.
        if (!_context.Database.IsRelational())
        {
            // 2. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
            // 3. Gọi `Enqueue` để thực hiện bước nghiệp vụ này.
            _auditService.Enqueue(BuildAuditEntry(
                actor,
                action,
                AdminAuditOutcome.Success,
                provider,
                reason));
            // 4. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
            // 5. Kết thúc phương thức sau khi hoàn tất xử lý.
            return;
        }

        // 6. Gọi `BeginTransactionAsync` và lưu kết quả vào `transaction`.
        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);
        // 7. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync(cancellationToken);
        // 8. Gọi `Enqueue` để thực hiện bước nghiệp vụ này.
        _auditService.Enqueue(BuildAuditEntry(
            actor,
            action,
            AdminAuditOutcome.Success,
            provider,
            reason));
        // 9. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync(cancellationToken);
        // 10. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
        await transaction.CommitAsync(cancellationToken);
    }

    // Bật hoặc vô hiệu hóa provider; không bao giờ xóa cứng để giữ lịch sử vận hành.
    public async Task<AiProviderOperationResult> SetEnabledAsync(
        int id,
        bool enable,
        int version,
        string reason,
        AiProviderActorContext actor,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(reason)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(reason))
        {
            // 2. Trả kết quả từ `Failure` cho nơi gọi.
            return AiProviderOperationResult.Failure("Lý do thay đổi là bắt buộc.");
        }

        // 3. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `provider`.
        AiProvider? provider = await _context.AiProviders
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        // 4. Kiểm tra `provider == null` để chọn nhánh xử lý phù hợp.
        if (provider == null)
        {
            // 5. Trả kết quả từ `Failure` cho nơi gọi.
            return AiProviderOperationResult.Failure("Provider không tồn tại.");
        }

        // 6. Kiểm tra `provider.Version != version` để chọn nhánh xử lý phù hợp.
        if (provider.Version != version)
        {
            // 7. Trả kết quả từ `Failure` cho nơi gọi.
            return AiProviderOperationResult.Failure(
                "Cấu hình đã bị người khác thay đổi. Hãy tải lại trang trước khi thao tác.");
        }

        // 8. Kiểm tra `provider.IsEnabled == enable` để chọn nhánh xử lý phù hợp.
        if (provider.IsEnabled == enable)
        {
            // 9. Kiểm tra `enable` để chọn nhánh xử lý phù hợp.
            if (enable)
            {
                // 10. Trả kết quả từ `Failure` cho nơi gọi.
                return AiProviderOperationResult.Failure("Provider này đang bật sẵn.");
            }

            // 11. Trả kết quả từ `Failure` cho nơi gọi.
            return AiProviderOperationResult.Failure("Provider này đã bị tắt trước đó.");
        }

        // Vô hiệu hóa thay thế hoàn toàn cho xóa cứng: nhà cung cấp đã có
        // lịch sử vận hành (AiOperationLogs) vẫn được giữ lại nguyên vẹn.
        // Không cho tắt nhà cung cấp chính vì hệ thống cần một đường AI mặc định.
        // 12. Kiểm tra `!enable && provider.IsPrimary` để chọn nhánh xử lý phù hợp.
        if (!enable && provider.IsPrimary)
        {
            // 13. Gọi `Enqueue` để thực hiện bước nghiệp vụ này.
            _auditService.Enqueue(BuildAuditEntry(
                actor,
                AdminAuditActions.AiProvidersDisable,
                AdminAuditOutcome.Denied,
                provider,
                reason));
            // 14. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
            // 15. Trả kết quả từ `Failure` cho nơi gọi.
            return AiProviderOperationResult.Failure(
                "Không thể tắt nhà cung cấp chính. Hãy chọn nhà cung cấp chính khác trước.");
        }

        // 16. Cập nhật `provider.IsEnabled` bằng giá trị mới.
        provider.IsEnabled = enable;
        // 17. Cập nhật `provider.UpdatedAt` bằng giá trị mới.
        provider.UpdatedAt = DateTime.UtcNow;
        // 18. Cập nhật `provider.Version` bằng giá trị mới.
        provider.Version = provider.Version + 1;

        // 19. Khai báo `action` để lưu dữ liệu dùng ở các bước sau.
        string action;
        // 20. Kiểm tra `enable` để chọn nhánh xử lý phù hợp.
        if (enable)
        {
            // 21. Cập nhật `action` bằng giá trị mới.
            action = AdminAuditActions.AiProvidersEnable;
        }
        else
        {
            // 22. Cập nhật `action` bằng giá trị mới.
            action = AdminAuditActions.AiProvidersDisable;
        }

        // 23. Gọi `Enqueue` để thực hiện bước nghiệp vụ này.
        _auditService.Enqueue(BuildAuditEntry(actor, action, AdminAuditOutcome.Success, provider, reason));
        // 24. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync(cancellationToken);

        // 25. Kiểm tra `enable` để chọn nhánh xử lý phù hợp.
        if (enable)
        {
            // 26. Trả kết quả từ `Success` cho nơi gọi.
            return AiProviderOperationResult.Success($"Đã bật provider {provider.Name}.");
        }

        // 27. Trả kết quả từ `Success` cho nơi gọi.
        return AiProviderOperationResult.Success($"Đã vô hiệu hóa provider {provider.Name}.");
    }

    // Chọn provider chính duy nhất cho hệ thống và gỡ cờ chính ở các provider còn lại.
    public async Task<AiProviderOperationResult> SetPrimaryAsync(
        int id,
        int version,
        string reason,
        AiProviderActorContext actor,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(reason)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(reason))
        {
            // 2. Trả kết quả từ `Failure` cho nơi gọi.
            return AiProviderOperationResult.Failure("Lý do thay đổi là bắt buộc.");
        }

        // 3. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `provider`.
        AiProvider? provider = await _context.AiProviders
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        // 4. Kiểm tra `provider == null` để chọn nhánh xử lý phù hợp.
        if (provider == null)
        {
            // 5. Trả kết quả từ `Failure` cho nơi gọi.
            return AiProviderOperationResult.Failure("Provider không tồn tại.");
        }

        // 6. Kiểm tra `provider.Version != version` để chọn nhánh xử lý phù hợp.
        if (provider.Version != version)
        {
            // 7. Trả kết quả từ `Failure` cho nơi gọi.
            return AiProviderOperationResult.Failure(
                "Cấu hình đã bị người khác thay đổi. Hãy tải lại trang trước khi thao tác.");
        }

        // Nhà cung cấp đã tắt không thể làm đường AI chính của hệ thống.
        // 8. Kiểm tra `!provider.IsEnabled` để chọn nhánh xử lý phù hợp.
        if (!provider.IsEnabled)
        {
            // 9. Gọi `Enqueue` để thực hiện bước nghiệp vụ này.
            _auditService.Enqueue(BuildAuditEntry(
                actor,
                AdminAuditActions.AiProvidersSetPrimary,
                AdminAuditOutcome.Denied,
                provider,
                reason));
            // 10. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
            // 11. Trả kết quả từ `Failure` cho nơi gọi.
            return AiProviderOperationResult.Failure(
                "Không thể chọn nhà cung cấp đã tắt làm nhà cung cấp chính.");
        }

        // 12. Kiểm tra `provider.IsPrimary` để chọn nhánh xử lý phù hợp.
        if (provider.IsPrimary)
        {
            // 13. Trả kết quả từ `Failure` cho nơi gọi.
            return AiProviderOperationResult.Failure("Provider này đã là nhà cung cấp chính.");
        }

        // Gỡ cờ chính khỏi mọi nhà cung cấp khác để toàn hệ thống
        // chỉ còn đúng một nhà cung cấp chính.
        // 14. Gọi `ToListAsync` và lưu kết quả vào `currentPrimaries`.
        List<AiProvider> currentPrimaries = await _context.AiProviders
            .Where(candidate => candidate.IsPrimary && candidate.Id != provider.Id)
            .ToListAsync(cancellationToken);
        // 15. Duyệt từng `currentPrimary` trong `currentPrimaries` để xử lý lần lượt.
        foreach (AiProvider currentPrimary in currentPrimaries)
        {
            // 16. Cập nhật `currentPrimary.IsPrimary` bằng giá trị mới.
            currentPrimary.IsPrimary = false;
            // 17. Cập nhật `currentPrimary.UpdatedAt` bằng giá trị mới.
            currentPrimary.UpdatedAt = DateTime.UtcNow;
            // 18. Cập nhật `currentPrimary.Version` bằng giá trị mới.
            currentPrimary.Version = currentPrimary.Version + 1;
        }

        // 19. Cập nhật `provider.IsPrimary` bằng giá trị mới.
        provider.IsPrimary = true;
        // 20. Cập nhật `provider.UpdatedAt` bằng giá trị mới.
        provider.UpdatedAt = DateTime.UtcNow;
        // 21. Cập nhật `provider.Version` bằng giá trị mới.
        provider.Version = provider.Version + 1;

        // 22. Gọi `Enqueue` để thực hiện bước nghiệp vụ này.
        _auditService.Enqueue(BuildAuditEntry(
            actor,
            AdminAuditActions.AiProvidersSetPrimary,
            AdminAuditOutcome.Success,
            provider,
            reason));
        // 23. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync(cancellationToken);

        // 24. Trả kết quả từ `Success` cho nơi gọi.
        return AiProviderOperationResult.Success($"Đã chọn {provider.Name} làm nhà cung cấp chính.");
    }

    // Gọi endpoint models của provider để Admin kiểm tra danh sách model hiện có.
    public async Task<IReadOnlyList<string>> DiscoverModelsAsync(int id, CancellationToken cancellationToken = default)
    {
        // 1. Gọi `GetRequiredAsync` và lưu kết quả vào `provider`.
        AiProvider provider = await GetRequiredAsync(id, cancellationToken);
        // 2. Trả kết quả từ `GetModelsAsync` cho nơi gọi.
        return await GetAdapter(provider).GetModelsAsync(provider, Decrypt(provider), cancellationToken);
    }

    // Thử một completion ngắn để xác nhận provider còn kết nối được.
    public async Task TestAsync(int id, CancellationToken cancellationToken = default)
    {
        // 1. Gọi `GetRequiredAsync` và lưu kết quả vào `provider`.
        AiProvider provider = await GetRequiredAsync(id, cancellationToken);
        // 2. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 3. Gọi `CompleteAsync` để thực hiện bước nghiệp vụ này.
            await GetAdapter(provider).CompleteAsync(
                provider,
                Decrypt(provider),
                new AiCompletionRequest("Return only JSON.", "Return {\"ok\":true}.", 64),
                cancellationToken);
            // 4. Cập nhật `provider.LastCheckSucceeded` bằng giá trị mới.
            provider.LastCheckSucceeded = true;
            // 5. Cập nhật `provider.LastError` bằng giá trị mới.
            provider.LastError = null;
            // 6. Cập nhật `provider.ConsecutiveFailureCount` bằng giá trị mới.
            provider.ConsecutiveFailureCount = 0;
        }
        catch (Exception exception) when (exception is AiProviderUnavailableException or AiProviderConfigurationException)
        {
            // 7. Cập nhật `provider.LastCheckSucceeded` bằng giá trị mới.
            provider.LastCheckSucceeded = false;
            // 8. Cập nhật bộ đếm hoặc trạng thái `provider.ConsecutiveFailureCount`.
            provider.ConsecutiveFailureCount++;
            // 9. Kiểm tra `exception.Message.Length > 1000` để chọn nhánh xử lý phù hợp.
            if (exception.Message.Length > 1000)
            {
                // 10. Cập nhật `provider.LastError` bằng giá trị mới.
                provider.LastError = exception.Message[..1000];
            }
            else
            {
                // 11. Cập nhật `provider.LastError` bằng giá trị mới.
                provider.LastError = exception.Message;
            }
            // 12. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        finally
        {
            // 13. Cập nhật `provider.LastCheckedAt` bằng giá trị mới.
            provider.LastCheckedAt = _timeProvider.GetUtcNow().UtcDateTime;
            // 14. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(CancellationToken.None);
        }
    }

    // Giải mã khóa nội bộ ngay trước khi gọi adapter; không truyền khóa ra ViewModel hay JSON.
    internal string? Decrypt(AiProvider provider)
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

    // Lấy provider bắt buộc phải tồn tại cho các thao tác test/discover.
    private async Task<AiProvider> GetRequiredAsync(int id, CancellationToken cancellationToken)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `provider`.
        AiProvider? provider = await _context.AiProviders
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        // 2. Kiểm tra `provider == null` để chọn nhánh xử lý phù hợp.
        if (provider == null)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Provider không tồn tại.")`.
            throw new KeyNotFoundException("Provider không tồn tại.");
        }

        // 4. Trả `provider` cho nơi gọi.
        return provider;
    }

    // Kiểm tra dữ liệu cấu hình; trả về thông báo lỗi đầu tiên hoặc null nếu hợp lệ.
    private string? Validate(AiProviderInput input)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(input.Name)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            // 2. Trả `"Tên provider là bắt buộc."` cho nơi gọi.
            return "Tên provider là bắt buộc.";
        }

        // 3. Kiểm tra `string.IsNullOrWhiteSpace(input.AdapterType)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(input.AdapterType))
        {
            // 4. Trả `"Adapter là bắt buộc."` cho nơi gọi.
            return "Adapter là bắt buộc.";
        }

        // Tái sử dụng bảo vệ SSRF: chặn http cho host ngoài và chặn dải mạng nội bộ.
        // 5. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 6. Cập nhật `_` bằng giá trị mới.
            _ = OpenAiCompatibleApiClient.BuildEndpoint(input.BaseUrl, "models", _allowPrivateNetworks);
        }
        catch (ArgumentException exception)
        {
            // 7. Trả `exception.Message` cho nơi gọi.
            return exception.Message;
        }

        // 8. Kiểm tra `string.IsNullOrWhiteSpace(input.ModelId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(input.ModelId))
        {
            // 9. Trả `"Model ID là bắt buộc."` cho nơi gọi.
            return "Model ID là bắt buộc.";
        }

        // 10. Kiểm tra `input.TimeoutSeconds is < 5 or > 300` để chọn nhánh xử lý phù hợp.
        if (input.TimeoutSeconds is < 5 or > 300)
        {
            // 11. Trả `"Timeout phải từ 5 đến 300 giây."` cho nơi gọi.
            return "Timeout phải từ 5 đến 300 giây.";
        }

        // 12. Trả `null` cho nơi gọi.
        return null;
    }

    // Dựng payload audit đã lọc; metadata chỉ chứa thông tin cấu hình công khai,
    // tuyệt đối không chứa khóa bí mật (đã được AdminAuditMetadata chặn thêm một lớp).
    private AdminAuditEntry BuildAuditEntry(
        AiProviderActorContext actor,
        string action,
        string outcome,
        AiProvider provider,
        string reason)
    {
        // 1. Khởi tạo `metadata` với dữ liệu ban đầu cần thiết.
        Dictionary<string, string?> metadata = new()
        {
            ["providerName"] = provider.Name,
            ["adapterType"] = provider.AdapterType,
            ["modelId"] = provider.ModelId,
            ["isEnabled"] = provider.IsEnabled.ToString(),
            ["isPrimary"] = provider.IsPrimary.ToString(),
            ["priority"] = provider.Priority.ToString()
        };

        // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminAuditEntry(
            actor.ActorUserId,
            actor.ActorDisplay,
            action,
            outcome,
            TargetType: "AiProvider",
            TargetId: provider.Id.ToString(),
            Reason: reason,
            CorrelationId: actor.CorrelationId,
            Metadata: metadata);
    }

    // Tìm adapter đã đăng ký trong DI theo loại provider.
    private IAiProviderAdapter GetAdapter(AiProvider provider)
    {
        // 1. Kiểm tra `_adapters.TryGetValue(provider.AdapterType, out IAiProviderAdapter?...` để chọn nhánh xử lý phù hợp.
        if (_adapters.TryGetValue(provider.AdapterType, out IAiProviderAdapter? adapter))
        {
            // 2. Trả `adapter` cho nơi gọi.
            return adapter;
        }

        // 3. Dừng xử lý và phát sinh lỗi `new AiProviderConfigurationException($"Adapter {provider.AdapterTyp...`.
        throw new AiProviderConfigurationException($"Adapter {provider.AdapterType} chưa được đăng ký.");
    }

    // Đọc cửa sổ tính lỗi từ cấu hình hệ thống; giá trị sai sẽ quay về 5 phút.
    private int ReadHealthWindowMinutes()
    {
        // 1. Tính giá trị và lưu vào `value` để dùng ở bước tiếp theo.
        int value = _configuration.GetValue<int?>("AiProviders:Health:WindowMinutes")
            ?? DefaultHealthWindowMinutes;
        // 2. Trả kết quả từ `Clamp` cho nơi gọi.
        return Math.Clamp(value, 1, 60);
    }

    // Đọc số mẫu tối thiểu từ cấu hình hệ thống; giá trị sai sẽ quay về 20 request.
    private int ReadMinimumSampleSize()
    {
        // 1. Tính giá trị và lưu vào `value` để dùng ở bước tiếp theo.
        int value = _configuration.GetValue<int?>("AiProviders:Health:MinimumSampleSize")
            ?? DefaultMinimumSampleSize;
        // 2. Trả kết quả từ `Clamp` cho nơi gọi.
        return Math.Clamp(value, 1, 10_000);
    }

    // Đọc ngưỡng tỷ lệ lỗi từ cấu hình hệ thống; giá trị sai sẽ quay về 10%.
    private decimal ReadErrorRateThresholdPercent()
    {
        // 1. Tính giá trị và lưu vào `value` để dùng ở bước tiếp theo.
        decimal value = _configuration.GetValue<decimal?>("AiProviders:Health:ErrorRateThresholdPercent")
            ?? DefaultErrorRateThresholdPercent;
        // 2. Trả kết quả từ `Clamp` cho nơi gọi.
        return Math.Clamp(value, 0m, 100m);
    }

    // Đọc số lần test fail liên tiếp để xem provider không ổn định.
    private int ReadUnstableFailureThreshold()
    {
        // 1. Tính giá trị và lưu vào `value` để dùng ở bước tiếp theo.
        int value = _configuration.GetValue<int?>("AiProviders:Health:UnstableFailureThreshold")
            ?? DefaultUnstableFailureThreshold;
        // 2. Trả kết quả từ `Clamp` cho nơi gọi.
        return Math.Clamp(value, 1, 100);
    }

    private sealed record AiOperationWindowAggregate(
        int ProviderId,
        int SampleSize,
        int FailedCount);
}
