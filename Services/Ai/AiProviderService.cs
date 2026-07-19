using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Services.Ai;

public class AiProviderService : IAiProviderService
{
    private readonly AppDbContext _context;
    private readonly IDataProtector _protector;
    private readonly IReadOnlyDictionary<string, IAiProviderAdapter> _adapters;
    private readonly bool _allowPrivateNetworks;

    public AiProviderService(
        AppDbContext context,
        IDataProtectionProvider dataProtection,
        IEnumerable<IAiProviderAdapter> adapters,
        IConfiguration configuration)
    {
        _context = context;
        _protector = dataProtection.CreateProtector("AiProvider.ApiKey.v1");
        _adapters = adapters.ToDictionary(adapter => adapter.AdapterType, StringComparer.OrdinalIgnoreCase);
        _allowPrivateNetworks = configuration.GetValue<bool>("AiProviders:AllowPrivateNetworks");
    }

    public Task<List<AiProvider>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _context.AiProviders.OrderBy(provider => provider.Priority).ThenBy(provider => provider.Id).ToListAsync(cancellationToken);

    public Task<AiProvider?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        _context.AiProviders.FirstOrDefaultAsync(provider => provider.Id == id, cancellationToken);

    public async Task<AiProvider> SaveAsync(
        int? id,
        AiProviderInput input,
        CancellationToken cancellationToken = default)
    {
        Validate(input);
        AiProvider provider;
        if (id.HasValue)
        {
            provider = await _context.AiProviders.FindAsync([id.Value], cancellationToken)
                ?? throw new KeyNotFoundException("Provider không tồn tại.");
        }
        else
        {
            provider = new AiProvider { CreatedAt = DateTime.UtcNow };
            _context.AiProviders.Add(provider);
        }

        provider.Name = input.Name.Trim();
        provider.AdapterType = input.AdapterType.Trim();
        provider.BaseUrl = input.BaseUrl.TrimEnd('/');
        provider.ModelId = input.ModelId.Trim();
        provider.IsEnabled = input.IsEnabled;
        provider.Priority = input.Priority;
        provider.TimeoutSeconds = input.TimeoutSeconds;
        provider.UpdatedAt = DateTime.UtcNow;

        if (input.ClearApiKey)
        {
            provider.EncryptedApiKey = null;
            provider.ApiKeyLastFour = null;
        }
        else if (!string.IsNullOrWhiteSpace(input.ApiKey))
        {
            string key = input.ApiKey.Trim();
            provider.EncryptedApiKey = _protector.Protect(key);
            provider.ApiKeyLastFour = key.Length <= 4 ? key : key[^4..];
        }

        await _context.SaveChangesAsync(cancellationToken);
        return provider;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        AiProvider provider = await _context.AiProviders.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException("Provider không tồn tại.");
        _context.AiProviders.Remove(provider);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> DiscoverModelsAsync(int id, CancellationToken cancellationToken = default)
    {
        AiProvider provider = await GetRequiredAsync(id, cancellationToken);
        return await GetAdapter(provider).GetModelsAsync(provider, Decrypt(provider), cancellationToken);
    }

    public async Task TestAsync(int id, CancellationToken cancellationToken = default)
    {
        AiProvider provider = await GetRequiredAsync(id, cancellationToken);
        try
        {
            await GetAdapter(provider).CompleteAsync(
                provider,
                Decrypt(provider),
                new AiCompletionRequest("Return only JSON.", "Return {\"ok\":true}.", 64),
                cancellationToken);
            provider.LastCheckSucceeded = true;
            provider.LastError = null;
        }
        catch (Exception exception) when (exception is AiProviderUnavailableException or AiProviderConfigurationException)
        {
            provider.LastCheckSucceeded = false;
            provider.LastError = exception.Message.Length > 1000 ? exception.Message[..1000] : exception.Message;
            throw;
        }
        finally
        {
            provider.LastCheckedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(CancellationToken.None);
        }
    }

    internal string? Decrypt(AiProvider provider) =>
        string.IsNullOrWhiteSpace(provider.EncryptedApiKey) ? null : _protector.Unprotect(provider.EncryptedApiKey);

    private async Task<AiProvider> GetRequiredAsync(int id, CancellationToken cancellationToken) =>
        await _context.AiProviders.FirstOrDefaultAsync(provider => provider.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Provider không tồn tại.");

    private void Validate(AiProviderInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) throw new ArgumentException("Tên provider là bắt buộc.");
        if (string.IsNullOrWhiteSpace(input.AdapterType)) throw new ArgumentException("Adapter là bắt buộc.");
        _ = OpenAiCompatibleClient.BuildEndpoint(input.BaseUrl, "models", _allowPrivateNetworks);
        if (string.IsNullOrWhiteSpace(input.ModelId)) throw new ArgumentException("Model ID là bắt buộc.");
        if (input.TimeoutSeconds is < 5 or > 300) throw new ArgumentException("Timeout phải từ 5 đến 300 giây.");
    }

    private IAiProviderAdapter GetAdapter(AiProvider provider) =>
        _adapters.TryGetValue(provider.AdapterType, out IAiProviderAdapter? adapter)
            ? adapter
            : throw new AiProviderConfigurationException($"Adapter {provider.AdapterType} chưa được đăng ký.");
}
