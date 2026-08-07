namespace ltwnc.Tests.Controllers;

public sealed class RoutingConfigurationTests
{
    [Fact]
    public void StaticAssetsAreMappedAfterConventionalControllerRoutes()
    {
        string program = Read("Program.cs").Replace("\r\n", "\n");

        int staticAssetsIndex = program.IndexOf("app.MapStaticAssets();", StringComparison.Ordinal);
        int defaultRouteIndex = program.IndexOf(
            "name: \"default\",\n    pattern: \"{controller=Home}/{action=Index}/{id?}\")",
            StringComparison.Ordinal);

        Assert.True(defaultRouteIndex >= 0);
        Assert.True(staticAssetsIndex > defaultRouteIndex);
        Assert.DoesNotContain(
            "pattern: \"Admin/{controller=Dashboard}/{action=Index}/{id?}\")\n    .WithStaticAssets();",
            program);
        Assert.DoesNotContain(
            "pattern: \"{controller=Home}/{action=Index}/{id?}\")\n    .WithStaticAssets();",
            program);
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
