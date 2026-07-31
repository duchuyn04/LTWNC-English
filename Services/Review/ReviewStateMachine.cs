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
        DateTimeOffset now);
}

// State của thẻ mới: lần đánh giá đầu tiên đưa thẻ vào Learning hoặc Reviewing.
public sealed class NewReviewState : IReviewState
{
    public ReviewStage Stage => ReviewStage.New;

    public ReviewTransition Rate(
        ReviewRating rating,
        ReviewSchedule current,
        DateTimeOffset now)
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

// Context của GoF State. Các State không truy cập database; service chịu trách nhiệm persistence.
public sealed class ReviewStateMachine
{
    private readonly IReadOnlyDictionary<ReviewStage, IReviewState> _states;

    public ReviewStateMachine()
    {
        _states = new Dictionary<ReviewStage, IReviewState>
        {
            [ReviewStage.New] = new NewReviewState()
        };
    }

    public ReviewTransition Rate(
        ReviewSchedule current,
        ReviewRating rating,
        DateTimeOffset now)
    {
        if (!_states.TryGetValue(current.Stage, out IReviewState? state))
        {
            throw new NotSupportedException(
                $"Giai đoạn {current.Stage} chưa được triển khai trong ticket hiện tại.");
        }

        return state.Rate(rating, current, now);
    }
}
