using System.Security.Claims;
using ltwnc.Controllers;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Profile;
using ltwnc.Services.Auth;
using ltwnc.Services.Profiles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;

namespace ltwnc.Tests.Controllers;

public class ProfileControllerTests
{
    private static ProfileController CreateController(
        Mock<IProfileService> profileService,
        Mock<ICurrentUser> currentUser,
        Mock<IAuthService>? authService = null,
        Mock<IAvatarService>? avatarService = null)
    {
        var controller = new ProfileController(
            profileService.Object,
            currentUser.Object,
            (authService ?? new Mock<IAuthService>()).Object,
            (avatarService ?? new Mock<IAvatarService>()).Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "user-1")],
                    "Test"))
            }
        };
        controller.TempData = new Mock<ITempDataDictionary>().Object;
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
    public async Task Public_NonCanonicalCasing_RedirectsPermanentlyToStoredUsername()
    {
        var profileService = new Mock<IProfileService>();
        profileService.Setup(service => service.GetPublicProfileAsync("USER1", null, default))
            .ReturnsAsync(new PublicProfileViewModel { Username = "user1" });
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.UserId).Returns((string?)null);
        ProfileController controller = CreateController(profileService, currentUser);

        RedirectToRouteResult result = Assert.IsType<RedirectToRouteResult>(
            await controller.Public("USER1", default));

        Assert.True(result.Permanent);
        Assert.Equal(ProfileController.PublicProfileRouteName, result.RouteName);
        Assert.Equal("user1", result.RouteValues!["username"]);
    }

    [Fact]
    public void LegacyPublic_ValidUsername_RedirectsPermanentlyToNamedRoute()
    {
        var profileService = new Mock<IProfileService>();
        var currentUser = new Mock<ICurrentUser>();
        ProfileController controller = CreateController(profileService, currentUser);

        RedirectToRouteResult result = Assert.IsType<RedirectToRouteResult>(
            controller.LegacyPublic("user1"));

        Assert.True(result.Permanent);
        Assert.Equal(ProfileController.PublicProfileRouteName, result.RouteName);
        Assert.Equal("user1", result.RouteValues!["username"]);
    }

    [Fact]
    public void LegacyPublic_InvalidUsername_ReturnsNotFound()
    {
        var profileService = new Mock<IProfileService>();
        var currentUser = new Mock<ICurrentUser>();
        ProfileController controller = CreateController(profileService, currentUser);

        IActionResult result = controller.LegacyPublic("account");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditPost_ServiceErrors_AreAddedToModelState()
    {
        var profileService = new Mock<IProfileService>();
        profileService.Setup(service => service.UpdateProfileAsync(
                "user-1", It.IsAny<ProfileEditViewModel>(), default))
            .ReturnsAsync(ProfileOperationResult.Failure(
                new ProfileFieldError(nameof(ProfileEditViewModel.Username), "Tên đăng nhập đã được sử dụng.")));
        profileService.Setup(service => service.GetEditModelAsync("user-1", default))
            .ReturnsAsync(new ProfileEditViewModel
            {
                Email = "learner@example.com",
                AvatarPath = "/uploads/avatars/current.png",
                AvatarInitial = "L"
            });
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.UserId).Returns("user-1");
        ProfileController controller = CreateController(profileService, currentUser);

        ViewResult result = Assert.IsType<ViewResult>(await controller.Edit(
            new ProfileEditViewModel { Username = "new-name" }, default));

        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[nameof(ProfileEditViewModel.Username)]!.Errors,
            error => error.ErrorMessage == "Tên đăng nhập đã được sử dụng.");
        Assert.Equal("Edit", result.ViewName);
        ProfileEditViewModel viewModel = Assert.IsType<ProfileEditViewModel>(result.Model);
        Assert.Equal("learner@example.com", viewModel.Email);
        Assert.Equal("/uploads/avatars/current.png", viewModel.AvatarPath);
        Assert.Equal("L", viewModel.AvatarInitial);
    }

    [Fact]
    public async Task EditPost_InvalidModelState_PreservesSidebarContext()
    {
        var profileService = new Mock<IProfileService>();
        profileService.Setup(service => service.GetEditModelAsync("user-1", default))
            .ReturnsAsync(new ProfileEditViewModel
            {
                Email = "learner@example.com",
                AvatarPath = "/uploads/avatars/current.png",
                AvatarInitial = "L"
            });
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.UserId).Returns("user-1");
        ProfileController controller = CreateController(profileService, currentUser);
        controller.ModelState.AddModelError(nameof(ProfileEditViewModel.Username), "Username không hợp lệ.");

        ViewResult result = Assert.IsType<ViewResult>(await controller.Edit(
            new ProfileEditViewModel { Username = "bad value" },
            default));

        ProfileEditViewModel viewModel = Assert.IsType<ProfileEditViewModel>(result.Model);
        Assert.Equal("learner@example.com", viewModel.Email);
        Assert.Equal("/uploads/avatars/current.png", viewModel.AvatarPath);
        profileService.Verify(service => service.UpdateProfileAsync(
            It.IsAny<string>(),
            It.IsAny<ProfileEditViewModel>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditPost_Success_RefreshesCurrentUsersCookie()
    {
        var profileService = new Mock<IProfileService>();
        profileService.Setup(service => service.UpdateProfileAsync(
                "user-1", It.IsAny<ProfileEditViewModel>(), default))
            .ReturnsAsync(ProfileOperationResult.Success());
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.UserId).Returns("user-1");
        var authService = new Mock<IAuthService>();
        var user = new AppUser { Id = "user-1", UserName = "new-name" };
        authService.Setup(service => service.FindByIdAsync("user-1", default))
            .ReturnsAsync(user);
        ProfileController controller = CreateController(profileService, currentUser, authService);

        IActionResult result = await controller.Edit(
            new ProfileEditViewModel { Username = "new-name" },
            default);

        Assert.IsType<RedirectToActionResult>(result);
        authService.Verify(service => service.RefreshSignInAsync(user), Times.Once);
    }
}
