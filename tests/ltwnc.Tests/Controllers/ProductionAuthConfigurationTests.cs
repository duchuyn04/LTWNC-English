using System.Reflection;
using ltwnc.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Tests.Controllers;

public sealed class ProductionAuthConfigurationTests
{
    [Fact]
    public void StatusCodePageAcceptsReexecutedPostRequests()
    {
        MethodInfo method = typeof(HomeController).GetMethod(
            nameof(HomeController.StatusCodePage))!;

        Assert.Empty(method.GetCustomAttributes<HttpGetAttribute>(inherit: true));
    }

    [Fact]
    public void ProductionDataProtectionUsesPersistentAppDataKeys()
    {
        string program = Read("Program.cs");

        Assert.Contains("DataProtection:Path", program);
        Assert.Contains("App_Data", program);
        Assert.Contains("PersistKeysToFileSystem", program);
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
