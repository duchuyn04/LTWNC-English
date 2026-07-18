using ltwnc.Data;
using Microsoft.AspNetCore.Identity;

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
}
