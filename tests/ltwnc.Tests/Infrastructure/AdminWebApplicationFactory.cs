using System.Net;
using System.Text.RegularExpressions;
using ltwnc.Data;
using ltwnc.Services.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ltwnc.Tests.Infrastructure;

public sealed class AdminWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestPassword = "Testpass1";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private IReadOnlyList<IInterceptor> _interceptors = [];

    public AdjustableTimeProvider Clock { get; } = new();

    public AdminWebApplicationFactory()
    {
        _connection.Open();
    }

    // Cho phép test chuyên biệt gắn interceptor mà không thay đổi cấu hình của các fixture hiện có.
    internal AdminWebApplicationFactory(params IInterceptor[] interceptors)
        : this()
    {
        _interceptors = interceptors;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<TimeProvider>();
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connection);
                if (_interceptors.Count > 0)
                {
                    options.AddInterceptors(_interceptors);
                }
            });
            services.AddSingleton<TimeProvider>(Clock);
            // Cookie test phải dùng wall-clock giống CookieContainer; clock cố định vẫn dành cho nghiệp vụ Admin.
            services.RemoveAll<IAuthService>();
            services.AddScoped<IAuthService>(provider => new AuthService(
                provider.GetRequiredService<AppDbContext>(),
                provider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<ltwnc.Models.Entities.AppUser>>(),
                provider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
                TimeProvider.System));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        IHost host = base.CreateHost(builder);

        using IServiceScope scope = host.Services.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.EnsureCreated();

        return host;
    }

    public async Task SeedUserAsync(
        string userName,
        string email,
        bool isAdmin = false,
        bool twoFactorEnabled = false)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<ltwnc.Models.Entities.AppUser>();

        string normalizedEmail = email.ToUpperInvariant();
        ltwnc.Models.Entities.AppUser? user = await dbContext.AppUsers
            .SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail);
        if (user == null)
        {
            user = new ltwnc.Models.Entities.AppUser
            {
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = normalizedEmail
            };
            user.PasswordHash = passwordHasher.HashPassword(user, TestPassword);
            dbContext.AppUsers.Add(user);
        }

        user.IsAdmin = isAdmin;
        await dbContext.SaveChangesAsync();

        // Tham số cũ được giữ để các test hiện có không phải đổi chữ ký helper.
        _ = twoFactorEnabled;
    }

    public static async Task SignInAsync(HttpClient client, string email)
    {
        HttpResponseMessage response = await SubmitLoginAsync(client, email);

        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException(
                $"Đăng nhập người dùng thử nghiệm thất bại với mã {(int)response.StatusCode}.");
        }
    }

    public static async Task<HttpResponseMessage> SubmitLoginAsync(
        HttpClient client,
        string email)
    {
        HttpResponseMessage loginPage = await client.GetAsync("/Account/Login");
        string html = await loginPage.Content.ReadAsStringAsync();
        System.Text.RegularExpressions.Match tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        if (!tokenMatch.Success)
        {
            throw new InvalidOperationException("Không tìm thấy mã chống giả mạo trên trang đăng nhập.");
        }

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = TestPassword,
            ["RememberMe"] = "false",
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(tokenMatch.Groups[1].Value)
        });
        return await client.PostAsync("/Account/Login", form);
    }

    public async Task SignInVerifiedAdminAsync(HttpClient client, string email)
    {
        await SignInAsync(client, email);
    }

    public async Task<bool> IsLockedOutAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ltwnc.Models.Entities.AppUser user = await FindUserByEmailAsync(dbContext, email);
        return user.LockoutEnd != null && user.LockoutEnd > Clock.GetUtcNow();
    }

    // Lấy mã AppUser theo email để test có thể gọi trang chi tiết Admin/Users.
    public async Task<string> GetUserIdAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ltwnc.Models.Entities.AppUser user = await FindUserByEmailAsync(dbContext, email);
        return user.Id;
    }

    // Lấy security stamp hiện tại để test giả lập form hợp lệ hoặc form bị cũ.
    public async Task<string> GetSecurityStampAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ltwnc.Models.Entities.AppUser user = await FindUserByEmailAsync(dbContext, email);
        return user.ConcurrencyStamp;
    }

    // Đổi security stamp để mô phỏng thu hồi phiên (cookie cũ phải bị đăng xuất).
    public async Task RotateSecurityStampAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ltwnc.Models.Entities.AppUser user = await FindUserByEmailAsync(dbContext, email);
        user.SecurityStamp = Guid.NewGuid().ToString();
        await dbContext.SaveChangesAsync();
    }

    public static async Task<HttpResponseMessage> SubmitFormAsync(
        HttpClient client,
        string pagePath,
        string postPath,
        IReadOnlyDictionary<string, string> fields)
    {
        HttpResponseMessage page = await client.GetAsync(pagePath);
        string html = await page.Content.ReadAsStringAsync();
        System.Text.RegularExpressions.Match tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!tokenMatch.Success)
        {
            throw new InvalidOperationException(
                $"Không tìm thấy mã chống giả mạo trên biểu mẫu ({(int)page.StatusCode}): {html[..Math.Min(240, html.Length)]}");
        }

        var formFields = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(tokenMatch.Groups[1].Value)
        };
        using var form = new FormUrlEncodedContent(formFields);
        return await client.PostAsync(postPath, form);
    }

    private static async Task<ltwnc.Models.Entities.AppUser> FindUserByEmailAsync(
        AppDbContext dbContext,
        string email)
    {
        string normalizedEmail = email.ToUpperInvariant();
        return await dbContext.AppUsers
            .SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail)
            ?? throw new InvalidOperationException("Không tìm thấy người dùng thử nghiệm.");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
