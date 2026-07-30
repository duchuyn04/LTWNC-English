namespace ltwnc.Services.Ai;

// Cấu hình runtime tối thiểu mà Adapter cần; API key luôn được truyền riêng.
public sealed record AiProviderConnection(
    string Name,
    string BaseUrl,
    string ModelId,
    int TimeoutSeconds);

// Target: contract mà Router và dịch vụ quản trị dùng, không phụ thuộc entity hoặc giao thức OpenAI.
public interface IAiProviderAdapter
{
    string AdapterType { get; }

    // Kiểm tra cấu hình riêng của loại provider trước khi lưu hoặc kết nối.
    void ValidateConfiguration(AiProviderConnection connection);

    // Gửi request completion tới provider cụ thể.
    Task<string> CompleteAsync(
        AiProviderConnection connection,
        string? apiKey,
        AiCompletionRequest request,
        CancellationToken cancellationToken);
}
