using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ltwnc.Models.Entities;

namespace ltwnc.Services.Ai;

// Adaptee: giao tiếp trực tiếp với OpenAI-compatible HTTP API bằng contract riêng của giao thức.
public sealed class OpenAiCompatibleApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly bool _allowPrivateNetworks;

    public OpenAiCompatibleApiClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _allowPrivateNetworks = configuration.GetValue<bool>("AiProviders:AllowPrivateNetworks");
    }

    public async Task<IReadOnlyList<string>> GetModelsAsync(
        AiProvider provider,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        Uri endpoint = CreateEndpoint(provider, "models");
        await ValidateResolvedHostAsync(endpoint, cancellationToken);
        using HttpRequestMessage request = new(HttpMethod.Get, endpoint);
        AddAuthorization(request, apiKey);
        using HttpResponseMessage response = await SendAsync(provider, request, cancellationToken);
        EnsureSuccess(provider, response);

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            if (!document.RootElement.TryGetProperty("data", out JsonElement data)
                || data.ValueKind != JsonValueKind.Array)
            {
                throw InvalidResponse(provider);
            }

            var modelIds = new List<string>();
            foreach (JsonElement item in data.EnumerateArray())
            {
                if (item.TryGetProperty("id", out JsonElement id)
                    && !string.IsNullOrWhiteSpace(id.GetString()))
                {
                    modelIds.Add(id.GetString()!);
                }
            }

            return modelIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }
        catch (JsonException exception)
        {
            throw InvalidResponse(provider, exception);
        }
    }

    public async Task<OpenAiChatResponse> CompleteAsync(
        AiProvider provider,
        string? apiKey,
        OpenAiChatRequest completion,
        CancellationToken cancellationToken)
    {
        Uri endpoint = CreateEndpoint(provider, "chat/completions");
        await ValidateResolvedHostAsync(endpoint, cancellationToken);
        using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(completion)
        };
        AddAuthorization(request, apiKey);
        using HttpResponseMessage response = await SendAsync(provider, request, cancellationToken);
        EnsureSuccess(provider, response);

        try
        {
            OpenAiChatResponse? result = JsonSerializer.Deserialize<OpenAiChatResponse>(
                await response.Content.ReadAsStringAsync(cancellationToken));
            return result ?? throw InvalidResponse(provider);
        }
        catch (JsonException exception)
        {
            throw InvalidResponse(provider, exception);
        }
    }

    private Uri CreateEndpoint(AiProvider provider, string relativePath)
    {
        try
        {
            return BuildEndpoint(provider.BaseUrl, relativePath, _allowPrivateNetworks);
        }
        catch (ArgumentException exception)
        {
            throw new OpenAiClientException(
                OpenAiClientFailureKind.Configuration,
                exception.Message,
                exception);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        AiProvider provider,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpClient client = _httpClientFactory.CreateClient("AiProvider");
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(provider.TimeoutSeconds, 5, 300)));

        try
        {
            return await client.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OpenAiClientException(
                OpenAiClientFailureKind.Unavailable,
                $"{provider.Name} đã hết thời gian chờ.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new OpenAiClientException(
                OpenAiClientFailureKind.Unavailable,
                $"Không thể kết nối {provider.Name}.",
                exception);
        }
    }

    private static void EnsureSuccess(AiProvider provider, HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        OpenAiClientFailureKind failureKind =
            response.StatusCode is HttpStatusCode.BadRequest
                or HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden
                ? OpenAiClientFailureKind.Configuration
                : OpenAiClientFailureKind.Unavailable;

        throw new OpenAiClientException(
            failureKind,
            $"{provider.Name} trả HTTP {(int)response.StatusCode}.");
    }

    internal static Uri BuildEndpoint(
        string baseUrl,
        string relativePath,
        bool allowPrivateNetworks = false)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Base URL phải là URL HTTP hoặc HTTPS hợp lệ.");
        }

        bool isLoopback = uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        if (uri.Scheme == Uri.UriSchemeHttp && !isLoopback)
        {
            throw new ArgumentException("Provider từ xa phải dùng HTTPS. HTTP chỉ được phép cho localhost/loopback.");
        }

        if (!allowPrivateNetworks
            && (isLoopback || (IPAddress.TryParse(uri.Host, out IPAddress? address) && IsNonPublicAddress(address))))
        {
            throw new ArgumentException(
                "Base URL không được trỏ tới localhost, metadata service hoặc mạng nội bộ.");
        }

        string normalized = baseUrl.TrimEnd('/') + "/";
        return new Uri(new Uri(normalized), relativePath);
    }

    private async Task ValidateResolvedHostAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        if (_allowPrivateNetworks || IPAddress.TryParse(endpoint.Host, out _))
        {
            return;
        }

        try
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);
            if (addresses.Length == 0 || addresses.Any(IsNonPublicAddress))
            {
                throw new OpenAiClientException(
                    OpenAiClientFailureKind.Configuration,
                    "Host AI provider phân giải tới localhost, metadata service hoặc mạng nội bộ.");
            }
        }
        catch (System.Net.Sockets.SocketException exception)
        {
            throw new OpenAiClientException(
                OpenAiClientFailureKind.Unavailable,
                "Không thể phân giải host AI provider.",
                exception);
        }
    }

    private static bool IsNonPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        byte[] bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10
                || bytes[0] == 127
                || bytes[0] == 0
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || bytes[0] >= 224;
        }

        return address.IsIPv6LinkLocal
            || address.IsIPv6Multicast
            || address.IsIPv6SiteLocal
            || (bytes[0] & 0xFE) == 0xFC;
    }

    private static void AddAuthorization(HttpRequestMessage request, string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    private static OpenAiClientException InvalidResponse(
        AiProvider provider,
        Exception? innerException = null)
    {
        return new OpenAiClientException(
            OpenAiClientFailureKind.Unavailable,
            $"{provider.Name} trả response không đúng chuẩn OpenAI.",
            innerException);
    }
}
