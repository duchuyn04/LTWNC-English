using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ltwnc.Tests.Data;

public class AppDbContextTests
{
    [Fact]
    public void AppUser_email_and_username_indexes_are_unique()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new AppDbContext(options);

        IEntityType entityType = db.Model.FindEntityType(typeof(AppUser))!;
        Assert.NotNull(entityType);

        IIndex emailIndex = Assert.Single(entityType.GetIndexes(), index =>
            index.Properties.Count == 1 &&
            index.Properties[0].Name == nameof(AppUser.NormalizedEmail));
        Assert.Equal("AppUserEmailIndex", emailIndex.GetDatabaseName());
        Assert.True(emailIndex.IsUnique);

        IIndex userNameIndex = Assert.Single(entityType.GetIndexes(), index =>
            index.Properties.Count == 1 &&
            index.Properties[0].Name == nameof(AppUser.NormalizedUserName));
        Assert.Equal("AppUserNameIndex", userNameIndex.GetDatabaseName());
        Assert.True(userNameIndex.IsUnique);
    }

    [Fact]
    public void UserProfile_UsesUserIdAsPrimaryKeyAndAppUserForeignKey()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new AppDbContext(options);

        IEntityType entity = db.Model.FindEntityType(typeof(UserProfile))!;

        Assert.Equal(nameof(UserProfile.UserId), Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        IForeignKey foreignKey = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(AppUser), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void UserProfile_DefaultPrivacyValues_ArePublicBasicOnly()
    {
        var profile = new UserProfile { UserId = "user-1" };

        Assert.True(profile.IsPublic);
        Assert.False(profile.ShowStats);
        Assert.False(profile.ShowBadges);
        Assert.False(profile.ShowActivity);
        Assert.False(profile.ShowPublicSets);
    }
}
