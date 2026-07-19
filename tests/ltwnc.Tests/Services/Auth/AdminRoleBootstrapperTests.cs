using ltwnc.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;

namespace ltwnc.Tests.Services.Auth;

public sealed class AdminRoleBootstrapperTests
{
    [Fact]
    public async Task BootstrapAsync_DoesNotPromoteAUserSelectedByPublicUsername()
    {
        IdentityUser attacker = new() { Id = "attacker-id", UserName = "known-admin-name" };
        Mock<UserManager<IdentityUser>> users = MockUserManager();
        users.Setup(manager => manager.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((IdentityUser?)null);
        Mock<RoleManager<IdentityRole>> roles = MockRoleManager();
        roles.Setup(manager => manager.RoleExistsAsync("Admin")).ReturnsAsync(true);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminBootstrap:UserName"] = attacker.UserName
            })
            .Build();

        var bootstrapper = new AdminRoleBootstrapper(users.Object, roles.Object, configuration);
        await bootstrapper.BootstrapAsync(schemaIsCurrent: true);

        users.Verify(manager => manager.FindByNameAsync(It.IsAny<string>()), Times.Never);
        users.Verify(manager => manager.AddToRoleAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task BootstrapAsync_PromotesOnlyTheConfiguredImmutableUserId()
    {
        IdentityUser owner = new() { Id = "immutable-user-id", UserName = "owner" };
        Mock<UserManager<IdentityUser>> users = MockUserManager();
        users.Setup(manager => manager.FindByIdAsync(owner.Id)).ReturnsAsync(owner);
        users.Setup(manager => manager.IsInRoleAsync(owner, "Admin")).ReturnsAsync(false);
        users.Setup(manager => manager.AddToRoleAsync(owner, "Admin")).ReturnsAsync(IdentityResult.Success);
        Mock<RoleManager<IdentityRole>> roles = MockRoleManager();
        roles.Setup(manager => manager.RoleExistsAsync("Admin")).ReturnsAsync(true);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminBootstrap:UserId"] = owner.Id
            })
            .Build();

        var bootstrapper = new AdminRoleBootstrapper(users.Object, roles.Object, configuration);
        await bootstrapper.BootstrapAsync(schemaIsCurrent: true);

        users.Verify(manager => manager.AddToRoleAsync(owner, "Admin"), Times.Once);
    }

    private static Mock<UserManager<IdentityUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        return new Mock<UserManager<IdentityUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<RoleManager<IdentityRole>> MockRoleManager()
    {
        var store = new Mock<IRoleStore<IdentityRole>>();
        return new Mock<RoleManager<IdentityRole>>(
            store.Object, Array.Empty<IRoleValidator<IdentityRole>>(), null!, null!, null!);
    }
}
