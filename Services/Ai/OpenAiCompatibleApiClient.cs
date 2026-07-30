using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

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

    internal void ValidateConfiguration(OpenAiClientConfiguration configuration)
    {
        _ = CreateEndpoint(configuration, "chat/completions");
    }

    internal async Task<OpenAiChatResponse> CompleteAsync(
        OpenAiClientConfiguration configuration,
        string? apiKey,
        OpenAiChatRequest completion,
        CancellationToken cancellationToken)
    {
        Uri endpoint = CreateEndpoint(configuration, "chat/completions");
        await ValidateResolvedHostAsync(endpoint, cancellationToken);
        using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(completion)
        };
        AddAuthorization(request, apiKey);
        using HttpResponseMessage response = await SendAsync(configuration, request, cancellationToken);
        EnsureSuccess(configuration, response);

        try
        {
            OpenAiChatResponse? result = JsonSerializer.Deserialize<OpenAiChatResponse>(
                await response.Content.ReadAsStringAsync(cancellationToken));
            return result ?? throw InvalidResponse(configuration);
        }
        catch (JsonException exception)
        {
            throw InvalidResponse(configuration, exception);
        }
    }

    private Uri CreateEndpoint(OpenAiClientConfiguration configuration, string relativePath)
    {
        try
        {
            return BuildEndpoint(configuration.BaseUrl, relativePath, _allowPrivateNetworks);
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
        OpenAiClientConfiguration configuration,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpClient client = _httpClientFactory.CreateClient("AiProvider");
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(configuration.TimeoutSeconds, 5, 300)));

        try
        {
            return await client.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OpenAiClientException(
                OpenAiClientFailureKind.Unavailable,
                $"{configuration.Name} đã hết thời gian chờ.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new OpenAiClientException(
                OpenAiClientFailureKind.Unavailable,
                $"Không thể kết nối {configuration.Name}.",
                exception);
        }
    }

    private static void EnsureSuccess(OpenAiClientConfiguration configuration, HttpResponseMessage response)
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
            $"{configuration.Name} trả HTTP {(int)response.StatusCode}.");
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
        OpenAiClientConfiguration configuration,
        Exception? innerException = null)
    {
        return new OpenAiClientException(
            OpenAiClientFailureKind.Unavailable,
            $"{configuration.Name} trả response không đúng chuẩn OpenAI.",
            innerException);
    }
}
