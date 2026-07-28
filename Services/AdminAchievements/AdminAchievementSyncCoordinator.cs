namespace ltwnc.Services.AdminAchievements;

// Khóa nhẹ trong tiến trình để chặn hai tác vụ đồng bộ thành tích chạy trùng cùng phạm vi.
public sealed class AdminAchievementSyncCoordinator
{
    // Khóa hệ thống đại diện cho tác vụ quét toàn bộ người dùng.
    private const string SystemScopeKey = "system";

    // Gate bảo vệ tập scope đang chạy khi nhiều request truy cập đồng thời.
    private readonly object _gate = new();
    private readonly HashSet<string> _runningScopes = new(StringComparer.Ordinal);

    // Thử giữ khóa cho một người dùng; thất bại nếu batch toàn hệ thống hoặc user đó đang chạy.
    public IDisposable? TryStartUser(string userId)
    {
        // 1. Gọi `BuildUserKey` và lưu kết quả vào `key`.
        string key = BuildUserKey(userId);
        // 2. Khóa vùng dữ liệu dùng chung trước khi đọc hoặc cập nhật.
        lock (_gate)
        {
            // Kiểm tra và thêm scope trong cùng critical section để khóa hệ thống không chen vào giữa.
            // 3. Kiểm tra `_runningScopes.Contains(SystemScopeKey)` để chọn nhánh xử lý phù hợp.
            if (_runningScopes.Contains(SystemScopeKey))
            {
                // 4. Trả `null` cho nơi gọi.
                return null;
            }

            // 5. Kiểm tra `!_runningScopes.Add(key)` để chọn nhánh xử lý phù hợp.
            if (!_runningScopes.Add(key))
            {
                // 6. Trả `null` cho nơi gọi.
                return null;
            }

            // 7. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new ScopeLease(this, key);
        }
    }

    // Thử giữ khóa toàn hệ thống; thất bại nếu đang có bất kỳ tác vụ đồng bộ nào khác.
    public IDisposable? TryStartSystem()
    {
        // 1. Khóa vùng dữ liệu dùng chung trước khi đọc hoặc cập nhật.
        lock (_gate)
        {
            // Kiểm tra rỗng và thêm khóa hệ thống như một thao tác nguyên tử.
            // 2. Kiểm tra `_runningScopes.Count > 0` để chọn nhánh xử lý phù hợp.
            if (_runningScopes.Count > 0)
            {
                // 3. Trả `null` cho nơi gọi.
                return null;
            }

            // 4. Gọi `Add` để thực hiện bước nghiệp vụ này.
            _runningScopes.Add(SystemScopeKey);
            // 5. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new ScopeLease(this, SystemScopeKey);
        }
    }

    // Tạo khóa riêng cho user để so sánh chính xác, không phụ thuộc hoa thường.
    private static string BuildUserKey(string userId)
    {
        // 1. Trả `$"user:{userId}"` cho nơi gọi.
        return $"user:{userId}";
    }

    // Giải phóng scope dưới cùng một gate đã dùng khi cấp lease.
    private void Release(string key)
    {
        // 1. Khóa vùng dữ liệu dùng chung trước khi đọc hoặc cập nhật.
        lock (_gate)
        {
            // 2. Gọi `Remove` để thực hiện bước nghiệp vụ này.
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
            // 1. Lưu dependency `_coordinator` để các phương thức khác sử dụng.
            _coordinator = coordinator;
            // 2. Lưu dependency `_key` để các phương thức khác sử dụng.
            _key = key;
        }

        // Giải phóng khóa một lần duy nhất, kể cả khi Dispose bị gọi đồng thời hoặc gọi lặp.
        public void Dispose()
        {
            // 1. Kiểm tra `Interlocked.Exchange(ref _disposed, 1) != 0` để chọn nhánh xử lý phù hợp.
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                // 2. Kết thúc phương thức sau khi hoàn tất xử lý.
                return;
            }

            // 3. Gọi `Release` để thực hiện bước nghiệp vụ này.
            _coordinator.Release(_key);
        }
    }
}
