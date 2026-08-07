namespace ltwnc.Tests.Views;

public sealed class AuthViewTests
{
    [Fact]
    public void AuthViewsUseNeutralCopy()
    {
        string login = Read("Views/Account/Login.cshtml");
        string register = Read("Views/Account/Register.cshtml");
        string layout = Read("Views/Shared/_AuthLayout.cshtml");

        Assert.Contains("<p class=\"auth-eyebrow\">Tài khoản</p>", login);
        Assert.Contains("<h1 id=\"login-title\" class=\"auth-title\">Đăng nhập</h1>", login);
        Assert.Contains("Nhập thông tin tài khoản để tiếp tục.", login);
        Assert.Contains("Đăng nhập bằng Google", login);
        Assert.Contains("Hoặc đăng nhập bằng tài khoản", login);
        Assert.Contains("Duy trì đăng nhập trên thiết bị này", login);
        Assert.Contains("<button type=\"submit\" class=\"auth-submit\">Đăng nhập</button>", login);
        Assert.DoesNotContain("Đăng nhập <span aria-hidden=\"true\">→</span>", login);
        Assert.Contains("Chưa có tài khoản? <a href=\"/Account/Register\">Đăng ký</a>", login);
        Assert.DoesNotContain("Sẵn sàng học tiếp?", login);
        Assert.DoesNotContain("auth-progress", login);

        Assert.Contains("<p class=\"auth-eyebrow\">Tài khoản mới</p>", register);
        Assert.Contains("<h1 id=\"register-title\" class=\"auth-title\">Tạo tài khoản</h1>", register);
        Assert.Contains("Điền thông tin bên dưới. Mã xác thực sẽ được gửi đến email của bạn.", register);
        Assert.Contains("Hoặc đăng ký bằng email", register);
        Assert.DoesNotContain("Tạo góc học tập.", register);
        Assert.DoesNotContain("auth-progress", register);

        Assert.Contains("<span>Từ vựng hôm nay</span>", layout);
        Assert.Contains("<span>Xem nghĩa</span>", layout);
        Assert.Contains("Lưu bộ thẻ và theo dõi tiến độ học tập.", layout);
        Assert.DoesNotContain("Word of the day", layout);
        Assert.DoesNotContain("Tap to reveal", layout);
    }

    [Fact]
    public void AuthStylesSeparatePrimaryAndGoogleActions()
    {
        string css = Read("wwwroot/css/auth.css").Replace("\r\n", "\n");

        Assert.DoesNotContain(".auth-progress", css);
        Assert.Contains("""
.auth-google-submit {
    border-color: var(--auth-line);
    color: var(--auth-ink);
    background: #fff;
    text-decoration: none;
}
""", css);
        Assert.Contains("""
.auth-google-submit:hover {
    border-color: var(--auth-ink);
    color: var(--auth-ink);
    background: var(--sage-soft);
}
""", css);
        Assert.Contains(".auth-divider::before, .auth-divider::after", css);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(startPath);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ltwnc.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
