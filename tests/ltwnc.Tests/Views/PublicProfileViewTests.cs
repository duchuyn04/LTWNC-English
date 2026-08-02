namespace ltwnc.Tests.Views;

public sealed class PublicProfileViewTests
{
    [Fact]
    public void PublicProfileClosesPublicSetsAndSideColumn()
    {
        string view = Read("Views/Profile/Public.cshtml")
            .Replace("\r\n", "\n");

        Assert.Contains("            }\n        </aside>\n    </div>\n</div>", view);
    }

    private static string Read(string relativePath)
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(root, relativePath));
    }

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
