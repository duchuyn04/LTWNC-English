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
        // 1. Lưu dependency `_httpClientFactory` để các phương thức khác sử dụng.
        _httpClientFactory = httpClientFactory;
        // 2. Lưu dependency `_allowPrivateNetworks` để các phương thức khác sử dụng.
        _allowPrivateNetworks = configuration.GetValue<bool>("AiProviders:AllowPrivateNetworks");
    }

    // Gọi endpoint models và trả danh sách mã mô hình duy nhất theo thứ tự ổn định.
    public async Task<IReadOnlyList<string>> GetModelsAsync(
        AiProvider provider,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `BuildEndpoint` và lưu kết quả vào `endpoint`.
        Uri endpoint = BuildEndpoint(provider.BaseUrl, "models", _allowPrivateNetworks);
        // 2. Gọi `ValidateResolvedHostAsync` để thực hiện bước nghiệp vụ này.
        await ValidateResolvedHostAsync(endpoint, cancellationToken);
        // 3. Khởi tạo `request` với dữ liệu ban đầu cần thiết.
        using HttpRequestMessage request = new(HttpMethod.Get, endpoint);
        // 4. Gọi `AddAuthorization` để thực hiện bước nghiệp vụ này.
        AddAuthorization(request, apiKey);
        // 5. Gọi `SendAsync` và lưu kết quả vào `response`.
        using HttpResponseMessage response = await SendAsync(provider, request, cancellationToken);
        // 6. Gọi `EnsureSuccessAsync` để thực hiện bước nghiệp vụ này.
        await EnsureSuccessAsync(provider, response, cancellationToken);

        // 7. Gọi `Parse` và lưu kết quả vào `document`.
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        // 8. Kiểm tra `!document.RootElement.TryGetProperty("data", out JsonElement data) ...` để chọn nhánh xử lý phù hợp.
        if (!document.RootElement.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
        {
            // 9. Dừng xử lý và phát sinh lỗi `new AiProviderUnavailableException($"{provider.Name} trả danh sách ...`.
            throw new AiProviderUnavailableException($"{provider.Name} trả danh sách model không hợp lệ.");
        }

        // 10. Khởi tạo `modelIds` với dữ liệu ban đầu cần thiết.
        var modelIds = new List<string>();
        // 11. Duyệt từng `item` trong `data.EnumerateArray()` để xử lý lần lượt.
        foreach (JsonElement item in data.EnumerateArray())
        {
            // 12. Kiểm tra `!item.TryGetProperty("id", out JsonElement id)` để chọn nhánh xử lý phù hợp.
            if (!item.TryGetProperty("id", out JsonElement id))
            {
                // 13. Bỏ qua phần còn lại và chuyển sang lần lặp tiếp theo.
                continue;
            }

            // 14. Gọi `GetString` và lưu kết quả vào `modelId`.
            string? modelId = id.GetString();
            // 15. Kiểm tra `!string.IsNullOrWhiteSpace(modelId)` để chọn nhánh xử lý phù hợp.
            if (!string.IsNullOrWhiteSpace(modelId))
            {
                // 16. Gọi `Add` để thực hiện bước nghiệp vụ này.
                modelIds.Add(modelId);
            }
        }

        // 17. Trả kết quả từ `ToList` cho nơi gọi.
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
        // 1. Tính giá trị và lưu vào `body` để dùng ở bước tiếp theo.
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

        // 2. Gọi `BuildEndpoint` và lưu kết quả vào `endpoint`.
        Uri endpoint = BuildEndpoint(provider.BaseUrl, "chat/completions", _allowPrivateNetworks);
        // 3. Gọi `ValidateResolvedHostAsync` để thực hiện bước nghiệp vụ này.
        await ValidateResolvedHostAsync(endpoint, cancellationToken);
        // 4. Khởi tạo `request` với dữ liệu ban đầu cần thiết.
        using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(body)
        };
        // 5. Gọi `AddAuthorization` để thực hiện bước nghiệp vụ này.
        AddAuthorization(request, apiKey);
        // 6. Gọi `SendAsync` và lưu kết quả vào `response`.
        using HttpResponseMessage response = await SendAsync(provider, request, cancellationToken);
        // 7. Gọi `EnsureSuccessAsync` để thực hiện bước nghiệp vụ này.
        await EnsureSuccessAsync(provider, response, cancellationToken);

        // 8. Gọi `Parse` và lưu kết quả vào `document`.
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        // 9. Kiểm tra `!document.RootElement.TryGetProperty("choices", out JsonElement cho...` để chọn nhánh xử lý phù hợp.
        if (!document.RootElement.TryGetProperty("choices", out JsonElement choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0
            || !choices[0].TryGetProperty("message", out JsonElement message)
            || !message.TryGetProperty("content", out JsonElement contentElement)
            || contentElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(contentElement.GetString()))
        {
            // 10. Dừng xử lý và phát sinh lỗi `new AiProviderUnavailableException($"{provider.Name} trả response k...`.
            throw new AiProviderUnavailableException($"{provider.Name} trả response không đúng chuẩn OpenAI.");
        }

        // 11. Trả `contentElement.GetString()!` cho nơi gọi.
        return contentElement.GetString()!;
    }

    // Gửi request với timeout riêng của nhà cung cấp và chuyển lỗi mạng sang lỗi miền thống nhất.
    private async Task<HttpResponseMessage> SendAsync(
        AiProvider provider,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `CreateClient` và lưu kết quả vào `client`.
        HttpClient client = _httpClientFactory.CreateClient("AiProvider");
        // 2. Gọi `CreateLinkedTokenSource` và lưu kết quả vào `timeout`.
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // 3. Gọi `CancelAfter` để thực hiện bước nghiệp vụ này.
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(provider.TimeoutSeconds, 5, 300)));
        // 4. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 5. Trả kết quả từ `SendAsync` cho nơi gọi.
            return await client.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // 6. Dừng xử lý và phát sinh lỗi `new AiProviderUnavailableException($"{provider.Name} đã hết thời gi...`.
            throw new AiProviderUnavailableException($"{provider.Name} đã hết thời gian chờ.");
        }
        catch (HttpRequestException exception)
        {
            // 7. Dừng xử lý và phát sinh lỗi `new AiProviderUnavailableException($"Không thể kết nối {provider.Na...`.
            throw new AiProviderUnavailableException($"Không thể kết nối {provider.Name}: {exception.Message}");
        }
    }

    // Phân loại phản hồi HTTP lỗi thành lỗi cấu hình hoặc lỗi tạm thời để router xử lý đúng.
    private static async Task EnsureSuccessAsync(
        AiProvider provider,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra `response.IsSuccessStatusCode` để chọn nhánh xử lý phù hợp.
        if (response.IsSuccessStatusCode) return;

        // 2. Gọi `ReadAsStringAsync` và lưu kết quả vào `body`.
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        // 3. Tính giá trị và lưu vào `summary` để dùng ở bước tiếp theo.
        string summary = body;
        // 4. Kiểm tra `body.Length > 300` để chọn nhánh xử lý phù hợp.
        if (body.Length > 300)
        {
            // 5. Cập nhật `summary` bằng giá trị mới.
            summary = body[..300];
        }

        // 6. Kiểm tra `response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode....` để chọn nhánh xử lý phù hợp.
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // 7. Dừng xử lý và phát sinh lỗi `new AiProviderConfigurationException( $"{provider.Name} trả HTTP {(...`.
            throw new AiProviderConfigurationException(
                $"{provider.Name} trả HTTP {(int)response.StatusCode}: {summary}");
        }

        // 8. Dừng xử lý và phát sinh lỗi `new AiProviderUnavailableException( $"{provider.Name} trả HTTP {(in...`.
        throw new AiProviderUnavailableException(
            $"{provider.Name} trả HTTP {(int)response.StatusCode}: {summary}");
    }

    // Chuẩn hóa endpoint và chặn HTTP từ xa hoặc địa chỉ mạng riêng khi chính sách không cho phép.
    internal static Uri BuildEndpoint(string baseUrl, string relativePath, bool allowPrivateNetworks = false)
    {
        // 1. Kiểm tra `!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri) || (uri.Sch...` để chọn nhánh xử lý phù hợp.
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            // 2. Dừng xử lý và phát sinh lỗi `new ArgumentException("Base URL phải là URL HTTP hoặc HTTPS hợp lệ.")`.
            throw new ArgumentException("Base URL phải là URL HTTP hoặc HTTPS hợp lệ.");
        }

        // 3. Tính giá trị và lưu vào `isLoopback` để dùng ở bước tiếp theo.
        bool isLoopback = uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        // 4. Kiểm tra `uri.Scheme == Uri.UriSchemeHttp && !isLoopback` để chọn nhánh xử lý phù hợp.
        if (uri.Scheme == Uri.UriSchemeHttp && !isLoopback)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new ArgumentException("Provider từ xa phải dùng HTTPS. HTTP chỉ đượ...`.
            throw new ArgumentException("Provider từ xa phải dùng HTTPS. HTTP chỉ được phép cho localhost/loopback.");
        }

        // 6. Kiểm tra `!allowPrivateNetworks && (isLoopback || (IPAddress.TryParse(uri.Hos...` để chọn nhánh xử lý phù hợp.
        if (!allowPrivateNetworks
            && (isLoopback || (IPAddress.TryParse(uri.Host, out IPAddress? address) && IsNonPublicAddress(address))))
        {
            // 7. Dừng xử lý và phát sinh lỗi `new ArgumentException("Base URL không được trỏ tới localhost, metad...`.
            throw new ArgumentException("Base URL không được trỏ tới localhost, metadata service hoặc mạng nội bộ.");
        }

        // 8. Tính giá trị và lưu vào `normalized` để dùng ở bước tiếp theo.
        string normalized = baseUrl.TrimEnd('/') + "/";
        // 9. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new Uri(new Uri(normalized), relativePath);
    }

    // Kiểm tra toàn bộ địa chỉ DNS đã phân giải để không thể đi vòng qua bộ lọc host bằng DNS rebinding.
    private async Task ValidateResolvedHostAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra `_allowPrivateNetworks || IPAddress.TryParse(endpoint.Host, out _)` để chọn nhánh xử lý phù hợp.
        if (_allowPrivateNetworks || IPAddress.TryParse(endpoint.Host, out _))
        {
            // 2. Kết thúc phương thức sau khi hoàn tất xử lý.
            return;
        }

        // 3. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 4. Gọi `GetHostAddressesAsync` và lưu kết quả vào `addresses`.
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);
            // 5. Kiểm tra `addresses.Length == 0 || addresses.Any(IsNonPublicAddress)` để chọn nhánh xử lý phù hợp.
            if (addresses.Length == 0 || addresses.Any(IsNonPublicAddress))
            {
                // 6. Dừng xử lý và phát sinh lỗi `new AiProviderConfigurationException( "Host AI provider phân giải t...`.
                throw new AiProviderConfigurationException(
                    "Host AI provider phân giải tới localhost, metadata service hoặc mạng nội bộ.");
            }
        }
        catch (System.Net.Sockets.SocketException exception)
        {
            // 7. Dừng xử lý và phát sinh lỗi `new AiProviderUnavailableException($"Không thể phân giải host AI pr...`.
            throw new AiProviderUnavailableException($"Không thể phân giải host AI provider: {exception.Message}");
        }
    }

    // Nhận diện loopback, link-local, multicast và các dải mạng riêng IPv4/IPv6.
    private static bool IsNonPublicAddress(IPAddress address)
    {
        // 1. Kiểm tra `IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || a...` để chọn nhánh xử lý phù hợp.
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            // 2. Trả `true` cho nơi gọi.
            return true;
        }

        // 3. Kiểm tra `address.IsIPv4MappedToIPv6` để chọn nhánh xử lý phù hợp.
        if (address.IsIPv4MappedToIPv6)
        {
            // 4. Cập nhật `address` bằng giá trị mới.
            address = address.MapToIPv4();
        }

        // 5. Gọi `GetAddressBytes` và lưu kết quả vào `bytes`.
        byte[] bytes = address.GetAddressBytes();
        // 6. Kiểm tra `address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork` để chọn nhánh xử lý phù hợp.
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // 7. Trả `bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 0 || (bytes[0] == ...` cho nơi gọi.
            return bytes[0] == 10
                || bytes[0] == 127
                || bytes[0] == 0
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || bytes[0] >= 224;
        }

        // 8. Trả `address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv...` cho nơi gọi.
        return address.IsIPv6LinkLocal
            || address.IsIPv6Multicast
            || address.IsIPv6SiteLocal
            || (bytes[0] & 0xFE) == 0xFC;
    }

    // Chỉ thêm Bearer header khi có khóa để nhà cung cấp local không nhận header rỗng.
    private static void AddAuthorization(HttpRequestMessage request, string? apiKey)
    {
        // 1. Kiểm tra `!string.IsNullOrWhiteSpace(apiKey)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            // 2. Cập nhật `request.Headers.Authorization` bằng giá trị mới.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }
}
