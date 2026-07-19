using ltwnc.Data;
using ltwnc.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ltwnc.Tests.Infrastructure;

public sealed class AdminWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestPassword = "Testpass1";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public AdjustableTimeProvider Clock { get; } = new();

    public AdminWebApplicationFactory()
    {
        _connection.Open();
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
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            services.AddSingleton<TimeProvider>(Clock);
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
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        RoleManager<IdentityRole> roleManager =
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        IdentityUser? user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new IdentityUser { UserName = userName, Email = email };
            EnsureSucceeded(await userManager.CreateAsync(user, TestPassword));
        }

        if (isAdmin)
        {
            if (!await roleManager.RoleExistsAsync(AdminRoleBootstrapper.AdminRole))
            {
                EnsureSucceeded(await roleManager.CreateAsync(
                    new IdentityRole(AdminRoleBootstrapper.AdminRole)));
            }

            if (!await userManager.IsInRoleAsync(user, AdminRoleBootstrapper.AdminRole))
            {
                EnsureSucceeded(await userManager.AddToRoleAsync(
                    user,
                    AdminRoleBootstrapper.AdminRole));
            }
        }

        if (twoFactorEnabled && !await userManager.GetTwoFactorEnabledAsync(user))
        {
            if (string.IsNullOrWhiteSpace(await userManager.GetAuthenticatorKeyAsync(user)))
            {
                EnsureSucceeded(await userManager.ResetAuthenticatorKeyAsync(user));
            }

            EnsureSucceeded(await userManager.SetTwoFactorEnabledAsync(user, true));
        }
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

    public async Task<string> GenerateAuthenticatorCodeAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException("Không tìm thấy người dùng thử nghiệm.");

        string key = await userManager.GetAuthenticatorKeyAsync(user)
            ?? throw new InvalidOperationException("Người dùng thử nghiệm chưa có khóa authenticator.");
        return GenerateTotpCode(key, DateTimeOffset.UtcNow);
    }

    public async Task SignInVerifiedAdminAsync(HttpClient client, string email)
    {
        HttpResponseMessage loginResponse = await SubmitLoginAsync(client, email);
        if (loginResponse.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException(
                $"Đăng nhập Admin thử nghiệm thất bại với mã {(int)loginResponse.StatusCode}.");
        }

        string code = await GenerateAuthenticatorCodeAsync(email);
        HttpResponseMessage verifyResponse = await SubmitFormAsync(
            client,
            "/Account/AdminTwoFactor/Verify",
            "/Account/AdminTwoFactor/Verify",
            new Dictionary<string, string>
            {
                ["Code"] = code,
                ["ReturnUrl"] = "/Admin"
            });
        if (verifyResponse.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException(
                $"Xác minh Admin thử nghiệm thất bại với mã {(int)verifyResponse.StatusCode}.");
        }
    }

    public async Task<IReadOnlyList<string>> GenerateRecoveryCodesAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException("Không tìm thấy người dùng thử nghiệm.");
        IEnumerable<string>? recoveryCodes =
            await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return recoveryCodes?.ToArray()
            ?? throw new InvalidOperationException("Không thể tạo mã khôi phục thử nghiệm.");
    }

    public async Task<bool> IsLockedOutAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException("Không tìm thấy người dùng thử nghiệm.");
        return await userManager.IsLockedOutAsync(user);
    }

    // Lấy mã IdentityUser theo email để test có thể gọi trang chi tiết Admin/Users.
    public async Task<string> GetUserIdAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException("Không tìm thấy người dùng thử nghiệm.");
        return user.Id;
    }

    // Lấy security stamp hiện tại để test giả lập form hợp lệ hoặc form bị cũ.
    public async Task<string> GetSecurityStampAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException("Không tìm thấy người dùng thử nghiệm.");
        return user.ConcurrencyStamp
            ?? throw new InvalidOperationException("Người dùng thử nghiệm chưa có concurrency stamp.");
    }

    public async Task DisableTwoFactorAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        UserManager<IdentityUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        IdentityUser user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException("Không tìm thấy người dùng thử nghiệm.");
        EnsureSucceeded(await userManager.SetTwoFactorEnabledAsync(user, false));
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

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(
                "; ",
                result.Errors.Select(error => error.Description)));
        }
    }

    private static string GenerateTotpCode(string base32Key, DateTimeOffset now)
    {
        byte[] key = DecodeBase32(base32Key);
        long timeStep = now.ToUnixTimeSeconds() / 30;
        byte[] counter = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(timeStep));
        byte[] hash = HMACSHA1.HashData(key, counter);
        int offset = hash[^1] & 0x0F;
        int binaryCode = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);
        return (binaryCode % 1_000_000).ToString("D6");
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0;
        int bitsLeft = 0;

        foreach (char character in value.TrimEnd('=').ToUpperInvariant())
        {
            int index = alphabet.IndexOf(character);
            if (index < 0)
            {
                throw new InvalidOperationException("Khóa authenticator không đúng định dạng Base32.");
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)(buffer >> bitsLeft));
                buffer &= (1 << bitsLeft) - 1;
            }
        }

        return bytes.ToArray();
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
