using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.AiProviders;

public sealed class AiProviderEditViewModel
{
    public int? Id { get; set; }
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
}
