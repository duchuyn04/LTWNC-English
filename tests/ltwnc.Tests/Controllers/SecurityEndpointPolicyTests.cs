using System.Reflection;
using ltwnc.Areas.Admin;
using ltwnc.Areas.Admin.Controllers;
using ltwnc.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ltwnc.Tests.Controllers;

public sealed class SecurityEndpointPolicyTests
{
    [Fact]
    public void FlashcardApi_RequiresAntiforgeryForUnsafeCookieAuthenticatedRequests()
    {
        Assert.NotEmpty(typeof(FlashcardsApiController)
            .GetCustomAttributes<AutoValidateAntiforgeryTokenAttribute>());
    }

    [Fact]
    public void ClearFilters_IsAProtectedPostInsteadOfAStateChangingGet()
    {
        MethodInfo method = typeof(StudyController).GetMethod(nameof(StudyController.ClearFilters))!;

        Assert.NotEmpty(method.GetCustomAttributes<HttpPostAttribute>());
        Assert.NotEmpty(method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>());
    }

    [Theory]
    [InlineData(typeof(AccountController), nameof(AccountController.Register), 1, "auth")]
    [InlineData(typeof(AccountController), nameof(AccountController.Login), 1, "auth")]
    [InlineData(typeof(EnglishMissionController), nameof(EnglishMissionController.Start), 1, "ai")]
    [InlineData(typeof(EnglishMissionController), nameof(EnglishMissionController.Respond), 1, "ai")]
    [InlineData(typeof(FlashcardSetController), nameof(FlashcardSetController.Import), 1, "uploads")]
    [InlineData(typeof(FlashcardSetController), nameof(FlashcardSetController.AddCard), 1, "uploads")]
    [InlineData(typeof(FlashcardSetController), nameof(FlashcardSetController.EditCard), 1, "uploads")]
    public void SensitivePostEndpoints_AreRateLimited(
        Type controllerType,
        string methodName,
        int attributeCount,
        string expectedPolicy)
    {
        MethodInfo method = controllerType.GetMethods()
            .Single(candidate => candidate.Name == methodName
                && candidate.GetCustomAttribute<HttpPostAttribute>() != null);
        EnableRateLimitingAttribute[] attributes = method
            .GetCustomAttributes<EnableRateLimitingAttribute>()
            .ToArray();

        Assert.Equal(attributeCount, attributes.Length);
        Assert.Equal(expectedPolicy, attributes[0].PolicyName);
    }

    [Fact]
    public void AiProvidersController_RequiresPrivilegedAdminSession()
    {
        AuthorizeAttribute attribute = typeof(AiProvidersController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .Single();

        Assert.Equal(AdminAreaPolicy.Name, attribute.Policy);
    }

    [Theory]
    [InlineData(nameof(AiProvidersController.Save))]
    [InlineData(nameof(AiProvidersController.Delete))]
    [InlineData(nameof(AiProvidersController.SetPrimary))]
    public void SensitiveAiProviderChanges_RequireAntiforgery(
        string methodName)
    {
        MethodInfo method = typeof(AiProvidersController).GetMethods()
            .Single(candidate => candidate.Name == methodName
                && candidate.GetCustomAttribute<HttpPostAttribute>() != null);

        Assert.NotEmpty(method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>());
    }

    [Theory]
    [InlineData(nameof(ltwnc.Areas.Admin.Controllers.AchievementsController.ResyncUser))]
    [InlineData(nameof(ltwnc.Areas.Admin.Controllers.AchievementsController.ResyncAll))]
    public void AchievementResyncChanges_RequireAntiforgery(
        string methodName)
    {
        MethodInfo method = typeof(ltwnc.Areas.Admin.Controllers.AchievementsController)
            .GetMethods()
            .Single(candidate => candidate.Name == methodName
                && candidate.GetCustomAttribute<HttpPostAttribute>() != null);

        Assert.NotEmpty(method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void UnifiedEditor_SendsTheAntiforgeryTokenOnApiWrites()
    {
        string script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "wwwroot", "js", "unified-editor.js"));

        Assert.Contains("RequestVerificationToken", script);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ltwnc.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
