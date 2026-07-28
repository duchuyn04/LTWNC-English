namespace ltwnc.Services.AdminUsers;

// Điều phối thao tác khóa tài khoản để hai yêu cầu trong cùng tiến trình không cùng kiểm tra bất biến Admin.
public sealed class AdminUserLockCoordinator
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Chờ đến lượt xử lý và trả lease để caller luôn giải phóng khóa bằng await using.
    public async ValueTask<IAsyncDisposable> EnterAsync(
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `WaitAsync` để thực hiện bước nghiệp vụ này.
        await _semaphore.WaitAsync(cancellationToken);
        // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new LockLease(_semaphore);
    }

    private sealed class LockLease : IAsyncDisposable
    {
        private SemaphoreSlim? _semaphore;

        // Giữ semaphore đang sở hữu để giải phóng đúng một lần.
        public LockLease(SemaphoreSlim semaphore)
        {
            // 1. Lưu dependency `_semaphore` để các phương thức khác sử dụng.
            _semaphore = semaphore;
        }

        // Giải phóng lượt xử lý và bỏ tham chiếu để lần gọi lặp không tăng semaphore quá mức.
        public ValueTask DisposeAsync()
        {
            // 1. Gọi `Exchange` và lưu kết quả vào `semaphore`.
            SemaphoreSlim? semaphore = Interlocked.Exchange(ref _semaphore, null);
            // 2. Kiểm tra `semaphore != null` để chọn nhánh xử lý phù hợp.
            if (semaphore != null)
            {
                // 3. Gọi `Release` để thực hiện bước nghiệp vụ này.
                semaphore.Release();
            }

            // 4. Trả `ValueTask.CompletedTask` cho nơi gọi.
            return ValueTask.CompletedTask;
        }
    }
}
