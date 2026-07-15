using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ltwnc.Services.Auth;

// Đọc claims cookie hiện tại (scoped).
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated == true;

    public string? UserId =>
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName =>
        Principal?.FindFirstValue(ClaimTypes.Name);
}
