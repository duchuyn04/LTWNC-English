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
        if (metadata == null || metadata.Count == 0)
        {
            return null;
        }

        var safe = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string? value) in metadata)
        {
            if (!IsKeyAllowed(key) || value == null)
            {
                continue;
            }

            string trimmed = value.Length > MaxValueLength
                ? value[..MaxValueLength]
                : value;
            safe[key] = trimmed;

            // Dừng thêm trường khi sắp chạm trần kích thước.
            if (JsonSerializer.Serialize(safe).Length > MaxJsonLength)
            {
                safe.Remove(key);
                break;
            }
        }

        return safe.Count == 0 ? null : JsonSerializer.Serialize(safe);
    }

    private static bool IsKeyAllowed(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !AllowedKeys.Contains(key))
        {
            return false;
        }

        string normalized = key.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return !SensitiveKeyFragments.Any(fragment =>
            normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
