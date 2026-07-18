using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ltwnc.Tests.Data;

public class AppDbContextTests
{
    [Fact]
    public void Identity_email_index_is_unique_and_keeps_identity_name()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new AppDbContext(options);

        var entityType = db.Model.FindEntityType(typeof(IdentityUser));
        Assert.NotNull(entityType);

        var index = Assert.Single(entityType!.GetIndexes(), i =>
            i.Properties.Count == 1 &&
            i.Properties[0].Name == nameof(IdentityUser.NormalizedEmail));

        Assert.Equal("EmailIndex", index.GetDatabaseName());
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void UserProfile_UsesUserIdAsPrimaryKeyAndIdentityForeignKey()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new AppDbContext(options);

        IEntityType entity = db.Model.FindEntityType(typeof(UserProfile))!;

        Assert.Equal(nameof(UserProfile.UserId), Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        IForeignKey foreignKey = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(IdentityUser), foreignKey.PrincipalEntityType.ClrType);
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
