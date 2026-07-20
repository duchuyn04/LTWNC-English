using System.Reflection;
using ltwnc.Data;
using ltwnc.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ltwnc.Tests.Data;

public class MigrationMetadataTests
{
    [Fact]
    public void StudySessionTimingMigration_IsDiscoverableByEntityFramework()
    {
        MigrationAttribute? attribute = typeof(AddStudySessionTiming)
            .GetCustomAttribute<MigrationAttribute>();
        DbContextAttribute? contextAttribute = typeof(AddStudySessionTiming)
            .GetCustomAttribute<DbContextAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("20260719000000_AddStudySessionTiming", attribute.Id);
        Assert.NotNull(contextAttribute);
        Assert.Equal(typeof(AppDbContext), contextAttribute.ContextType);
    }
}
