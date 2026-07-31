using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Tests.Services.Study;

public sealed class StudySettingsMapperTests
{
    [Fact]
    public void ReviewPolicySettings_RoundTripThroughStudySettingsMapper()
    {
        StudySettingsViewModel input = new()
        {
            ReviewSessionSize = 35,
            ReviewMaxIntervalDays = 120
        };

        UserStudySettings entity = StudySettingsMapper.ToEntity(input);
        StudySettingsViewModel output = StudySettingsMapper.ToViewModel(entity);

        Assert.Equal(35, entity.ReviewSessionSize);
        Assert.Equal(120, entity.ReviewMaxIntervalDays);
        Assert.Equal(35, output.ReviewSessionSize);
        Assert.Equal(120, output.ReviewMaxIntervalDays);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(100)]
    public void ValidateSessionSize_BoundaryValues_ReturnsValue(int value)
    {
        int actual = ReviewSettingsPolicy.ValidateSessionSize(value);

        Assert.Equal(value, actual);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(101)]
    public void ValidateSessionSize_RejectsValuesOutsideTheAgreedRange(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReviewSettingsPolicy.ValidateSessionSize(value));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(365)]
    public void ValidateMaxIntervalDays_BoundaryValues_ReturnsValue(int value)
    {
        int actual = ReviewSettingsPolicy.ValidateMaxIntervalDays(value);

        Assert.Equal(value, actual);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(366)]
    public void ValidateMaxIntervalDays_RejectsValuesOutsideTheAgreedRange(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReviewSettingsPolicy.ValidateMaxIntervalDays(value));
    }
}
