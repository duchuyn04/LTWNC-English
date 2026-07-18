using ltwnc.Controllers;
using ltwnc.Models.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ltwnc.Tests.Controllers;

public class AccountControllerTests
{
    private static Mock<UserManager<IdentityUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        return new Mock<UserManager<IdentityUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<SignInManager<IdentityUser>> MockSignInManager(
        Mock<UserManager<IdentityUser>> userManager)
    {
        return new Mock<SignInManager<IdentityUser>>(
            userManager.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<IdentityUser>>().Object,
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<IdentityUser>>.Instance,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<IdentityUser>>().Object);
    }

    private static RegisterViewModel ValidRegister() => new()
    {
        Email = "a@b.com",
        Username = "user1",
        Password = "Pass1234",
        ConfirmPassword = "Pass1234"
    };

    [Fact]
    public async Task Register_CreateFails_MapsVietnameseErrorToModelState()
    {
        var userManager = MockUserManager();
        userManager.Setup(x => x.CreateAsync(It.IsAny<IdentityUser>(), "Pass1234"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail", Description = "Email taken" }));
        var signInManager = MockSignInManager(userManager);
        var controller = new AccountController(userManager.Object, signInManager.Object);

        var result = await controller.Register(ValidRegister());

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            e => e.ErrorMessage == "Email đã được sử dụng.");
        signInManager.Verify(x => x.SignInAsync(
            It.IsAny<IdentityUser>(), It.IsAny<AuthenticationProperties>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Register_CreateThrowsDbUpdateException_MapsToDuplicateEmailModelStateError()
    {
        var userManager = MockUserManager();
        userManager.Setup(x => x.CreateAsync(It.IsAny<IdentityUser>(), "Pass1234"))
            .ThrowsAsync(new DbUpdateException(
                "Unique constraint failed",
                new Exception("UNIQUE constraint failed: AspNetUsers.Email")));
        var signInManager = MockSignInManager(userManager);
        var controller = new AccountController(userManager.Object, signInManager.Object);

        var result = await controller.Register(ValidRegister());

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            e => e.ErrorMessage == "Email đã được sử dụng.");
        signInManager.Verify(x => x.SignInAsync(
            It.IsAny<IdentityUser>(), It.IsAny<AuthenticationProperties>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Register_CreateFailsWithUnknownIdentityError_ReturnsGenericVietnameseError()
    {
        var userManager = MockUserManager();
        userManager.Setup(x => x.CreateAsync(It.IsAny<IdentityUser>(), "Pass1234"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Unexpected", Description = "English fallback" }));
        var signInManager = MockSignInManager(userManager);
        var controller = new AccountController(userManager.Object, signInManager.Object);

        var result = await controller.Register(ValidRegister());

        Assert.IsType<ViewResult>(result);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            e => e.ErrorMessage == "Đăng ký không thành công. Vui lòng kiểm tra lại thông tin.");
    }

    [Fact]
    public async Task Register_Success_SignsInPersistentOneDayAndRedirectsHome()
    {
        var userManager = MockUserManager();
        userManager.Setup(x => x.CreateAsync(It.IsAny<IdentityUser>(), "Pass1234"))
            .ReturnsAsync(IdentityResult.Success);
        var signInManager = MockSignInManager(userManager);
        var controller = new AccountController(userManager.Object, signInManager.Object);

        var result = await controller.Register(ValidRegister());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        signInManager.Verify(x => x.SignInAsync(
            It.Is<IdentityUser>(u => u.UserName == "user1" && u.Email == "a@b.com"),
            It.Is<AuthenticationProperties>(p =>
                p.IsPersistent &&
                p.ExpiresUtc.HasValue &&
                p.ExpiresUtc.Value > DateTimeOffset.UtcNow.AddHours(23) &&
                p.ExpiresUtc.Value < DateTimeOffset.UtcNow.AddDays(2)),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsGenericError()
    {
        var userManager = MockUserManager();
        userManager.Setup(x => x.FindByEmailAsync("nobody@b.com"))
            .ReturnsAsync((IdentityUser?)null);
        var signInManager = MockSignInManager(userManager);
        var controller = new AccountController(userManager.Object, signInManager.Object);

        var result = await controller.Login(new LoginViewModel
        {
            Email = "nobody@b.com",
            Password = "Pass1234"
        });

        Assert.IsType<ViewResult>(result);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            e => e.ErrorMessage == "Email hoặc mật khẩu không đúng.");
        signInManager.Verify(x => x.SignInAsync(
            It.IsAny<IdentityUser>(), It.IsAny<AuthenticationProperties>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsGenericError()
    {
        var user = new IdentityUser { UserName = "user1", Email = "a@b.com" };
        var userManager = MockUserManager();
        userManager.Setup(x => x.FindByEmailAsync("a@b.com")).ReturnsAsync(user);
        var signInManager = MockSignInManager(userManager);
        signInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Sai1234", false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);
        var controller = new AccountController(userManager.Object, signInManager.Object);

        var result = await controller.Login(new LoginViewModel
        {
            Email = "a@b.com",
            Password = "Sai1234"
        });

        Assert.IsType<ViewResult>(result);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            e => e.ErrorMessage == "Email hoặc mật khẩu không đúng.");
    }

    [Fact]
    public async Task Login_SuccessRememberMe_SignsInThirtyDaysAndRedirectsSet()
    {
        var user = new IdentityUser { UserName = "user1", Email = "a@b.com" };
        var userManager = MockUserManager();
        userManager.Setup(x => x.FindByEmailAsync("a@b.com")).ReturnsAsync(user);
        var signInManager = MockSignInManager(userManager);
        signInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Pass1234", false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        var controller = new AccountController(userManager.Object, signInManager.Object);

        var result = await controller.Login(new LoginViewModel
        {
            Email = "a@b.com",
            Password = "Pass1234",
            RememberMe = true
        });

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Set", redirect.Url);
        signInManager.Verify(x => x.SignInAsync(
            user,
            It.Is<AuthenticationProperties>(p =>
                p.IsPersistent &&
                p.ExpiresUtc.HasValue &&
                p.ExpiresUtc.Value > DateTimeOffset.UtcNow.AddDays(29)),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Login_SuccessNoRememberMe_SignsInOneDay()
    {
        var user = new IdentityUser { UserName = "user1", Email = "a@b.com" };
        var userManager = MockUserManager();
        userManager.Setup(x => x.FindByEmailAsync("a@b.com")).ReturnsAsync(user);
        var signInManager = MockSignInManager(userManager);
        signInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Pass1234", false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        var controller = new AccountController(userManager.Object, signInManager.Object);

        var result = await controller.Login(new LoginViewModel
        {
            Email = "a@b.com",
            Password = "Pass1234",
            RememberMe = false
        });

        Assert.IsType<RedirectResult>(result);
        signInManager.Verify(x => x.SignInAsync(
            user,
            It.Is<AuthenticationProperties>(p =>
                p.IsPersistent &&
                p.ExpiresUtc.HasValue &&
                p.ExpiresUtc.Value > DateTimeOffset.UtcNow.AddHours(23) &&
                p.ExpiresUtc.Value < DateTimeOffset.UtcNow.AddDays(2)),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Logout_CallsSignOutAndRedirectsHome()
    {
        var userManager = MockUserManager();
        var signInManager = MockSignInManager(userManager);
        var controller = new AccountController(userManager.Object, signInManager.Object);

        var result = await controller.Logout();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        signInManager.Verify(x => x.SignOutAsync(), Times.Once);
    }
}
