namespace ltwnc.Services.Audit;

// Nhóm danh tính người thực hiện và correlation id luôn đi cùng các lệnh quản trị có audit.
public sealed record AdminActorContext(
    string UserId,
    string Display,
    string? CorrelationId = null);
