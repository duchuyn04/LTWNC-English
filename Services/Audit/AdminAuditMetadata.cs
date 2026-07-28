using System.Text.Json;

namespace ltwnc.Services.Audit;

// Danh sách trường metadata được phép ghi vào Bản ghi kiểm toán quản trị.
// Trường ngoài danh sách bị loại bỏ; trường có tên nhạy cảm bị chặn tuyệt đối.
public static class AdminAuditMetadata
{
    public const int MaxValueLength = 200;
    public const int MaxJsonLength = 2000;

    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "method",
        "ip",
        "userAgent",
        "returnUrl",
        "path",
        "filter",
        "page",
        "pageSize",
        "count",
        "rowCount",
        "status",
        "scope",
        "exportType",
        "providerName",
        "adapterType",
        "modelId",
        "isEnabled",
        "isPrimary",
        "priority",
        "incidentType",
        "caseReference",
        "topic",
        "turnCount",
        "processedCount",
        "changedCount",
        "failedCount",
        "batchSize",
        "failureKind",
        "deniedReason"
    };

    // Mật khẩu, khóa bí mật, câu lệnh AI và hội thoại không bao giờ được ghi.
    private static readonly string[] SensitiveKeyFragments =
    [
        "password",
        "secret",
        "apikey",
        "token",
        "credential",
        "prompt",
        "conversation",
        "message"
    ];

    public static string? Serialize(IReadOnlyDictionary<string, string?>? metadata)
    {
        // 1. Kiểm tra `metadata == null || metadata.Count == 0` để chọn nhánh xử lý phù hợp.
        if (metadata == null || metadata.Count == 0)
        {
            // 2. Trả `null` cho nơi gọi.
            return null;
        }

        // 3. Khởi tạo `safe` với dữ liệu ban đầu cần thiết.
        var safe = new Dictionary<string, string>(StringComparer.Ordinal);
        // 4. Duyệt từng phần tử trong `metadata` để xử lý lần lượt.
        foreach ((string key, string? value) in metadata)
        {
            // 5. Kiểm tra `!IsKeyAllowed(key) || value == null` để chọn nhánh xử lý phù hợp.
            if (!IsKeyAllowed(key) || value == null)
            {
                // 6. Bỏ qua phần còn lại và chuyển sang lần lặp tiếp theo.
                continue;
            }

            // 7. Tính giá trị và lưu vào `trimmed` để dùng ở bước tiếp theo.
            string trimmed = value.Length > MaxValueLength
                ? value[..MaxValueLength]
                : value;
            // 8. Cập nhật `safe[key]` bằng giá trị mới.
            safe[key] = trimmed;

            // Dừng thêm trường khi sắp chạm trần kích thước.
            // 9. Kiểm tra `JsonSerializer.Serialize(safe).Length > MaxJsonLength` để chọn nhánh xử lý phù hợp.
            if (JsonSerializer.Serialize(safe).Length > MaxJsonLength)
            {
                // 10. Gọi `Remove` để thực hiện bước nghiệp vụ này.
                safe.Remove(key);
                // 11. Thoát khỏi vòng lặp hoặc nhánh xử lý hiện tại.
                break;
            }
        }

        // 12. Trả `safe.Count == 0 ? null : JsonSerializer.Serialize(safe)` cho nơi gọi.
        return safe.Count == 0 ? null : JsonSerializer.Serialize(safe);
    }

    private static bool IsKeyAllowed(string key)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(key) || !AllowedKeys.Contains(key)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(key) || !AllowedKeys.Contains(key))
        {
            // 2. Trả `false` cho nơi gọi.
            return false;
        }

        // 3. Gọi `Replace` và lưu kết quả vào `normalized`.
        string normalized = key.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        // 4. Trả `!SensitiveKeyFragments.Any(fragment => normalized.Contains(fragment...` cho nơi gọi.
        return !SensitiveKeyFragments.Any(fragment =>
            normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
