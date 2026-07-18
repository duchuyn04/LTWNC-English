namespace ltwnc.Tests.Views;

public class ProfileMarkupTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void PublicProfileView_DoesNotRenderEmailAndHasOwnerEditLink()
    {
        string view = File.ReadAllText(Path.Combine(Root, "Views", "Profile", "Public.cshtml"));

        Assert.Contains("/Account/Profile/Edit", view);
        Assert.DoesNotContain("Model.Email", view);
        Assert.Contains("Model.Timeline", view);
    }

    [Fact]
    public void EditProfileView_HasSeparateAntiForgeryForms()
    {
        string view = File.ReadAllText(Path.Combine(Root, "Views", "Profile", "Edit.cshtml"));

        Assert.Contains("ChangeEmail", view);
        Assert.Contains("ChangePassword", view);
        Assert.Contains("AntiForgeryToken", view);
        Assert.Contains("multipart/form-data", view);
    }
}
