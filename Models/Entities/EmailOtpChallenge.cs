using ltwnc.Models.Enums;

namespace ltwnc.Models.Entities;

public sealed class EmailOtpChallenge
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public EmailOtpPurpose Purpose { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? PendingRegistrationId { get; set; }
    public string? GoogleSubjectId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? UsedAtUtc { get; set; }
    public int FailedAttempts { get; set; }
    public string? RequestIpAddress { get; set; }
}
