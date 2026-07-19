using System.Collections.Concurrent;

namespace ltwnc.Services.AdminAchievements;

// Khoa nhe trong tien trinh de chan hai tac vu dong bo thanh tich chay trung cung pham vi.
public sealed class AdminAchievementSyncCoordinator
{
    private const string SystemScopeKey = "system";
    private readonly ConcurrentDictionary<string, byte> _runningScopes = new(StringComparer.Ordinal);

    // Thu giu khoa cho mot nguoi dung; that bai neu dang co batch toan he thong hoac user do dang chay.
    public IDisposable? TryStartUser(string userId)
    {
        string key = BuildUserKey(userId);
        if (_runningScopes.ContainsKey(SystemScopeKey))
        {
            return null;
        }

        if (!_runningScopes.TryAdd(key, 0))
        {
            return null;
        }

        return new ScopeLease(_runningScopes, key);
    }

    // Thu giu khoa toan he thong; that bai neu dang co bat ky tac vu dong bo nao khac.
    public IDisposable? TryStartSystem()
    {
        if (!_runningScopes.IsEmpty)
        {
            return null;
        }

        if (!_runningScopes.TryAdd(SystemScopeKey, 0))
        {
            return null;
        }

        return new ScopeLease(_runningScopes, SystemScopeKey);
    }

    // Tao khoa rieng cho user de so sanh chinh xac, khong phu thuoc hoa-thuong.
    private static string BuildUserKey(string userId)
    {
        return $"user:{userId}";
    }

    private sealed class ScopeLease : IDisposable
    {
        private readonly ConcurrentDictionary<string, byte> _runningScopes;
        private readonly string _key;
        private bool _disposed;

        // Luu dictionary va khoa dang giu de Dispose co the giai phong dung scope.
        public ScopeLease(ConcurrentDictionary<string, byte> runningScopes, string key)
        {
            _runningScopes = runningScopes;
            _key = key;
        }

        // Giai phong khoa mot lan duy nhat, ke ca khi Dispose bi goi lap.
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _runningScopes.TryRemove(_key, out _);
            _disposed = true;
        }
    }
}
