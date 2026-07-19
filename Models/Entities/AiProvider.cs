using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

public class AiProvider
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string AdapterType { get; set; } = "OpenAICompatible";

    [Required, MaxLength(500)]
    public string BaseUrl { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ModelId { get; set; } = string.Empty;

    public string? EncryptedApiKey { get; set; }
    [MaxLength(4)] public string? ApiKeyLastFour { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
    public DateTime? LastCheckedAt { get; set; }
    public bool? LastCheckSucceeded { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
