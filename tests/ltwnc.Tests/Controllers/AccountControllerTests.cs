using ltwnc.Controllers;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Account;
using ltwnc.Services.Audit;
using ltwnc.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ltwnc.Tests.Controllers;

public class AccountControllerTests
{
    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<IAdminAuditService> _auditService = new();

    private AccountController CreateController()
    {
        return new AccountController(_authService.Object, _auditService.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static AppUser CreateUser(bool isAdmin = false) => new()
    {
        Email = "a@example.com",
        NormalizedEmail = "A@EXAMPLE.COM",
        UserName = "alice",
        NormalizedUserName = "ALICE",
        IsAdmin = isAdmin
    };

    [Fact]
    public async Task RegisterPost_InvalidModelState_ReturnsView()
    {
        AccountController controller = CreateController();
        controller.ModelState.AddModelError("Email", "required");

        IActionResult result = await controller.Register(new RegisterViewModel());

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task RegisterPost_AuthFailure_MapsErrorsToModelState()
    {
        _authService
            .Setup(service => service.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(AuthResult.Failure(new AuthError("DuplicateEmail", "Email đã được sử dụng.")));
        AccountController controller = CreateController();
        var model = new RegisterViewModel
        {
            Email = "a@example.com",
            Username = "alice",
            Password = "Password1"
        };

        IActionResult result = await controller.Register(model);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Contains(
            view.ViewData.ModelState.Values.SelectMany(value => value.Errors),
            error => error.ErrorMessage == "Email đã được sử dụng.");
    }

    [Fact]
    public async Task RegisterPost_Success_SignsInAndRedirectsHome()
    {
        AppUser user = CreateUser();
        _authService
            .Setup(service => service.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(AuthResult.Success());
        _authService
            .Setup(service => service.FindByEmailAsync("a@example.com", default))
            .ReturnsAsync(user);
        AccountController controller = CreateController();
        var model = new RegisterViewModel
        {
            Email = "a@example.com",
            Username = "alice",
            Password = "Password1"
        };

        IActionResult result = await controller.Register(model);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        _authService.Verify(service => service.SignInAsync(user, TimeSpan.FromDays(1)), Times.Once);
    }

    [Fact]
    public async Task LoginPost_UnknownEmail_ReturnsGenericError()
    {
        _authService
            .Setup(service => service.FindByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync((AppUser?)null);
        AccountController controller = CreateController();
        var model = new LoginViewModel { Email = "a@example.com", Password = "Password1" };

        IActionResult result = await controller.Login(model);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Contains(
            view.ViewData.ModelState.Values.SelectMany(value => value.Errors),
            error => error.ErrorMessage == "Email hoặc mật khẩu không đúng.");
    }

    [Fact]
    public async Task LoginPost_LockedOut_ShowsLockedMessage()
    {
        AppUser user = CreateUser();
        _authService
            .Setup(service => service.FindByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(user);
        _authService
            .Setup(service => service.ValidateLoginAsync(user, It.IsAny<string>(), default))
            .ReturnsAsync(AuthResult.LockedOut());
        AccountController controller = CreateController();
        var model = new LoginViewModel { Email = "a@example.com", Password = "Password1" };

        IActionResult result = await controller.Login(model);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Contains(
            view.ViewData.ModelState.Values.SelectMany(value => value.Errors),
            error => error.ErrorMessage.Contains("không thể đăng nhập"));
    }

    [Fact]
    public async Task LoginPost_AdminUser_RedirectsAdminAndAudits()
    {
        AppUser user = CreateUser(isAdmin: true);
        _authService
            .Setup(service => service.FindByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(user);
        _authService
            .Setup(service => service.ValidateLoginAsync(user, It.IsAny<string>(), default))
            .ReturnsAsync(AuthResult.Success());
        AccountController controller = CreateController();
        var model = new LoginViewModel { Email = "a@example.com", Password = "Password1" };

        IActionResult result = await controller.Login(model);

        RedirectResult redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Admin", redirect.Url);
        _auditService.Verify(service => service.RecordAsync(
            It.Is<AdminAuditEntry>(entry => entry.Action == AdminAuditActions.AdminAreaSignIn),
            default), Times.Once);
    }

    [Fact]
    public async Task LoginPost_RegularUser_RedirectsSet()
    {
        AppUser user = CreateUser();
        _authService
            .Setup(service => service.FindByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(user);
        _authService
            .Setup(service => service.ValidateLoginAsync(user, It.IsAny<string>(), default))
            .ReturnsAsync(AuthResult.Success());
        AccountController controller = CreateController();
        var model = new LoginViewModel { Email = "a@example.com", Password = "Password1" };

        IActionResult result = await controller.Login(model);

        RedirectResult redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Set", redirect.Url);
    }

    [Fact]
    public async Task Logout_SignsOutAndRedirectsHome()
    {
        AccountController controller = CreateController();

        IActionResult result = await controller.Logout();

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        _authService.Verify(service => service.SignOutAsync(), Times.Once);
    }
}
