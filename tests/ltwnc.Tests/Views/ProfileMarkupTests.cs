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

        Assert.Contains("ChangePassword", view);
        Assert.Contains("AntiForgeryToken", view);
        Assert.Contains("multipart/form-data", view);
        Assert.Contains("data-avatar-cropper", view);
        Assert.Contains("profile-avatar.js", view);
        Assert.Contains("TempData[\"Error\"]", view);
        Assert.Contains("asp-validation-summary=\"All\"", view);
        Assert.Contains("tabindex=\"0\"", view);
        Assert.Contains("data-loading-form", view);
        Assert.Contains("data-password-toggle", view);
        Assert.Contains("autocomplete=\"current-password\"", view);
        Assert.Contains("autocomplete=\"new-password\"", view);
        Assert.Contains("settings-back-link", view);
    }

    [Fact]
    public void PublicProfileView_RendersHelpfulEmptyStates()
    {
        string view = File.ReadAllText(Path.Combine(Root, "Views", "Profile", "Public.cshtml"));

        Assert.Contains("Chưa có hoạt động học tập nào để hiển thị.", view);
        Assert.Contains("Chưa mở khóa huy hiệu nào.", view);
        Assert.Contains("Chưa có bộ thẻ công khai nào.", view);
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
