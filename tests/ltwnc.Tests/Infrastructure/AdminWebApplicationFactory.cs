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
using System.Text.RegularExpressions;

namespace ltwnc.Tests.Infrastructure;

public sealed class AdminWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestPassword = "Testpass1";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

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
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
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

    public async Task SeedUserAsync(string userName, string email, bool isAdmin = false)
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
    }

    public static async Task SignInAsync(HttpClient client, string email)
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
        HttpResponseMessage response = await client.PostAsync("/Account/Login", form);

        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException(
                $"Đăng nhập người dùng thử nghiệm thất bại với mã {(int)response.StatusCode}.");
        }
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
