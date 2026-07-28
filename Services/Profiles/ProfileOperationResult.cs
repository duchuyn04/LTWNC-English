namespace ltwnc.Services.Profiles;

public sealed record ProfileFieldError(string Field, string Message);

public sealed class ProfileOperationResult
{
    public bool Succeeded { get; init; }
    public IReadOnlyList<ProfileFieldError> Errors { get; init; } = [];

    public static ProfileOperationResult Success()
    {
        // 1. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new() { Succeeded = true };
    }

    public static ProfileOperationResult Failure(params ProfileFieldError[] errors)
    {
        // 1. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new() { Errors = errors };
    }
}
