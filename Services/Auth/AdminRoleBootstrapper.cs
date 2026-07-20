using Microsoft.AspNetCore.Identity;

namespace ltwnc.Services.Auth;

public sealed class AdminRoleBootstrapper
{
    public const string AdminRole = "Admin";

    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;

    public AdminRoleBootstrapper(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
    }

    public async Task BootstrapAsync(bool schemaIsCurrent)
    {
        if (!schemaIsCurrent)
        {
            return;
        }

        if (!await _roleManager.RoleExistsAsync(AdminRole))
        {
            IdentityResult roleResult = await _roleManager.CreateAsync(new IdentityRole(AdminRole));
            EnsureSucceeded(roleResult, "Không thể tạo role Admin.");
        }

        string? configuredUserId = _configuration["AdminBootstrap:UserId"]?.Trim();
        if (string.IsNullOrWhiteSpace(configuredUserId))
        {
            return;
        }

        IdentityUser? user = await _userManager.FindByIdAsync(configuredUserId);
        if (user == null)
        {
            throw new InvalidOperationException(
                "AdminBootstrap:UserId không khớp với tài khoản hiện có. Không cấp quyền Admin.");
        }

        if (!await _userManager.IsInRoleAsync(user, AdminRole))
        {
            IdentityResult addResult = await _userManager.AddToRoleAsync(user, AdminRole);
            EnsureSucceeded(addResult, "Không thể cấp role Admin cho tài khoản bootstrap.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            string details = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"{message} {details}".Trim());
        }
    }
}
