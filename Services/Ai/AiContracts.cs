namespace ltwnc.Services.Ai;

public sealed record AiCompletionRequest(string SystemPrompt, string UserPrompt, int MaxTokens = 1200);

public sealed record AiCompletionResult(
    string Content,
    int? ProviderId,
    string ProviderName,
    string ModelId);

public sealed class AiProviderUnavailableException : Exception
{
    // Tạo lỗi chung khi router không còn provider phù hợp để phục vụ người học.
    public AiProviderUnavailableException(string message) : base(message)
    {
        // 1. Chuyển thông báo lỗi cho lớp Exception cơ sở; không cần xử lý bổ sung.
    }
}

public sealed class AiProviderConfigurationException : Exception
{
    // Tạo lỗi cấu hình provider để service và router có thể phân loại fallback.
    public AiProviderConfigurationException(string message) : base(message)
    {
        // 1. Chuyển thông báo lỗi cấu hình cho lớp Exception cơ sở.
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
