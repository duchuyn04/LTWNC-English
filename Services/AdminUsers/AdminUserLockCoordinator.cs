namespace ltwnc.Services.AdminUsers;

// Điều phối thao tác khóa tài khoản để hai yêu cầu trong cùng tiến trình không cùng kiểm tra bất biến Admin.
public sealed class AdminUserLockCoordinator
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Chờ đến lượt xử lý và trả lease để caller luôn giải phóng khóa bằng await using.
    public async ValueTask<IAsyncDisposable> EnterAsync(
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new LockLease(_semaphore);
    }

    private sealed class LockLease : IAsyncDisposable
    {
        private SemaphoreSlim? _semaphore;

        // Giữ semaphore đang sở hữu để giải phóng đúng một lần.
        public LockLease(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        // Giải phóng lượt xử lý và bỏ tham chiếu để lần gọi lặp không tăng semaphore quá mức.
        public ValueTask DisposeAsync()
        {
            SemaphoreSlim? semaphore = Interlocked.Exchange(ref _semaphore, null);
            if (semaphore != null)
            {
                semaphore.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
