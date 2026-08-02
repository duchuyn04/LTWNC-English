namespace ltwnc.Services.Ai;

public sealed class AiProvidersOptions
{
    public AiRoutingOptions Routing { get; set; } = new();
    public List<AiProviderOptions> Providers { get; set; } = [];
}

public sealed class AiRoutingOptions
{
    public int OverallTimeoutSeconds { get; set; } = 90;
}

public sealed class AiProviderOptions
{
    public string Name { get; set; } = string.Empty;
    public string AdapterType { get; set; } = "OpenAICompatible";
    public string BaseUrl { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsPrimary { get; set; }
    public int Priority { get; set; } = 1;
    public int TimeoutSeconds { get; set; } = 60;
}
