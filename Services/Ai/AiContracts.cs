using ltwnc.Models.Entities;

namespace ltwnc.Services.Ai;

public sealed record AiCompletionRequest(string SystemPrompt, string UserPrompt, int MaxTokens = 1200);

public sealed record AiCompletionResult(
    string Content,
    int ProviderId,
    string ProviderName,
    string ModelId);

public sealed record AiProviderHealthSnapshot(
    int ProviderId,
    int ConsecutiveFailureCount,
    bool IsUnstable,
    int SampleSize,
    decimal? ErrorRatePercent,
    bool ErrorRateExceeded);

public sealed class AiProviderUnavailableException : Exception
{
    // Tạo lỗi chung khi router không còn provider phù hợp để phục vụ người học.
    public AiProviderUnavailableException(string message) : base(message) { }
}

public sealed class AiProviderConfigurationException : Exception
{
    // Tạo lỗi cấu hình provider để service và router có thể phân loại fallback.
    public AiProviderConfigurationException(string message) : base(message) { }
}

// Dữ liệu form khi tạo mới hoặc cập nhật một nhà cung cấp AI.
public sealed class AiProviderInput
{
    public string Name { get; set; } = string.Empty;
    public string AdapterType { get; set; } = "OpenAICompatible";
    public string BaseUrl { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;

    // Khóa bí mật mới; để trống nghĩa là giữ nguyên khóa đã lưu.
    // Khóa chỉ đi một chiều vào hệ thống, không bao giờ được đọc lại.
    public string? ApiKey { get; set; }
    public bool ClearApiKey { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; }
    public int TimeoutSeconds { get; set; } = 60;

    // Lý do thay đổi, bắt buộc để ghi vào Bản ghi kiểm toán quản trị.
    public string Reason { get; set; } = string.Empty;

    // Khóa phiên bản đọc được khi tải form; dùng để phát hiện sửa đồng thời.
    public int Version { get; set; }
}

// Thông tin người thực hiện thao tác quản trị, phục vụ ghi Bản ghi kiểm toán.
public sealed record AiProviderActorContext(
    string ActorUserId,
    string ActorDisplay,
    string? CorrelationId = null);

// Kết quả của một thao tác thay đổi cấu hình nhà cung cấp AI.
public sealed class AiProviderOperationResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = string.Empty;

    // Tạo kết quả thành công kèm thông báo tiếng Việt cho giao diện.
    public static AiProviderOperationResult Success(string message)
    {
        return new AiProviderOperationResult
        {
            Succeeded = true,
            Message = message
        };
    }

    // Tạo kết quả thất bại kèm lý do cụ thể để hiển thị và kiểm thử.
    public static AiProviderOperationResult Failure(string message)
    {
        return new AiProviderOperationResult
        {
            Succeeded = false,
            Message = message
        };
    }
}

public interface IAiCompletionRouter
{
    // Hoàn tất một request AI qua router, có thể truyền validator để kiểm tra output.
    Task<AiCompletionResult> CompleteAsync(
        AiCompletionRequest request,
        Func<string, bool>? responseValidator = null,
        CancellationToken cancellationToken = default);
}

public interface IAiProviderAdapter
{
    string AdapterType { get; }

    // Lấy danh sách model mà provider hiện tại hỗ trợ.
    Task<IReadOnlyList<string>> GetModelsAsync(AiProvider provider, string? apiKey, CancellationToken cancellationToken);

    // Gửi request completion tới provider cụ thể.
    Task<string> CompleteAsync(AiProvider provider, string? apiKey, AiCompletionRequest request, CancellationToken cancellationToken);
}

public interface IAiProviderService
{
    // Lấy toàn bộ provider theo thứ tự vận hành để hiển thị trong Admin.
    Task<List<AiProvider>> GetAllAsync(CancellationToken cancellationToken = default);

    // Lấy một provider theo id để mở form chỉnh sửa hoặc thao tác vòng đời.
    Task<AiProvider?> GetAsync(int id, CancellationToken cancellationToken = default);

    // Tạo mới hoặc cập nhật cấu hình; mọi thay đổi đều cần lý do và được audit.
    Task<AiProviderOperationResult> SaveAsync(
        int? id,
        AiProviderInput input,
        AiProviderActorContext actor,
        CancellationToken cancellationToken = default);

    // Bật hoặc vô hiệu hóa nhà cung cấp; thay thế hoàn toàn cho thao tác xóa cứng.
    Task<AiProviderOperationResult> SetEnabledAsync(
        int id,
        bool enable,
        int version,
        string reason,
        AiProviderActorContext actor,
        CancellationToken cancellationToken = default);

    // Chọn một nhà cung cấp chính duy nhất; các nhà cung cấp khác tự mất cờ chính.
    Task<AiProviderOperationResult> SetPrimaryAsync(
        int id,
        int version,
        string reason,
        AiProviderActorContext actor,
        CancellationToken cancellationToken = default);

    // Đọc danh sách model từ provider để Admin kiểm tra cấu hình.
    Task<IReadOnlyList<string>> DiscoverModelsAsync(int id, CancellationToken cancellationToken = default);

    // Chạy health check thủ công và cập nhật kết quả gần nhất của provider.
    Task TestAsync(int id, CancellationToken cancellationToken = default);

    // Tính snapshot sức khỏe vận hành từ health check và log AI an toàn.
    Task<IReadOnlyList<AiProviderHealthSnapshot>> GetHealthSnapshotsAsync(CancellationToken cancellationToken = default);
}
