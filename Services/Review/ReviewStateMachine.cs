using ltwnc.Models.Entities;

namespace ltwnc.Services.Review;

public sealed record ReviewSchedule(
    ReviewStage Stage,
    DateTimeOffset? NextReviewAtUtc,
    int LongTermIntervalDays);

public sealed record ReviewTransition(
    ReviewStage NextStage,
    DateTimeOffset NextReviewAtUtc,
    int LongTermIntervalDays);

public interface IReviewState
{
    ReviewStage Stage { get; }

    ReviewTransition Rate(
        ReviewRating rating,
        ReviewSchedule current,
        DateTimeOffset now,
        int maximumIntervalDays);
}

// State của thẻ mới: lần đánh giá đầu tiên đưa thẻ vào Learning hoặc Reviewing.
public sealed class NewReviewState : IReviewState
{
    public ReviewStage Stage => ReviewStage.New;

    public ReviewTransition Rate(
        ReviewRating rating,
        ReviewSchedule current,
        DateTimeOffset now,
        int maximumIntervalDays)
    {
        return rating switch
        {
            ReviewRating.Again => new(
                ReviewStage.Learning,
                now.AddMinutes(10),
                0),
            ReviewRating.Hard => new(
                ReviewStage.Learning,
                now.AddDays(1),
                0),
            ReviewRating.Good => new(
                ReviewStage.Reviewing,
                now.AddDays(2),
                2),
            ReviewRating.Easy => new(
                ReviewStage.Reviewing,
                now.AddDays(4),
                4),
            _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, "Mức nhớ không hợp lệ.")
        };
    }
}

public sealed class LearningReviewState : IReviewState
{
    public ReviewStage Stage => ReviewStage.Learning;

    public ReviewTransition Rate(
        ReviewRating rating,
        ReviewSchedule current,
        DateTimeOffset now,
        int maximumIntervalDays)
    {
        return rating switch
        {
            ReviewRating.Again => new(ReviewStage.Learning, now.AddMinutes(10), 0),
            ReviewRating.Hard => new(ReviewStage.Learning, now.AddDays(1), 0),
            ReviewRating.Good => ReviewScheduleCalculator.LongTerm(
                ReviewStage.Reviewing, now, 3, maximumIntervalDays),
            ReviewRating.Easy => ReviewScheduleCalculator.LongTerm(
                ReviewStage.Reviewing, now, 7, maximumIntervalDays),
            _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, "Mức nhớ không hợp lệ.")
        };
    }
}

public sealed class ReviewingReviewState : IReviewState
{
    public ReviewStage Stage => ReviewStage.Reviewing;

    public ReviewTransition Rate(
        ReviewRating rating,
        ReviewSchedule current,
        DateTimeOffset now,
        int maximumIntervalDays)
    {
        return rating switch
        {
            ReviewRating.Again => new(
                ReviewStage.Relearning,
                now.AddMinutes(10),
                current.LongTermIntervalDays),
            ReviewRating.Hard => ReviewScheduleCalculator.LongTerm(
                ReviewStage.Reviewing,
                now,
                ReviewScheduleCalculator.RoundUp(current.LongTermIntervalDays * 1.2),
                maximumIntervalDays),
            ReviewRating.Good => ReviewScheduleCalculator.LongTerm(
                ReviewStage.Reviewing,
                now,
                ReviewScheduleCalculator.RoundUp(current.LongTermIntervalDays * 2),
                maximumIntervalDays),
            ReviewRating.Easy => ReviewScheduleCalculator.LongTerm(
                ReviewStage.Reviewing,
                now,
                ReviewScheduleCalculator.RoundUp(current.LongTermIntervalDays * 3),
                maximumIntervalDays),
            _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, "Mức nhớ không hợp lệ.")
        };
    }
}

public sealed class RelearningReviewState : IReviewState
{
    public ReviewStage Stage => ReviewStage.Relearning;

    public ReviewTransition Rate(
        ReviewRating rating,
        ReviewSchedule current,
        DateTimeOffset now,
        int maximumIntervalDays)
    {
        return rating switch
        {
            ReviewRating.Again => new(
                ReviewStage.Relearning,
                now.AddMinutes(10),
                current.LongTermIntervalDays),
            ReviewRating.Hard => new(
                ReviewStage.Relearning,
                now.AddDays(1),
                current.LongTermIntervalDays),
            ReviewRating.Good => ReviewScheduleCalculator.LongTerm(
                ReviewStage.Reviewing,
                now,
                Math.Max(1, ReviewScheduleCalculator.RoundUp(current.LongTermIntervalDays * 0.5)),
                maximumIntervalDays),
            ReviewRating.Easy => ReviewScheduleCalculator.LongTerm(
                ReviewStage.Reviewing,
                now,
                Math.Max(2, ReviewScheduleCalculator.RoundUp(current.LongTermIntervalDays * 0.75)),
                maximumIntervalDays),
            _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, "Mức nhớ không hợp lệ.")
        };
    }
}

internal static class ReviewScheduleCalculator
{
    public static int RoundUp(double days) => Math.Max(1, (int)Math.Ceiling(days));

    public static ReviewTransition LongTerm(
        ReviewStage stage,
        DateTimeOffset now,
        int intervalDays,
        int maximumIntervalDays)
    {
        int cappedDays = Math.Min(maximumIntervalDays, Math.Max(1, intervalDays));
        return new(stage, now.AddDays(cappedDays), cappedDays);
    }
}

// Context của GoF State. Các State không truy cập database; service chịu trách nhiệm persistence.
public sealed class ReviewStateMachine
{
    private readonly IReadOnlyDictionary<ReviewStage, IReviewState> _states;

    public ReviewStateMachine()
    {
        _states = new Dictionary<ReviewStage, IReviewState>
        {
            [ReviewStage.New] = new NewReviewState(),
            [ReviewStage.Learning] = new LearningReviewState(),
            [ReviewStage.Reviewing] = new ReviewingReviewState(),
            [ReviewStage.Relearning] = new RelearningReviewState()
        };
    }

    public ReviewTransition Rate(
        ReviewSchedule current,
        ReviewRating rating,
        DateTimeOffset now,
        int maximumIntervalDays = ReviewSettingsPolicy.DefaultMaxIntervalDays)
    {
        ReviewSettingsPolicy.ValidateMaxIntervalDays(maximumIntervalDays);
        if (!_states.TryGetValue(current.Stage, out IReviewState? state))
        {
            throw new NotSupportedException($"Giai đoạn {current.Stage} chưa được triển khai.");
        }

        return state.Rate(rating, current, now, maximumIntervalDays);
    }
}
