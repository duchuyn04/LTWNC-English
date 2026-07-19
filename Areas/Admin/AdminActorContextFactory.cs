using System.Security.Claims;
using ltwnc.Services.Audit;

namespace ltwnc.Areas.Admin;

public static class AdminActorContextFactory
{
    // Tạo danh tính quản trị viên và trace id thống nhất cho mọi lệnh cần ghi audit.
    public static AdminActorContext FromHttpContext(HttpContext httpContext)
    {
        string actorUserId =
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        string actorDisplay = httpContext.User.Identity?.Name ?? actorUserId;

        return new AdminActorContext(
            actorUserId,
            actorDisplay,
            httpContext.TraceIdentifier);
    }
}
