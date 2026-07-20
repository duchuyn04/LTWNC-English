using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ltwnc.Services.Profiles;

namespace ltwnc.Tests.Services.Profiles;

public class ProfileUsernameRouteConstraintTests
{
    private static bool Matches(string username, RouteDirection direction)
    {
        var constraint = new ProfileUsernameRouteConstraint();
        var values = new RouteValueDictionary { ["username"] = username };

        return constraint.Match(
            new DefaultHttpContext(),
            new MockRouter(),
            "username",
            values,
            direction);
    }

    [Theory]
    [InlineData(RouteDirection.IncomingRequest)]
    [InlineData(RouteDirection.UrlGeneration)]
    public void Match_ValidUsername_ReturnsTrue(RouteDirection direction)
    {
        Assert.True(Matches("user.name-1", direction));
    }

    [Theory]
    [InlineData("account")]
    [InlineData("SET")]
    [InlineData("invalid user")]
    public void Match_InvalidOrReservedUsername_ReturnsFalse(string username)
    {
        Assert.False(Matches(username, RouteDirection.IncomingRequest));
    }

    private sealed class MockRouter : IRouter
    {
        public VirtualPathData? GetVirtualPath(VirtualPathContext context) => null;

        public Task RouteAsync(RouteContext context) => Task.CompletedTask;
    }
}
