using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.AiProviders;

public sealed class AiProviderIndexViewModel
{
    public IReadOnlyList<AiProviderRowViewModel> Providers { get; init; } = [];
}

public sealed class AiProviderRowViewModel
{
    public int Id { get; init; }
    public int Version { get; init; }
    public string Name { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public string ApiKeyDisplay { get; init; } = "Không API key";
    public bool IsEnabled { get; init; }
    public bool IsPrimary { get; init; }
    public int Priority { get; init; }
    public int TimeoutSeconds { get; init; }
    public DateTime? LastCheckedAt { get; init; }
    public bool? LastCheckSucceeded { get; init; }
    public string? LastError { get; init; }
    public int ConsecutiveFailureCount { get; init; }
    public int HealthSampleSize { get; init; }
    public decimal? ErrorRatePercent { get; init; }
    public bool ErrorRateExceeded { get; init; }
    public bool IsUnstable { get; init; }
}

public sealed class AiProviderEditViewModel
{
    public int? Id { get; set; }

    public int Version { get; set; } = 1;

    public bool IsPrimary { get; set; }

    [Required, MaxLength(120)] public string Name { get; set; } = string.Empty;
    [Required] public string AdapterType { get; set; } = "OpenAICompatible";
    [Required, Url, MaxLength(500)] public string BaseUrl { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string ModelId { get; set; } = string.Empty;
    [DataType(DataType.Password)] public string? ApiKey { get; set; }
    public bool ClearApiKey { get; set; }
    public bool HasApiKey { get; set; }
    public string? ApiKeyLastFour { get; set; }
    public bool IsEnabled { get; set; } = true;
    [Range(0, 10000)] public int Priority { get; set; }
    [Range(5, 300)] public int TimeoutSeconds { get; set; } = 60;

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class AiProviderLifecycleActionViewModel
{
    public int Version { get; set; }

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
