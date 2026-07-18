using System.Security.Claims;
using ltwnc.Controllers;
using ltwnc.Models.ViewModels.Profile;
using ltwnc.Services.Auth;
using ltwnc.Services.Profiles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ltwnc.Tests.Controllers;

public class ProfileControllerTests
{
    private static Mock<SignInManager<IdentityUser>> MockSignInManager()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        var userManager = new Mock<UserManager<IdentityUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        return new Mock<SignInManager<IdentityUser>>(
            userManager.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<IdentityUser>>().Object,
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<IdentityUser>>.Instance,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<IdentityUser>>().Object);
    }

    private static ProfileController CreateController(
        Mock<IProfileService> profileService,
        Mock<ICurrentUser> currentUser)
    {
        var controller = new ProfileController(
            profileService.Object,
            currentUser.Object,
            MockSignInManager().Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "user-1")],
                    "Test"))
            }
        };
        return controller;
    }

    [Fact]
    public async Task Public_UnknownUsername_ReturnsNotFound()
    {
        var profileService = new Mock<IProfileService>();
        profileService.Setup(service => service.GetPublicProfileAsync("missing", null, default))
            .ReturnsAsync((PublicProfileViewModel?)null);
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.UserId).Returns((string?)null);
        ProfileController controller = CreateController(profileService, currentUser);

        IActionResult result = await controller.Public("missing", default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Public_PrivateProfile_ReturnsPrivateView()
    {
        var profileService = new Mock<IProfileService>();
        profileService.Setup(service => service.GetPublicProfileAsync("private", null, default))
            .ReturnsAsync(new PublicProfileViewModel { Username = "private", IsPrivate = true });
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.UserId).Returns((string?)null);
        ProfileController controller = CreateController(profileService, currentUser);

        ViewResult result = Assert.IsType<ViewResult>(await controller.Public("private", default));

        Assert.Equal("Private", result.ViewName);
    }

    [Fact]
    public async Task EditPost_ServiceErrors_AreAddedToModelState()
    {
        var profileService = new Mock<IProfileService>();
        profileService.Setup(service => service.UpdateProfileAsync(
                "user-1", It.IsAny<ProfileEditViewModel>(), default))
            .ReturnsAsync(ProfileOperationResult.Failure(
                new ProfileFieldError(nameof(ProfileEditViewModel.Username), "Tên đăng nhập đã được sử dụng.")));
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.UserId).Returns("user-1");
        ProfileController controller = CreateController(profileService, currentUser);

        ViewResult result = Assert.IsType<ViewResult>(await controller.Edit(
            new ProfileEditViewModel { Username = "new-name" }, default));

        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[nameof(ProfileEditViewModel.Username)]!.Errors,
            error => error.ErrorMessage == "Tên đăng nhập đã được sử dụng.");
        Assert.Equal("Edit", result.ViewName);
    }
}
