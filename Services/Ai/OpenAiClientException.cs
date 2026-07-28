namespace ltwnc.Services.Ai;

public enum OpenAiClientFailureKind
{
    Configuration,
    Unavailable
}

// Lỗi theo ngôn ngữ của Adaptee; Adapter chuyển lỗi này sang exception của application.
public sealed class OpenAiClientException : Exception
{
    public OpenAiClientFailureKind FailureKind { get; }

    public OpenAiClientException(
        OpenAiClientFailureKind failureKind,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
    }
}
