using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

public class AiOperationLog
{
    [Key]
    public long Id { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public int? ProviderId { get; set; }

    [MaxLength(120)]
    public string? ProviderName { get; set; }

    [MaxLength(200)]
    public string? ModelId { get; set; }

    [Required, MaxLength(80)]
    public string Operation { get; set; } = "Completion";

    public bool Succeeded { get; set; }

    [MaxLength(80)]
    public string? FailureKind { get; set; }

    public int LatencyMs { get; set; }

    // 0 là provider đầu tiên trong snapshot; 1+ là các lần chuyển sang provider dự phòng.
    public int FallbackAttempt { get; set; }
}
