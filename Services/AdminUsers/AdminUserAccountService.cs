using System.Data;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using ltwnc.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ltwnc.Services.AdminUsers;

public sealed class AdminUserAccountService : IAdminUserAccountService
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;

    private static readonly DateTimeOffset PermanentLockoutEnd =
        new(new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc));

    private readonly AppDbContext _context;
    private readonly IAdminAuditService _auditService;
    private readonly TimeProvider _timeProvider;
    private readonly AdminUserLockCoordinator _lockCoordinator;

    // Nhận database, audit, clock và coordinator để thao tác tài khoản an toàn.
    public AdminUserAccountService(
        AppDbContext context,
        IAdminAuditService auditService,
        TimeProvider timeProvider,
        AdminUserLockCoordinator lockCoordinator)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_auditService` để các phương thức khác sử dụng.
        _auditService = auditService;
        // 3. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
        // 4. Lưu dependency `_lockCoordinator` để các phương thức khác sử dụng.
        _lockCoordinator = lockCoordinator;
    }

    // Trả về danh sách tài khoản đã lọc, sắp xếp và phân trang phía máy chủ.
    public async Task<AdminUserAccountPage> SearchAsync(
        AdminUserAccountQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `Max` và lưu kết quả vào `page`.
        int page = Math.Max(DefaultPage, query.Page);
        // 2. Gọi `Clamp` và lưu kết quả vào `pageSize`.
        int pageSize = DefaultPageSize;
        // 3. Gọi `AsNoTracking` và lưu kết quả vào `users`.
        IQueryable<AppUser> users = _context.AppUsers.AsNoTracking();

        // Giữ thứ tự lọc rõ ràng trên entity gốc để EF Core dịch SQL ổn định.
        // 4. Cập nhật `users` bằng giá trị mới.
        users = ApplySearch(users, query.Search);
        // 5. Cập nhật `users` bằng giá trị mới.
        users = ApplyStatus(users, query.Status);
        // 6. Cập nhật `users` bằng giá trị mới.
        users = users.OrderBy(user => user.Email).ThenBy(user => user.UserName);

        // 7. Gọi `CountAsync` và lưu kết quả vào `totalCount`.
        int totalCount = await users.CountAsync(cancellationToken);
        // 8. Gọi `ToListAsync` và lưu kết quả vào `items`.
        List<AdminUserAccountRow> items = await BuildUserRows(users)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 9. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminUserAccountPage(items, totalCount, page, pageSize);
    }

    // Lấy một tài khoản ở dạng chỉ đọc để trang chi tiết không có quyền sửa hồ sơ/mật khẩu/role.
    public async Task<AdminUserAccountDetails?> GetDetailsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `user`.
        AppUser? user = await _context.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        // 2. Kiểm tra `user == null` để chọn nhánh xử lý phù hợp.
        if (user == null)
        {
            // 3. Trả `null` cho nơi gọi.
            return null;
        }

        // 4. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `profile`.
        UserProfile? profile = await _context.UserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        // 5. Gọi `IsLocked` và lưu kết quả vào `isLocked`.
        bool isLocked = IsLocked(user);

        // 6. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminUserAccountDetails(
            Id: user.Id,
            UserName: user.UserName,
            Email: user.Email,
            IsAdmin: user.IsAdmin,
            IsLocked: isLocked,
            AccessFailedCount: user.AccessFailedCount,
            ConcurrencyStamp: user.ConcurrencyStamp,
            CreatedAtUtc: profile?.CreatedAt,
            UpdatedAtUtc: profile?.UpdatedAt,
            LockoutEnd: user.LockoutEnd);
    }

    // Khóa tài khoản, kiểm tra bất biến Admin và ghi audit cùng transaction.
    public async Task<AdminUserOperationResult> LockAsync(
        AdminUserAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        // Chặn sớm dữ liệu form thiếu để không ghi thay đổi nửa vời.
        // 1. Gọi `ValidateCommand` và lưu kết quả vào `validationError`.
        string? validationError = ValidateCommand(command);
        // 2. Kiểm tra `validationError != null` để chọn nhánh xử lý phù hợp.
        if (validationError != null)
        {
            // 3. Trả kết quả từ `Failure` cho nơi gọi.
            return AdminUserOperationResult.Failure(validationError);
        }

        // Tuần tự hóa toàn bộ quyết định khóa để hai request không cùng thấy một số đếm Admin đã cũ.
        // 4. Gọi `EnterAsync` và lưu kết quả vào `operationLock`.
        await using IAsyncDisposable operationLock =
            await _lockCoordinator.EnterAsync(cancellationToken);
        // 5. Gọi `BeginTransactionAsync` và lưu kết quả vào `transaction`.
        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // 6. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `user`.
        AppUser? user = await _context.AppUsers.SingleOrDefaultAsync(
            item => item.Id == command.TargetUserId,
            cancellationToken);
        // 7. Kiểm tra `user == null` để chọn nhánh xử lý phù hợp.
        if (user == null)
        {
            // 8. Trả kết quả từ `Failure` cho nơi gọi.
            return AdminUserOperationResult.Failure("Không tìm thấy tài khoản cần khóa.");
        }

        // 9. Gọi `GetLockDenialReasonAsync` và lưu kết quả vào `denialReason`.
        string? denialReason = await GetLockDenialReasonAsync(command, user, user.IsAdmin);
        // 10. Kiểm tra `denialReason != null` để chọn nhánh xử lý phù hợp.
        if (denialReason != null)
        {
            // Quyết định bị từ chối vẫn được audit để Admin khác có thể truy vết.
            // 11. Gọi `RecordAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordAuditAsync(command, AdminAuditActions.UsersLock, AdminAuditOutcome.Denied, user, denialReason, cancellationToken);
            // 12. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
            await transaction.CommitAsync(cancellationToken);
            // 13. Trả kết quả từ `Failure` cho nơi gọi.
            return AdminUserOperationResult.Failure(denialReason);
        }

        // So khớp concurrency stamp từ form để phát hiện tab cũ hoặc thao tác đồng thời.
        // 14. Gọi `DetectConflictAsync` và lưu kết quả vào `conflict`.
        AdminUserOperationResult? conflict = await DetectConflictAsync(
            command,
            user,
            AdminAuditActions.UsersLock,
            cancellationToken);
        // 15. Kiểm tra `conflict != null` để chọn nhánh xử lý phù hợp.
        if (conflict != null)
        {
            // 16. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
            await transaction.CommitAsync(cancellationToken);
            // 17. Trả `conflict` cho nơi gọi.
            return conflict;
        }

        // Khóa tài khoản và đổi stamp để cookie cũ bị vô hiệu ở request kế tiếp.
        // 18. Cập nhật `user.LockoutEnd` bằng giá trị mới.
        user.LockoutEnd = PermanentLockoutEnd;
        // 19. Cập nhật `user.SecurityStamp` bằng giá trị mới.
        user.SecurityStamp = Guid.NewGuid().ToString();
        // 20. Cập nhật `user.ConcurrencyStamp` bằng giá trị mới.
        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        // Enqueue audit vào cùng DbContext để commit nghiệp vụ và audit đi cùng nhau.
        // 21. Gọi `Enqueue` để thực hiện bước nghiệp vụ này.
        _auditService.Enqueue(BuildAuditEntry(
            command,
            AdminAuditActions.UsersLock,
            AdminAuditOutcome.Success,
            user,
            null));
        // 22. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync(cancellationToken);
        // 23. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
        await transaction.CommitAsync(cancellationToken);

        // 24. Trả kết quả từ `Success` cho nơi gọi.
        return AdminUserOperationResult.Success("Đã khóa tài khoản và thu hồi toàn bộ phiên đăng nhập.");
    }

    // Mở khóa tài khoản, chỉ xóa lockout và ghi audit; không sửa dữ liệu học tập.
    public async Task<AdminUserOperationResult> UnlockAsync(
        AdminUserAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        // Lý do và stamp vẫn bắt buộc vì đây là thao tác thay đổi trạng thái nhạy cảm.
        // 1. Gọi `ValidateCommand` và lưu kết quả vào `validationError`.
        string? validationError = ValidateCommand(command);
        // 2. Kiểm tra `validationError != null` để chọn nhánh xử lý phù hợp.
        if (validationError != null)
        {
            // 3. Trả kết quả từ `Failure` cho nơi gọi.
            return AdminUserOperationResult.Failure(validationError);
        }

        // 4. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `user`.
        AppUser? user = await _context.AppUsers.SingleOrDefaultAsync(
            item => item.Id == command.TargetUserId,
            cancellationToken);
        // 5. Kiểm tra `user == null` để chọn nhánh xử lý phù hợp.
        if (user == null)
        {
            // 6. Trả kết quả từ `Failure` cho nơi gọi.
            return AdminUserOperationResult.Failure("Không tìm thấy tài khoản cần mở khóa.");
        }

        // Nếu form mang stamp cũ, dừng lại để tránh ghi đè quyết định mới hơn.
        // 7. Gọi `DetectConflictAsync` và lưu kết quả vào `conflict`.
        AdminUserOperationResult? conflict = await DetectConflictAsync(
            command,
            user,
            AdminAuditActions.UsersUnlock,
            cancellationToken);
        // 8. Kiểm tra `conflict != null` để chọn nhánh xử lý phù hợp.
        if (conflict != null)
        {
            // 9. Trả `conflict` cho nơi gọi.
            return conflict;
        }

        // 10. Gọi `BeginTransactionAsync` và lưu kết quả vào `transaction`.
        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        // Chỉ xóa lockout, không chạm vào tiến độ học, thành tích hay nội dung của người dùng.
        // 11. Cập nhật `user.LockoutEnd` bằng giá trị mới.
        user.LockoutEnd = null;
        // 12. Cập nhật `user.AccessFailedCount` bằng giá trị mới.
        user.AccessFailedCount = 0;
        // 13. Cập nhật `user.ConcurrencyStamp` bằng giá trị mới.
        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        // Audit nằm trong cùng transaction với thay đổi mở khóa.
        // 14. Gọi `Enqueue` để thực hiện bước nghiệp vụ này.
        _auditService.Enqueue(BuildAuditEntry(
            command,
            AdminAuditActions.UsersUnlock,
            AdminAuditOutcome.Success,
            user,
            null));
        // 15. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync(cancellationToken);
        // 16. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
        await transaction.CommitAsync(cancellationToken);

        // 17. Trả kết quả từ `Success` cho nơi gọi.
        return AdminUserOperationResult.Success("Đã mở khóa tài khoản.");
    }

    // Thu hồi mọi cookie hiện có bằng cách đổi security stamp, độc lập với khóa tài khoản.
    public async Task<AdminUserOperationResult> RevokeSessionsAsync(
        AdminUserAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        // Dùng cùng contract form với lock/unlock để mọi thao tác Admin đều có lý do.
        // 1. Gọi `ValidateCommand` và lưu kết quả vào `validationError`.
        string? validationError = ValidateCommand(command);
        // 2. Kiểm tra `validationError != null` để chọn nhánh xử lý phù hợp.
        if (validationError != null)
        {
            // 3. Trả kết quả từ `Failure` cho nơi gọi.
            return AdminUserOperationResult.Failure(validationError);
        }

        // 4. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `user`.
        AppUser? user = await _context.AppUsers.SingleOrDefaultAsync(
            item => item.Id == command.TargetUserId,
            cancellationToken);
        // 5. Kiểm tra `user == null` để chọn nhánh xử lý phù hợp.
        if (user == null)
        {
            // 6. Trả kết quả từ `Failure` cho nơi gọi.
            return AdminUserOperationResult.Failure("Không tìm thấy tài khoản cần thu hồi phiên.");
        }

        // Security stamp cũng là dữ liệu đồng thời, nên phải kiểm tra trước khi đổi.
        // 7. Gọi `DetectConflictAsync` và lưu kết quả vào `conflict`.
        AdminUserOperationResult? conflict = await DetectConflictAsync(
            command,
            user,
            AdminAuditActions.UsersRevokeSessions,
            cancellationToken);
        // 8. Kiểm tra `conflict != null` để chọn nhánh xử lý phù hợp.
        if (conflict != null)
        {
            // 9. Trả `conflict` cho nơi gọi.
            return conflict;
        }

        // 10. Gọi `BeginTransactionAsync` và lưu kết quả vào `transaction`.
        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        // Thu hồi phiên độc lập với trạng thái khóa tài khoản.
        // 11. Cập nhật `user.SecurityStamp` bằng giá trị mới.
        user.SecurityStamp = Guid.NewGuid().ToString();
        // 12. Cập nhật `user.ConcurrencyStamp` bằng giá trị mới.
        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        // Audit cùng transaction để không có trường hợp báo thành công mà thiếu dấu vết.
        // 13. Gọi `Enqueue` để thực hiện bước nghiệp vụ này.
        _auditService.Enqueue(BuildAuditEntry(
            command,
            AdminAuditActions.UsersRevokeSessions,
            AdminAuditOutcome.Success,
            user,
            null));
        // 14. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync(cancellationToken);
        // 15. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
        await transaction.CommitAsync(cancellationToken);

        // 16. Trả kết quả từ `Success` cho nơi gọi.
        return AdminUserOperationResult.Success("Đã thu hồi toàn bộ phiên đăng nhập của tài khoản.");
    }

    // Ghép AppUser đã lọc với hồ sơ tối thiểu và trạng thái Admin/khóa để dựng hàng danh sách.
    private IQueryable<AdminUserAccountRow> BuildUserRows(
        IQueryable<AppUser> users)
    {
        // 1. Trả `from user in users let createdAtUtc = _context.UserProfiles .Where(...` cho nơi gọi.
        return from user in users
               let createdAtUtc = _context.UserProfiles
                   .Where(profile => profile.UserId == user.Id)
                   .Select(profile => (DateTime?)profile.CreatedAt)
                   .FirstOrDefault()
               select new AdminUserAccountRow(
                   user.Id,
                   user.UserName,
                   user.Email,
                   user.IsAdmin,
                   user.LockoutEnd != null,
                   createdAtUtc,
                   user.LockoutEnd);
    }

    // Áp dụng tìm kiếm an toàn trên email, username và mã định danh trước khi projection.
    private static IQueryable<AppUser> ApplySearch(
        IQueryable<AppUser> users,
        string? search)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(search)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(search))
        {
            // 2. Trả `users` cho nơi gọi.
            return users;
        }

        // 3. Gọi `Trim` và lưu kết quả vào `term`.
        string term = search.Trim();
        // 4. Trả kết quả từ `Where` cho nơi gọi.
        return users.Where(user =>
            user.Email.Contains(term)
            || user.UserName.Contains(term)
            || user.Id.Contains(term));
    }

    // Lọc theo các trạng thái được UI hỗ trợ, giá trị lạ được xem như "tất cả".
    private static IQueryable<AppUser> ApplyStatus(
        IQueryable<AppUser> users,
        string? status)
    {
        // 1. Gọi `NormalizeToken` và lưu kết quả vào `normalizedStatus`.
        string normalizedStatus = NormalizeToken(status);
        // 2. Kiểm tra `normalizedStatus == "locked"` để chọn nhánh xử lý phù hợp.
        if (normalizedStatus == "locked")
        {
            // 3. Trả kết quả từ `Where` cho nơi gọi.
            return users.Where(user => user.LockoutEnd != null);
        }

        // 4. Kiểm tra `normalizedStatus == "unlocked"` để chọn nhánh xử lý phù hợp.
        if (normalizedStatus == "unlocked")
        {
            // 5. Trả kết quả từ `Where` cho nơi gọi.
            return users.Where(user => user.LockoutEnd == null);
        }

        // 6. Kiểm tra `normalizedStatus == "admin"` để chọn nhánh xử lý phù hợp.
        if (normalizedStatus == "admin")
        {
            // 7. Trả kết quả từ `Where` cho nơi gọi.
            return users.Where(user => user.IsAdmin);
        }

        // 8. Trả `users` cho nơi gọi.
        return users;
    }

    // Sắp xếp server-side theo danh sách khóa cố định để tránh truyền field tùy ý vào truy vấn.
    // Gom các bất biến khóa Admin vào một chỗ để controller không tự quyết định bảo mật.
    private async Task<string?> GetLockDenialReasonAsync(
        AdminUserAccountCommand command,
        AppUser target,
        bool targetIsAdmin)
    {
        // Không cho tự khóa để tránh Admin tự làm mất quyền truy cập trong phiên hiện tại.
        // 1. Kiểm tra `string.Equals(command.ActorUserId, target.Id, StringComparison.Ordi...` để chọn nhánh xử lý phù hợp.
        if (string.Equals(command.ActorUserId, target.Id, StringComparison.Ordinal))
        {
            // 2. Trả `"Quản trị viên không thể tự khóa tài khoản của mình."` cho nơi gọi.
            return "Quản trị viên không thể tự khóa tài khoản của mình.";
        }

        // Người học không ảnh hưởng số lượng Admin còn hoạt động.
        // 3. Kiểm tra `!targetIsAdmin` để chọn nhánh xử lý phù hợp.
        if (!targetIsAdmin)
        {
            // 4. Trả `null` cho nơi gọi.
            return null;
        }

        // Nếu đây là Admin cuối cùng đang mở khóa, thao tác sẽ làm dashboard không còn người quản trị.
        // 5. Kiểm tra `!IsLocked(target)` để chọn nhánh xử lý phù hợp.
        if (!IsLocked(target))
        {
            // 6. Gọi `CountActiveAdminsAsync` và lưu kết quả vào `activeAdminCount`.
            int activeAdminCount = await CountActiveAdminsAsync();
            // 7. Kiểm tra `activeAdminCount <= 1` để chọn nhánh xử lý phù hợp.
            if (activeAdminCount <= 1)
            {
                // 8. Trả `"Không thể khóa Quản trị viên đang hoạt động cuối cùng."` cho nơi gọi.
                return "Không thể khóa Quản trị viên đang hoạt động cuối cùng.";
            }
        }

        // 9. Trả `null` cho nơi gọi.
        return null;
    }

    // Đếm Admin chưa bị khóa để bảo vệ bất biến "luôn còn ít nhất một Admin hoạt động".
    private async Task<int> CountActiveAdminsAsync()
    {
        // 1. Gọi `GetUtcNow` và lưu kết quả vào `now`.
        DateTimeOffset now = _timeProvider.GetUtcNow();
        // 2. Gọi `ToListAsync` và lưu kết quả vào `lockoutEnds`.
        List<DateTimeOffset?> lockoutEnds = await _context.AppUsers
            .Where(user => user.IsAdmin)
            .Select(user => user.LockoutEnd)
            .ToListAsync();
        // 3. Trả kết quả từ `Count` cho nơi gọi.
        return lockoutEnds.Count(lockoutEnd => lockoutEnd == null || lockoutEnd <= now);
    }

    // So sánh concurrency stamp và ghi audit bị từ chối khi phát hiện dữ liệu cũ.
    private async Task<AdminUserOperationResult?> DetectConflictAsync(
        AdminUserAccountCommand command,
        AppUser user,
        string action,
        CancellationToken cancellationToken)
    {
        // 1. Tính giá trị và lưu vào `currentStamp` để dùng ở bước tiếp theo.
        string currentStamp = user.ConcurrencyStamp;
        // 2. Kiểm tra `string.Equals(currentStamp, command.ConcurrencyStamp, StringCompari...` để chọn nhánh xử lý phù hợp.
        if (string.Equals(currentStamp, command.ConcurrencyStamp, StringComparison.Ordinal))
        {
            // 3. Trả `null` cho nơi gọi.
            return null;
        }

        // 4. Tính giá trị và lưu vào `message` để dùng ở bước tiếp theo.
        const string message = "Tài khoản đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trước khi thao tác.";
        // 5. Gọi `RecordAuditAsync` để thực hiện bước nghiệp vụ này.
        await RecordAuditAsync(command, action, AdminAuditOutcome.Denied, user, message, cancellationToken);
        // 6. Trả kết quả từ `Failure` cho nơi gọi.
        return AdminUserOperationResult.Failure(message);
    }

    // Ghi audit độc lập cho các nhánh bị từ chối không có transaction nghiệp vụ.
    private async Task RecordAuditAsync(
        AdminUserAccountCommand command,
        string action,
        string outcome,
        AppUser target,
        string? denialReason,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `BuildAuditEntry` và lưu kết quả vào `entry`.
        AdminAuditEntry entry = BuildAuditEntry(command, action, outcome, target, denialReason);
        // 2. Gọi `RecordAsync` để thực hiện bước nghiệp vụ này.
        await _auditService.RecordAsync(entry, cancellationToken);
    }

    // Dựng payload audit đã lọc, không chứa mật khẩu, token hoặc ghi chú nội bộ nhạy cảm.
    private AdminAuditEntry BuildAuditEntry(
        AdminUserAccountCommand command,
        string action,
        string outcome,
        AppUser target,
        string? denialReason)
    {
        // 1. Khởi tạo `metadata` với dữ liệu ban đầu cần thiết.
        var metadata = new Dictionary<string, string?>
        {
            ["TargetEmail"] = target.Email,
            ["TargetUserName"] = target.UserName,
            ["DeniedReason"] = denialReason
        };

        // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminAuditEntry(
            ActorUserId: command.ActorUserId,
            ActorDisplay: command.ActorDisplay,
            Action: action,
            Outcome: outcome,
            TargetType: "AppUser",
            TargetId: target.Id,
            Reason: command.Reason,
            CorrelationId: command.CorrelationId,
            Metadata: metadata);
    }

    // Kiểm tra dữ liệu lệnh trước khi ghi database.
    private string? ValidateCommand(AdminUserAccountCommand command)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(command.ActorUserId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(command.ActorUserId))
        {
            // 2. Trả `"Không xác định được Quản trị viên đang thao tác."` cho nơi gọi.
            return "Không xác định được Quản trị viên đang thao tác.";
        }

        // 3. Kiểm tra `string.IsNullOrWhiteSpace(command.TargetUserId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(command.TargetUserId))
        {
            // 4. Trả `"Không xác định được tài khoản cần xử lý."` cho nơi gọi.
            return "Không xác định được tài khoản cần xử lý.";
        }

        // 5. Kiểm tra `string.IsNullOrWhiteSpace(command.Reason)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            // 6. Trả `"Vui lòng nhập lý do trước khi thực hiện."` cho nơi gọi.
            return "Vui lòng nhập lý do trước khi thực hiện.";
        }

        // 7. Kiểm tra `command.Reason.Trim().Length > 500` để chọn nhánh xử lý phù hợp.
        if (command.Reason.Trim().Length > 500)
        {
            // 8. Trả `"Lý do không được vượt quá 500 ký tự."` cho nơi gọi.
            return "Lý do không được vượt quá 500 ký tự.";
        }

        // 9. Kiểm tra `string.IsNullOrWhiteSpace(command.ConcurrencyStamp)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(command.ConcurrencyStamp))
        {
            // 10. Trả `"Thiếu mã phiên bản tài khoản. Vui lòng tải lại trang."` cho nơi gọi.
            return "Thiếu mã phiên bản tài khoản. Vui lòng tải lại trang.";
        }

        // 11. Trả `null` cho nơi gọi.
        return null;
    }

    // Kiểm tra khóa theo thời gian hiện tại để test có thể điều khiển bằng TimeProvider.
    private bool IsLocked(AppUser user)
    {
        // 1. Gọi `GetUtcNow` và lưu kết quả vào `now`.
        DateTimeOffset now = _timeProvider.GetUtcNow();
        // 2. Kiểm tra `user.LockoutEnd == null` để chọn nhánh xử lý phù hợp.
        if (user.LockoutEnd == null)
        {
            // 3. Trả `false` cho nơi gọi.
            return false;
        }

        // 4. Trả `user.LockoutEnd > now` cho nơi gọi.
        return user.LockoutEnd > now;
    }

    // Chuẩn hóa khóa lọc/sắp xếp từ query string.
    private static string NormalizeToken(string? value)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(value)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(value))
        {
            // 2. Trả `string.Empty` cho nơi gọi.
            return string.Empty;
        }

        // 3. Trả kết quả từ `ToLowerInvariant` cho nơi gọi.
        return value.Trim().ToLowerInvariant();
    }

}
