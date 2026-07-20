using ltwnc.Services.Profiles;

namespace ltwnc.Tests.Services.Profiles;

public class UsernamePolicyTests
{
    [Theory]
    [InlineData("user123")]
    [InlineData("nguyen.van-a")]
    [InlineData("user_name")]
    [InlineData("Ab3")]
    public void GetValidationError_UrlSafeUsername_ReturnsNull(string username)
    {
        Assert.Null(UsernamePolicy.GetValidationError(username));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("nguyễn")]
    [InlineData("user name")]
    [InlineData("user/name")]
    [InlineData("user@name")]
    [InlineData(".username")]
    [InlineData("username-")]
    public void GetValidationError_InvalidUsername_ReturnsVietnameseError(string username)
    {
        string? error = UsernamePolicy.GetValidationError(username);

        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("account")]
    [InlineData("SET")]
    [InlineData("Study")]
    [InlineData("favicon.ico")]
    [InlineData("uploads")]
    public void GetValidationError_ReservedUsername_IsCaseInsensitive(string username)
    {
        Assert.Equal(
            "Username này được dành riêng cho hệ thống.",
            UsernamePolicy.GetValidationError(username));
    }
}
