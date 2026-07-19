using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;

namespace ltwnc.Services.Ai;

public class AiProviderService : IAiProviderService
{
    private readonly AppDbContext _context;
    private readonly IDataProtector _protector;
    private readonly IReadOnlyDictionary<string, IAiProviderAdapter> _adapters;
    private readonly IAdminAuditService _auditService;
    private readonly bool _allowPrivateNetworks;

    public AiProviderService(
        AppDbContext context,
        IDataProtectionProvider dataProtection,
        IEnumerable<IAiProviderAdapter> adapters,
        IAdminAuditService auditService,
        IConfiguration configuration)
    {
        _context = context;
        _protector = dataProtection.CreateProtector("AiProvider.ApiKey.v1");
        _adapters = adapters.ToDictionary(adapter => adapter.AdapterType, StringComparer.OrdinalIgnoreCase);
        _auditService = auditService;
        _allowPrivateNetworks = configuration.GetValue<bool>("AiProviders:AllowPrivateNetworks");
    }

    // Lấy danh sách provider cho trang Admin, ưu tiên hiển thị provider chính trước.
    public Task<List<AiProvider>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _context.AiProviders
            .OrderByDescending(provider => provider.IsPrimary)
            .ThenBy(provider => provider.Priority)
            .ThenBy(provider => provider.Id)
            .ToListAsync(cancellationToken);
    }

    // Lấy một provider theo mã định danh để mở form chỉnh sửa hoặc thao tác lifecycle.
    public Task<AiProvider?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        return _context.AiProviders.FirstOrDefaultAsync(provider => provider.Id == id, cancellationToken);
    }

    // Tạo mới hoặc cập nhật provider, kèm lý do, khóa phiên bản và audit.
    public async Task<AiProviderOperationResult> SaveAsync(
        int? id,
        AiProviderInput input,
        AiProviderActorContext actor,
        CancellationToken cancellationToken = default)
    {
        // Chặn sớm dữ liệu form không hợp lệ để không ghi thay đổi dở dang.
        string? validationError = Validate(input);
        if (validationError != null)
        {
            return AiProviderOperationResult.Failure(validationError);
        }

        // Lý do là bắt buộc vì mọi thay đổi cấu hình đều phải truy vết được.
        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return AiProviderOperationResult.Failure("Lý do thay đổi là bắt buộc.");
        }

        AiProvider provider;
        bool isCreate;

        if (id.HasValue)
        {
            AiProvider? existing = await _context.AiProviders
                .FirstOrDefaultAsync(candidate => candidate.Id == id.Value, cancellationToken);
            if (existing == null)
            {
                return AiProviderOperationResult.Failure("Provider không tồn tại.");
            }

            // So khóa phiên bản từ form để phát hiện tab cũ hoặc thao tác đồng thời.
            if (existing.Version != input.Version)
            {
                return AiProviderOperationResult.Failure(
                    "Cấu hình đã bị người khác thay đổi. Hãy tải lại trang trước khi lưu.");
            }

            // Không cho tắt nhà cung cấp chính qua đường lưu cấu hình;
            // phải chọn nhà cung cấp chính khác trước.
            if (existing.IsPrimary && !input.IsEnabled)
            {
                return AiProviderOperationResult.Failure(
                    "Không thể tắt nhà cung cấp chính. Hãy chọn nhà cung cấp chính khác trước.");
            }

            provider = existing;
            isCreate = false;
        }
        else
        {
            provider = new AiProvider { CreatedAt = DateTime.UtcNow };
            _context.AiProviders.Add(provider);
            isCreate = true;
        }

        provider.Name = input.Name.Trim();
        provider.AdapterType = input.AdapterType.Trim();
        provider.BaseUrl = input.BaseUrl.TrimEnd('/');
        provider.ModelId = input.ModelId.Trim();
        provider.IsEnabled = input.IsEnabled;
        provider.Priority = input.Priority;
        provider.TimeoutSeconds = input.TimeoutSeconds;
        provider.UpdatedAt = DateTime.UtcNow;

        // Tăng khóa phiên bản để lần sửa tiếp theo phải đọc giá trị mới nhất.
        provider.Version = provider.Version + 1;

        if (input.ClearApiKey)
        {
            provider.EncryptedApiKey = null;
            provider.ApiKeyLastFour = null;
        }
        else if (!string.IsNullOrWhiteSpace(input.ApiKey))
        {
            // Khóa bí mật chỉ được mã hóa rồi lưu; giá trị gốc không bao giờ
            // được ghi log, audit hay trả về giao diện.
            string key = input.ApiKey.Trim();
            provider.EncryptedApiKey = _protector.Protect(key);
            if (key.Length <= 4)
            {
                provider.ApiKeyLastFour = key;
            }
            else
            {
                provider.ApiKeyLastFour = key[^4..];
            }
        }

        // Với provider mới, cần lưu trước để database cấp Id rồi audit mới ghi đúng TargetId.
        if (isCreate)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Audit chỉ chứa metadata công khai của provider sau khi áp dụng thay đổi.
        string action;
        if (isCreate)
        {
            action = AdminAuditActions.AiProvidersCreate;
        }
        else
        {
            action = AdminAuditActions.AiProvidersUpdate;
        }

        _auditService.Enqueue(BuildAuditEntry(actor, action, AdminAuditOutcome.Success, provider, input.Reason));
        await _context.SaveChangesAsync(cancellationToken);

        return AiProviderOperationResult.Success($"Đã lưu provider {provider.Name}.");
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
        if (string.IsNullOrWhiteSpace(reason))
        {
            return AiProviderOperationResult.Failure("Lý do thay đổi là bắt buộc.");
        }

        AiProvider? provider = await _context.AiProviders
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (provider == null)
        {
            return AiProviderOperationResult.Failure("Provider không tồn tại.");
        }

        if (provider.Version != version)
        {
            return AiProviderOperationResult.Failure(
                "Cấu hình đã bị người khác thay đổi. Hãy tải lại trang trước khi thao tác.");
        }

        if (provider.IsEnabled == enable)
        {
            if (enable)
            {
                return AiProviderOperationResult.Failure("Provider này đang bật sẵn.");
            }

            return AiProviderOperationResult.Failure("Provider này đã bị tắt trước đó.");
        }

        // Vô hiệu hóa thay thế hoàn toàn cho xóa cứng: nhà cung cấp đã có
        // lịch sử vận hành (AiOperationLogs) vẫn được giữ lại nguyên vẹn.
        // Không cho tắt nhà cung cấp chính vì hệ thống cần một đường AI mặc định.
        if (!enable && provider.IsPrimary)
        {
            _auditService.Enqueue(BuildAuditEntry(
                actor,
                AdminAuditActions.AiProvidersDisable,
                AdminAuditOutcome.Denied,
                provider,
                reason));
            await _context.SaveChangesAsync(cancellationToken);
            return AiProviderOperationResult.Failure(
                "Không thể tắt nhà cung cấp chính. Hãy chọn nhà cung cấp chính khác trước.");
        }

        provider.IsEnabled = enable;
        provider.UpdatedAt = DateTime.UtcNow;
        provider.Version = provider.Version + 1;

        string action;
        if (enable)
        {
            action = AdminAuditActions.AiProvidersEnable;
        }
        else
        {
            action = AdminAuditActions.AiProvidersDisable;
        }

        _auditService.Enqueue(BuildAuditEntry(actor, action, AdminAuditOutcome.Success, provider, reason));
        await _context.SaveChangesAsync(cancellationToken);

        if (enable)
        {
            return AiProviderOperationResult.Success($"Đã bật provider {provider.Name}.");
        }

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
        if (string.IsNullOrWhiteSpace(reason))
        {
            return AiProviderOperationResult.Failure("Lý do thay đổi là bắt buộc.");
        }

        AiProvider? provider = await _context.AiProviders
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (provider == null)
        {
            return AiProviderOperationResult.Failure("Provider không tồn tại.");
        }

        if (provider.Version != version)
        {
            return AiProviderOperationResult.Failure(
                "Cấu hình đã bị người khác thay đổi. Hãy tải lại trang trước khi thao tác.");
        }

        // Nhà cung cấp đã tắt không thể làm đường AI chính của hệ thống.
        if (!provider.IsEnabled)
        {
            _auditService.Enqueue(BuildAuditEntry(
                actor,
                AdminAuditActions.AiProvidersSetPrimary,
                AdminAuditOutcome.Denied,
                provider,
                reason));
            await _context.SaveChangesAsync(cancellationToken);
            return AiProviderOperationResult.Failure(
                "Không thể chọn nhà cung cấp đã tắt làm nhà cung cấp chính.");
        }

        if (provider.IsPrimary)
        {
            return AiProviderOperationResult.Failure("Provider này đã là nhà cung cấp chính.");
        }

        // Gỡ cờ chính khỏi mọi nhà cung cấp khác để toàn hệ thống
        // chỉ còn đúng một nhà cung cấp chính.
        List<AiProvider> currentPrimaries = await _context.AiProviders
            .Where(candidate => candidate.IsPrimary && candidate.Id != provider.Id)
            .ToListAsync(cancellationToken);
        foreach (AiProvider currentPrimary in currentPrimaries)
        {
            currentPrimary.IsPrimary = false;
            currentPrimary.UpdatedAt = DateTime.UtcNow;
            currentPrimary.Version = currentPrimary.Version + 1;
        }

        provider.IsPrimary = true;
        provider.UpdatedAt = DateTime.UtcNow;
        provider.Version = provider.Version + 1;

        _auditService.Enqueue(BuildAuditEntry(
            actor,
            AdminAuditActions.AiProvidersSetPrimary,
            AdminAuditOutcome.Success,
            provider,
            reason));
        await _context.SaveChangesAsync(cancellationToken);

        return AiProviderOperationResult.Success($"Đã chọn {provider.Name} làm nhà cung cấp chính.");
    }

    // Gọi endpoint models của provider để Admin kiểm tra danh sách model hiện có.
    public async Task<IReadOnlyList<string>> DiscoverModelsAsync(int id, CancellationToken cancellationToken = default)
    {
        AiProvider provider = await GetRequiredAsync(id, cancellationToken);
        return await GetAdapter(provider).GetModelsAsync(provider, Decrypt(provider), cancellationToken);
    }

    // Thử một completion ngắn để xác nhận provider còn kết nối được.
    public async Task TestAsync(int id, CancellationToken cancellationToken = default)
    {
        AiProvider provider = await GetRequiredAsync(id, cancellationToken);
        try
        {
            await GetAdapter(provider).CompleteAsync(
                provider,
                Decrypt(provider),
                new AiCompletionRequest("Return only JSON.", "Return {\"ok\":true}.", 64),
                cancellationToken);
            provider.LastCheckSucceeded = true;
            provider.LastError = null;
        }
        catch (Exception exception) when (exception is AiProviderUnavailableException or AiProviderConfigurationException)
        {
            provider.LastCheckSucceeded = false;
            if (exception.Message.Length > 1000)
            {
                provider.LastError = exception.Message[..1000];
            }
            else
            {
                provider.LastError = exception.Message;
            }
            throw;
        }
        finally
        {
            provider.LastCheckedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(CancellationToken.None);
        }
    }

    // Giải mã khóa nội bộ ngay trước khi gọi adapter; không truyền khóa ra ViewModel hay JSON.
    internal string? Decrypt(AiProvider provider)
    {
        if (string.IsNullOrWhiteSpace(provider.EncryptedApiKey))
        {
            return null;
        }

        return _protector.Unprotect(provider.EncryptedApiKey);
    }

    // Lấy provider bắt buộc phải tồn tại cho các thao tác test/discover.
    private async Task<AiProvider> GetRequiredAsync(int id, CancellationToken cancellationToken)
    {
        AiProvider? provider = await _context.AiProviders
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (provider == null)
        {
            throw new KeyNotFoundException("Provider không tồn tại.");
        }

        return provider;
    }

    // Kiểm tra dữ liệu cấu hình; trả về thông báo lỗi đầu tiên hoặc null nếu hợp lệ.
    private string? Validate(AiProviderInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return "Tên provider là bắt buộc.";
        }

        if (string.IsNullOrWhiteSpace(input.AdapterType))
        {
            return "Adapter là bắt buộc.";
        }

        // Tái sử dụng bảo vệ SSRF: chặn http cho host ngoài và chặn dải mạng nội bộ.
        try
        {
            _ = OpenAiCompatibleClient.BuildEndpoint(input.BaseUrl, "models", _allowPrivateNetworks);
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }

        if (string.IsNullOrWhiteSpace(input.ModelId))
        {
            return "Model ID là bắt buộc.";
        }

        if (input.TimeoutSeconds is < 5 or > 300)
        {
            return "Timeout phải từ 5 đến 300 giây.";
        }

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
        Dictionary<string, string?> metadata = new()
        {
            ["providerName"] = provider.Name,
            ["adapterType"] = provider.AdapterType,
            ["modelId"] = provider.ModelId,
            ["isEnabled"] = provider.IsEnabled.ToString(),
            ["isPrimary"] = provider.IsPrimary.ToString(),
            ["priority"] = provider.Priority.ToString()
        };

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
        if (_adapters.TryGetValue(provider.AdapterType, out IAiProviderAdapter? adapter))
        {
            return adapter;
        }

        throw new AiProviderConfigurationException($"Adapter {provider.AdapterType} chưa được đăng ký.");
    }
}
