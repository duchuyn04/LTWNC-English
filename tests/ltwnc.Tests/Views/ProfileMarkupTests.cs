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
        Assert.Contains("data-avatar-cropper", view);
        Assert.Contains("profile-avatar.js", view);
        Assert.Contains("TempData[\"Error\"]", view);
        Assert.Contains("asp-validation-summary=\"All\"", view);
        Assert.Contains("tabindex=\"0\"", view);
    }

    [Fact]
    public void AuthenticatedLayout_UsesNamedPublicProfileRouteAndKeepsEditLink()
    {
        string layout = File.ReadAllText(Path.Combine(Root, "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("asp-route=\"PublicProfile\"", layout);
        Assert.Contains("asp-route-username=\"@User.Identity?.Name\"", layout);
        Assert.Contains("/Account/Profile/Edit", layout);
        Assert.Contains("Chỉnh sửa profile", layout);
    }
}
