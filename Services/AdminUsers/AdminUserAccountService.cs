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
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

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
        _context = context;
        _auditService = auditService;
        _timeProvider = timeProvider;
        _lockCoordinator = lockCoordinator;
    }

    // Trả về danh sách tài khoản đã lọc, sắp xếp và phân trang phía máy chủ.
    public async Task<AdminUserAccountPage> SearchAsync(
        AdminUserAccountQuery query,
        CancellationToken cancellationToken = default)
    {
        int page = Math.Max(DefaultPage, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        IQueryable<AppUser> users = _context.AppUsers.AsNoTracking();

        // Giữ thứ tự lọc rõ ràng trên entity gốc để EF Core dịch SQL ổn định.
        users = ApplySearch(users, query.Search);
        users = ApplyStatus(users, query.Status);
        users = ApplySort(users, query.Sort);

        int totalCount = await users.CountAsync(cancellationToken);
        List<AdminUserAccountRow> items = await BuildUserRows(users)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AdminUserAccountPage(items, totalCount, page, pageSize);
    }

    // Lấy một tài khoản ở dạng chỉ đọc để trang chi tiết không có quyền sửa hồ sơ/mật khẩu/role.
    public async Task<AdminUserAccountDetails?> GetDetailsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        AppUser? user = await _context.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user == null)
        {
            return null;
        }

        UserProfile? profile = await _context.UserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        bool isLocked = IsLocked(user);

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
        string? validationError = ValidateCommand(command);
        if (validationError != null)
        {
            return AdminUserOperationResult.Failure(validationError);
        }

        // Tuần tự hóa toàn bộ quyết định khóa để hai request không cùng thấy một số đếm Admin đã cũ.
        await using IAsyncDisposable operationLock =
            await _lockCoordinator.EnterAsync(cancellationToken);
        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        AppUser? user = await _context.AppUsers.SingleOrDefaultAsync(
            item => item.Id == command.TargetUserId,
            cancellationToken);
        if (user == null)
        {
            return AdminUserOperationResult.Failure("Không tìm thấy tài khoản cần khóa.");
        }

        string? denialReason = await GetLockDenialReasonAsync(command, user, user.IsAdmin);
        if (denialReason != null)
        {
            // Quyết định bị từ chối vẫn được audit để Admin khác có thể truy vết.
            await RecordAuditAsync(command, AdminAuditActions.UsersLock, AdminAuditOutcome.Denied, user, denialReason, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AdminUserOperationResult.Failure(denialReason);
        }

        // So khớp concurrency stamp từ form để phát hiện tab cũ hoặc thao tác đồng thời.
        AdminUserOperationResult? conflict = await DetectConflictAsync(
            command,
            user,
            AdminAuditActions.UsersLock,
            cancellationToken);
        if (conflict != null)
        {
            await transaction.CommitAsync(cancellationToken);
            return conflict;
        }

        // Khóa tài khoản và đổi stamp để cookie cũ bị vô hiệu ở request kế tiếp.
        user.LockoutEnd = PermanentLockoutEnd;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        // Enqueue audit vào cùng DbContext để commit nghiệp vụ và audit đi cùng nhau.
        _auditService.Enqueue(BuildAuditEntry(
            command,
            AdminAuditActions.UsersLock,
            AdminAuditOutcome.Success,
            user,
            null));
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return AdminUserOperationResult.Success("Đã khóa tài khoản và thu hồi toàn bộ phiên đăng nhập.");
    }

    // Mở khóa tài khoản, chỉ xóa lockout và ghi audit; không sửa dữ liệu học tập.
    public async Task<AdminUserOperationResult> UnlockAsync(
        AdminUserAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        // Lý do và stamp vẫn bắt buộc vì đây là thao tác thay đổi trạng thái nhạy cảm.
        string? validationError = ValidateCommand(command);
        if (validationError != null)
        {
            return AdminUserOperationResult.Failure(validationError);
        }

        AppUser? user = await _context.AppUsers.SingleOrDefaultAsync(
            item => item.Id == command.TargetUserId,
            cancellationToken);
        if (user == null)
        {
            return AdminUserOperationResult.Failure("Không tìm thấy tài khoản cần mở khóa.");
        }

        // Nếu form mang stamp cũ, dừng lại để tránh ghi đè quyết định mới hơn.
        AdminUserOperationResult? conflict = await DetectConflictAsync(
            command,
            user,
            AdminAuditActions.UsersUnlock,
            cancellationToken);
        if (conflict != null)
        {
            return conflict;
        }

        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        // Chỉ xóa lockout, không chạm vào tiến độ học, thành tích hay nội dung của người dùng.
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        // Audit nằm trong cùng transaction với thay đổi mở khóa.
        _auditService.Enqueue(BuildAuditEntry(
            command,
            AdminAuditActions.UsersUnlock,
            AdminAuditOutcome.Success,
            user,
            null));
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return AdminUserOperationResult.Success("Đã mở khóa tài khoản.");
    }

    // Thu hồi mọi cookie hiện có bằng cách đổi security stamp, độc lập với khóa tài khoản.
    public async Task<AdminUserOperationResult> RevokeSessionsAsync(
        AdminUserAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        // Dùng cùng contract form với lock/unlock để mọi thao tác Admin đều có lý do.
        string? validationError = ValidateCommand(command);
        if (validationError != null)
        {
            return AdminUserOperationResult.Failure(validationError);
        }

        AppUser? user = await _context.AppUsers.SingleOrDefaultAsync(
            item => item.Id == command.TargetUserId,
            cancellationToken);
        if (user == null)
        {
            return AdminUserOperationResult.Failure("Không tìm thấy tài khoản cần thu hồi phiên.");
        }

        // Security stamp cũng là dữ liệu đồng thời, nên phải kiểm tra trước khi đổi.
        AdminUserOperationResult? conflict = await DetectConflictAsync(
            command,
            user,
            AdminAuditActions.UsersRevokeSessions,
            cancellationToken);
        if (conflict != null)
        {
            return conflict;
        }

        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        // Thu hồi phiên độc lập với trạng thái khóa tài khoản.
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        // Audit cùng transaction để không có trường hợp báo thành công mà thiếu dấu vết.
        _auditService.Enqueue(BuildAuditEntry(
            command,
            AdminAuditActions.UsersRevokeSessions,
            AdminAuditOutcome.Success,
            user,
            null));
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return AdminUserOperationResult.Success("Đã thu hồi toàn bộ phiên đăng nhập của tài khoản.");
    }

    // Ghép AppUser đã lọc với hồ sơ tối thiểu và trạng thái Admin/khóa để dựng hàng danh sách.
    private IQueryable<AdminUserAccountRow> BuildUserRows(
        IQueryable<AppUser> users)
    {
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
        if (string.IsNullOrWhiteSpace(search))
        {
            return users;
        }

        string term = search.Trim();
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
        string normalizedStatus = NormalizeToken(status);
        if (normalizedStatus == "locked")
        {
            return users.Where(user => user.LockoutEnd != null);
        }

        if (normalizedStatus == "unlocked")
        {
            return users.Where(user => user.LockoutEnd == null);
        }

        if (normalizedStatus == "admin")
        {
            return users.Where(user => user.IsAdmin);
        }

        return users;
    }

    // Sắp xếp server-side theo danh sách khóa cố định để tránh truyền field tùy ý vào truy vấn.
    private IQueryable<AppUser> ApplySort(
        IQueryable<AppUser> users,
        string? sort)
    {
        string normalizedSort = NormalizeToken(sort);
        if (normalizedSort == "username")
        {
            return users.OrderBy(user => user.UserName).ThenBy(user => user.Email);
        }

        if (normalizedSort == "created")
        {
            return users
                .OrderByDescending(user => _context.UserProfiles
                    .Where(profile => profile.UserId == user.Id)
                    .Select(profile => (DateTime?)profile.CreatedAt)
                    .FirstOrDefault())
                .ThenBy(user => user.Email);
        }

        if (normalizedSort == "locked")
        {
            return users.OrderByDescending(user => user.LockoutEnd != null).ThenBy(user => user.Email);
        }

        return users.OrderBy(user => user.Email).ThenBy(user => user.UserName);
    }

    // Gom các bất biến khóa Admin vào một chỗ để controller không tự quyết định bảo mật.
    private async Task<string?> GetLockDenialReasonAsync(
        AdminUserAccountCommand command,
        AppUser target,
        bool targetIsAdmin)
    {
        // Không cho tự khóa để tránh Admin tự làm mất quyền truy cập trong phiên hiện tại.
        if (string.Equals(command.ActorUserId, target.Id, StringComparison.Ordinal))
        {
            return "Quản trị viên không thể tự khóa tài khoản của mình.";
        }

        // Người học không ảnh hưởng số lượng Admin còn hoạt động.
        if (!targetIsAdmin)
        {
            return null;
        }

        // Nếu đây là Admin cuối cùng đang mở khóa, thao tác sẽ làm dashboard không còn người quản trị.
        if (!IsLocked(target))
        {
            int activeAdminCount = await CountActiveAdminsAsync();
            if (activeAdminCount <= 1)
            {
                return "Không thể khóa Quản trị viên đang hoạt động cuối cùng.";
            }
        }

        return null;
    }

    // Đếm Admin chưa bị khóa để bảo vệ bất biến "luôn còn ít nhất một Admin hoạt động".
    private async Task<int> CountActiveAdminsAsync()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        List<DateTimeOffset?> lockoutEnds = await _context.AppUsers
            .Where(user => user.IsAdmin)
            .Select(user => user.LockoutEnd)
            .ToListAsync();
        return lockoutEnds.Count(lockoutEnd => lockoutEnd == null || lockoutEnd <= now);
    }

    // So sánh concurrency stamp và ghi audit bị từ chối khi phát hiện dữ liệu cũ.
    private async Task<AdminUserOperationResult?> DetectConflictAsync(
        AdminUserAccountCommand command,
        AppUser user,
        string action,
        CancellationToken cancellationToken)
    {
        string currentStamp = user.ConcurrencyStamp;
        if (string.Equals(currentStamp, command.ConcurrencyStamp, StringComparison.Ordinal))
        {
            return null;
        }

        const string message = "Tài khoản đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trước khi thao tác.";
        await RecordAuditAsync(command, action, AdminAuditOutcome.Denied, user, message, cancellationToken);
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
        AdminAuditEntry entry = BuildAuditEntry(command, action, outcome, target, denialReason);
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
        var metadata = new Dictionary<string, string?>
        {
            ["TargetEmail"] = target.Email,
            ["TargetUserName"] = target.UserName,
            ["DeniedReason"] = denialReason
        };

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
        if (string.IsNullOrWhiteSpace(command.ActorUserId))
        {
            return "Không xác định được Quản trị viên đang thao tác.";
        }

        if (string.IsNullOrWhiteSpace(command.TargetUserId))
        {
            return "Không xác định được tài khoản cần xử lý.";
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return "Vui lòng nhập lý do trước khi thực hiện.";
        }

        if (command.Reason.Trim().Length > 500)
        {
            return "Lý do không được vượt quá 500 ký tự.";
        }

        if (string.IsNullOrWhiteSpace(command.ConcurrencyStamp))
        {
            return "Thiếu mã phiên bản tài khoản. Vui lòng tải lại trang.";
        }

        return null;
    }

    // Kiểm tra khóa theo thời gian hiện tại để test có thể điều khiển bằng TimeProvider.
    private bool IsLocked(AppUser user)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (user.LockoutEnd == null)
        {
            return false;
        }

        return user.LockoutEnd > now;
    }

    // Chuẩn hóa khóa lọc/sắp xếp từ query string.
    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToLowerInvariant();
    }

}
