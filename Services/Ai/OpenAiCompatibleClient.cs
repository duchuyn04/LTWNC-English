using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ltwnc.Models.Entities;

namespace ltwnc.Services.Ai;

public sealed class OpenAiCompatibleClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly bool _allowPrivateNetworks;

    // Nhận HTTP client factory và đọc chính sách cho phép mạng riêng từ cấu hình ứng dụng.
    public OpenAiCompatibleClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _allowPrivateNetworks = configuration.GetValue<bool>("AiProviders:AllowPrivateNetworks");
    }

    // Gọi endpoint models và trả danh sách mã mô hình duy nhất theo thứ tự ổn định.
    public async Task<IReadOnlyList<string>> GetModelsAsync(
        AiProvider provider,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        Uri endpoint = BuildEndpoint(provider.BaseUrl, "models", _allowPrivateNetworks);
        await ValidateResolvedHostAsync(endpoint, cancellationToken);
        using HttpRequestMessage request = new(HttpMethod.Get, endpoint);
        AddAuthorization(request, apiKey);
        using HttpResponseMessage response = await SendAsync(provider, request, cancellationToken);
        await EnsureSuccessAsync(provider, response, cancellationToken);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
        {
            throw new AiProviderUnavailableException($"{provider.Name} trả danh sách model không hợp lệ.");
        }

        var modelIds = new List<string>();
        foreach (JsonElement item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out JsonElement id))
            {
                continue;
            }

            string? modelId = id.GetString();
            if (!string.IsNullOrWhiteSpace(modelId))
            {
                modelIds.Add(modelId);
            }
        }

        return modelIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    // Gửi yêu cầu hội thoại theo contract tương thích OpenAI và lấy nội dung trả lời đầu tiên.
    public async Task<string> CompleteAsync(
        AiProvider provider,
        string? apiKey,
        AiCompletionRequest completion,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            model = provider.ModelId,
            messages = new[]
            {
                new { role = "system", content = completion.SystemPrompt },
                new { role = "user", content = completion.UserPrompt }
            },
            max_tokens = completion.MaxTokens,
            temperature = 0.3
        };

        Uri endpoint = BuildEndpoint(provider.BaseUrl, "chat/completions", _allowPrivateNetworks);
        await ValidateResolvedHostAsync(endpoint, cancellationToken);
        using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(body)
        };
        AddAuthorization(request, apiKey);
        using HttpResponseMessage response = await SendAsync(provider, request, cancellationToken);
        await EnsureSuccessAsync(provider, response, cancellationToken);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("choices", out JsonElement choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0
            || !choices[0].TryGetProperty("message", out JsonElement message)
            || !message.TryGetProperty("content", out JsonElement contentElement)
            || contentElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(contentElement.GetString()))
        {
            throw new AiProviderUnavailableException($"{provider.Name} trả response không đúng chuẩn OpenAI.");
        }

        return contentElement.GetString()!;
    }

    // Gửi request với timeout riêng của nhà cung cấp và chuyển lỗi mạng sang lỗi miền thống nhất.
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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderUnavailableException($"{provider.Name} đã hết thời gian chờ.");
        }
        catch (HttpRequestException exception)
        {
            throw new AiProviderUnavailableException($"Không thể kết nối {provider.Name}: {exception.Message}");
        }
    }

    // Phân loại phản hồi HTTP lỗi thành lỗi cấu hình hoặc lỗi tạm thời để router xử lý đúng.
    private static async Task EnsureSuccessAsync(
        AiProvider provider,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        string summary = body;
        if (body.Length > 300)
        {
            summary = body[..300];
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new AiProviderConfigurationException(
                $"{provider.Name} trả HTTP {(int)response.StatusCode}: {summary}");
        }

        throw new AiProviderUnavailableException(
            $"{provider.Name} trả HTTP {(int)response.StatusCode}: {summary}");
    }

    // Chuẩn hóa endpoint và chặn HTTP từ xa hoặc địa chỉ mạng riêng khi chính sách không cho phép.
    internal static Uri BuildEndpoint(string baseUrl, string relativePath, bool allowPrivateNetworks = false)
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
            throw new ArgumentException("Base URL không được trỏ tới localhost, metadata service hoặc mạng nội bộ.");
        }

        string normalized = baseUrl.TrimEnd('/') + "/";
        return new Uri(new Uri(normalized), relativePath);
    }

    // Kiểm tra toàn bộ địa chỉ DNS đã phân giải để không thể đi vòng qua bộ lọc host bằng DNS rebinding.
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
                throw new AiProviderConfigurationException(
                    "Host AI provider phân giải tới localhost, metadata service hoặc mạng nội bộ.");
            }
        }
        catch (System.Net.Sockets.SocketException exception)
        {
            throw new AiProviderUnavailableException($"Không thể phân giải host AI provider: {exception.Message}");
        }
    }

    // Nhận diện loopback, link-local, multicast và các dải mạng riêng IPv4/IPv6.
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

    // Chỉ thêm Bearer header khi có khóa để nhà cung cấp local không nhận header rỗng.
    private static void AddAuthorization(HttpRequestMessage request, string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }
}
