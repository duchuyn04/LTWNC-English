using ltwnc.Models.Entities;
using ltwnc.Services.Review;

namespace ltwnc.Tests.Services.Review;

public sealed class ReviewStateMachineTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 31, 8, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ReviewStage.New, ReviewRating.Again, ReviewStage.Learning, 10, 0)]
    [InlineData(ReviewStage.New, ReviewRating.Hard, ReviewStage.Learning, 1440, 0)]
    [InlineData(ReviewStage.New, ReviewRating.Good, ReviewStage.Reviewing, 2880, 2)]
    [InlineData(ReviewStage.New, ReviewRating.Easy, ReviewStage.Reviewing, 5760, 4)]
    [InlineData(ReviewStage.Learning, ReviewRating.Again, ReviewStage.Learning, 10, 0)]
    [InlineData(ReviewStage.Learning, ReviewRating.Hard, ReviewStage.Learning, 1440, 0)]
    [InlineData(ReviewStage.Learning, ReviewRating.Good, ReviewStage.Reviewing, 4320, 3)]
    [InlineData(ReviewStage.Learning, ReviewRating.Easy, ReviewStage.Reviewing, 10080, 7)]
    [InlineData(ReviewStage.Reviewing, ReviewRating.Again, ReviewStage.Relearning, 10, 10)]
    [InlineData(ReviewStage.Reviewing, ReviewRating.Hard, ReviewStage.Reviewing, 17280, 12)]
    [InlineData(ReviewStage.Reviewing, ReviewRating.Good, ReviewStage.Reviewing, 28800, 20)]
    [InlineData(ReviewStage.Reviewing, ReviewRating.Easy, ReviewStage.Reviewing, 43200, 30)]
    [InlineData(ReviewStage.Relearning, ReviewRating.Again, ReviewStage.Relearning, 10, 10)]
    [InlineData(ReviewStage.Relearning, ReviewRating.Hard, ReviewStage.Relearning, 1440, 10)]
    [InlineData(ReviewStage.Relearning, ReviewRating.Good, ReviewStage.Reviewing, 7200, 5)]
    [InlineData(ReviewStage.Relearning, ReviewRating.Easy, ReviewStage.Reviewing, 11520, 8)]
    public void Rate_AllStatesUseTheAgreedSchedule(
        ReviewStage stage,
        ReviewRating rating,
        ReviewStage expectedStage,
        int expectedDelayMinutes,
        int expectedIntervalDays)
    {
        ReviewStateMachine machine = new();

        ReviewTransition transition = machine.Rate(
            new ReviewSchedule(stage, FixedNow, stage is ReviewStage.New or ReviewStage.Learning ? 0 : 10),
            rating,
            FixedNow,
            maximumIntervalDays: 30);

        Assert.Equal(expectedStage, transition.NextStage);
        Assert.Equal(FixedNow.AddMinutes(expectedDelayMinutes), transition.NextReviewAtUtc);
        Assert.Equal(expectedIntervalDays, transition.LongTermIntervalDays);
    }

    [Fact]
    public void Rate_LongTermIntervalRoundsUpAndRespectsMaximum()
    {
        ReviewStateMachine machine = new();

        ReviewTransition transition = machine.Rate(
            new ReviewSchedule(ReviewStage.Reviewing, FixedNow, 20),
            ReviewRating.Easy,
            FixedNow,
            maximumIntervalDays: 30);

        Assert.Equal(30, transition.LongTermIntervalDays);
        Assert.Equal(FixedNow.AddDays(30), transition.NextReviewAtUtc);
    }

    [Fact]
    public void Rate_RelearningMinimumsKeepGoodAtOneDayAndEasyAtTwoDays()
    {
        ReviewStateMachine machine = new();

        ReviewTransition good = machine.Rate(
            new ReviewSchedule(ReviewStage.Relearning, FixedNow, 1),
            ReviewRating.Good,
            FixedNow,
            maximumIntervalDays: 30);
        ReviewTransition easy = machine.Rate(
            new ReviewSchedule(ReviewStage.Relearning, FixedNow, 1),
            ReviewRating.Easy,
            FixedNow,
            maximumIntervalDays: 30);

        Assert.Equal(1, good.LongTermIntervalDays);
        Assert.Equal(2, easy.LongTermIntervalDays);
    }

    [Fact]
    public void Rate_ConcreteStateChangesTheStateOwnedByContext()
    {
        ReviewStateMachine machine = new();

        Assert.Equal(ReviewStage.New, machine.CurrentStage);

        ReviewTransition transition = machine.Rate(
            new ReviewSchedule(ReviewStage.Reviewing, FixedNow, 10),
            ReviewRating.Again,
            FixedNow,
            maximumIntervalDays: 30);

        Assert.Equal(ReviewStage.Relearning, transition.NextStage);
        Assert.Equal(ReviewStage.Relearning, machine.CurrentStage);
    }
}
