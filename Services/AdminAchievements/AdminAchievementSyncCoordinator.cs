namespace ltwnc.Services.AdminAchievements;

// Khóa nhẹ trong tiến trình để chặn hai tác vụ đồng bộ thành tích chạy trùng cùng phạm vi.
public sealed class AdminAchievementSyncCoordinator
{
    private const string SystemScopeKey = "system";
    private readonly object _gate = new();
    private readonly HashSet<string> _runningScopes = new(StringComparer.Ordinal);

    // Thử giữ khóa cho một người dùng; thất bại nếu batch toàn hệ thống hoặc user đó đang chạy.
    public IDisposable? TryStartUser(string userId)
    {
        string key = BuildUserKey(userId);
        lock (_gate)
        {
            // Kiểm tra và thêm scope trong cùng critical section để khóa hệ thống không chen vào giữa.
            if (_runningScopes.Contains(SystemScopeKey))
            {
                return null;
            }

            if (!_runningScopes.Add(key))
            {
                return null;
            }

            return new ScopeLease(this, key);
        }
    }

    // Thử giữ khóa toàn hệ thống; thất bại nếu đang có bất kỳ tác vụ đồng bộ nào khác.
    public IDisposable? TryStartSystem()
    {
        lock (_gate)
        {
            // Kiểm tra rỗng và thêm khóa hệ thống như một thao tác nguyên tử.
            if (_runningScopes.Count > 0)
            {
                return null;
            }

            _runningScopes.Add(SystemScopeKey);
            return new ScopeLease(this, SystemScopeKey);
        }
    }

    // Tạo khóa riêng cho user để so sánh chính xác, không phụ thuộc hoa thường.
    private static string BuildUserKey(string userId)
    {
        return $"user:{userId}";
    }

    // Giải phóng scope dưới cùng một gate đã dùng khi cấp lease.
    private void Release(string key)
    {
        lock (_gate)
        {
            _runningScopes.Remove(key);
        }
    }

    private sealed class ScopeLease : IDisposable
    {
        private readonly AdminAchievementSyncCoordinator _coordinator;
        private readonly string _key;
        private int _disposed;

        // Lưu coordinator và khóa đang giữ để Dispose giải phóng đúng scope.
        public ScopeLease(AdminAchievementSyncCoordinator coordinator, string key)
        {
            _coordinator = coordinator;
            _key = key;
        }

        // Giải phóng khóa một lần duy nhất, kể cả khi Dispose bị gọi đồng thời hoặc gọi lặp.
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _coordinator.Release(_key);
        }
    }
}
