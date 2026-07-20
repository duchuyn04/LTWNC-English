using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.StudyModes;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.StudyModes;

public sealed class EnglishMissionModeStrategyTests
{
    [Fact]
    public void BuildOption_RequiresAtLeastThreeCards()
    {
        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var strategy = new EnglishMissionModeStrategy(new StudyCardQueryService(context));

        var unavailable = strategy.BuildOption(7, [new Flashcard(), new Flashcard()], new UserStudySettings());
        var available = strategy.BuildOption(7, [new Flashcard(), new Flashcard(), new Flashcard()], new UserStudySettings());

        Assert.False(unavailable.IsAvailable);
        Assert.True(available.IsAvailable);
        Assert.Equal("/Study/7/Mission", available.ActionUrl);
        Assert.Equal(StudyMode.EnglishMission, available.Mode);
    }
}
