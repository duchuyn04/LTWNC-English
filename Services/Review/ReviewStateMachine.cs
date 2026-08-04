using ltwnc.Models.Entities;

namespace ltwnc.Services.Review;

// Thông tin ôn tập hiện tại của một thẻ, được đọc từ ReviewProgress trong database.
// State machine dùng Stage để chọn đúng cách xử lý cho lần đánh giá tiếp theo.
public sealed record ReviewSchedule(
    ReviewStage Stage,
    DateTimeOffset? NextReviewAtUtc,
    int LongTermIntervalDays);

// Kết quả sau khi người học chọn Again, Hard, Good hoặc Easy.
// ReviewService sẽ lưu trạng thái và lịch ôn mới này vào database.
public sealed record ReviewTransition(
    ReviewStage NextStage,
    DateTimeOffset NextReviewAtUtc,
    int LongTermIntervalDays);

// Quy định chung cho mọi trạng thái học. Mỗi class trạng thái tự xử lý Rate,
// vì vậy ReviewStateMachine không phải chứa một switch lớn cho toàn bộ nghiệp vụ.
public interface IReviewState
{
    // Giai đoạn mà class này xử lý: New, Learning, Reviewing hoặc Relearning.
    ReviewStage Stage { get; }

    // Tính trạng thái tiếp theo và ngày ôn tiếp theo dựa trên lựa chọn của người học.
    // context được truyền vào để state hiện tại yêu cầu chuyển sang state mới.
    ReviewTransition Rate(
        ReviewStateMachine context,
        ReviewRating rating,
        ReviewSchedule current,
        DateTimeOffset now,
        int maximumIntervalDays);
}

// Xử lý thẻ chưa từng học:
// - Again/Hard: đưa vào giai đoạn học ngắn hạn.
// - Good/Easy: chuyển thẳng sang ôn dài hạn.
public sealed class NewReviewState : IReviewState
{
    public ReviewStage Stage => ReviewStage.New;

    public ReviewTransition Rate(
        ReviewStateMachine context,
        ReviewRating rating,
        ReviewSchedule current,
        DateTimeOffset now,
        int maximumIntervalDays)
    {
        ReviewTransition transition = rating switch
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

        // State hiện tại đã tính xong kết quả; Context chỉ cập nhật state đang giữ.
        return context.TransitionTo(transition);
    }
}

// Xử lý thẻ đang học ngắn hạn:
// - Again/Hard: tiếp tục Learning và sớm gặp lại thẻ.
// - Good/Easy: chuyển sang Reviewing để bắt đầu ôn dài hạn.
public sealed class LearningReviewState : IReviewState
{
    public ReviewStage Stage => ReviewStage.Learning;

    public ReviewTransition Rate(
        ReviewStateMachine context,
        ReviewRating rating,
        ReviewSchedule current,
        DateTimeOffset now,
        int maximumIntervalDays)
    {
        ReviewTransition transition = rating switch
        {
            ReviewRating.Again => new(ReviewStage.Learning, now.AddMinutes(10), 0),
            ReviewRating.Hard => new(ReviewStage.Learning, now.AddDays(1), 0),
            ReviewRating.Good => ReviewScheduleCalculator.LongTerm(
                ReviewStage.Reviewing, now, 3, maximumIntervalDays),
            ReviewRating.Easy => ReviewScheduleCalculator.LongTerm(
                ReviewStage.Reviewing, now, 7, maximumIntervalDays),
            _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, "Mức nhớ không hợp lệ.")
        };

        return context.TransitionTo(transition);
    }
}

// Xử lý thẻ đang ôn dài hạn:
// - Again: người học đã quên, chuyển sang Relearning.
// - Hard/Good/Easy: vẫn Reviewing nhưng tăng khoảng cách tới lần ôn sau.
public sealed class ReviewingReviewState : IReviewState
{
    public ReviewStage Stage => ReviewStage.Reviewing;

    public ReviewTransition Rate(
        ReviewStateMachine context,
        ReviewRating rating,
        ReviewSchedule current,
        DateTimeOffset now,
        int maximumIntervalDays)
    {
        ReviewTransition transition = rating switch
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

        return context.TransitionTo(transition);
    }
}

// Xử lý thẻ đang học lại sau khi quên:
// - Again/Hard: tiếp tục Relearning.
// - Good/Easy: trở lại Reviewing, nhưng dùng khoảng ôn ngắn hơn trước.
public sealed class RelearningReviewState : IReviewState
{
    public ReviewStage Stage => ReviewStage.Relearning;

    public ReviewTransition Rate(
        ReviewStateMachine context,
        ReviewRating rating,
        ReviewSchedule current,
        DateTimeOffset now,
        int maximumIntervalDays)
    {
        ReviewTransition transition = rating switch
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

        return context.TransitionTo(transition);
    }
}

// Hàm tính lịch dùng chung, tránh lặp công thức trong nhiều state.
// Lớp này chỉ tính số ngày, không tự chuyển trạng thái.
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

// Đầu mối điều phối các state. Class này chọn state phù hợp rồi giao việc Rate
// cho state đó; bản thân nó không chứa công thức lên lịch của từng giai đoạn.
//
// Trạng thái thật được lưu trong ReviewProgress. ReviewStateMachine chỉ sống trong
// bộ nhớ nên mỗi lần Rate phải đọc lại Stage từ ReviewSchedule. Các state không
// truy cập database; ReviewService chịu trách nhiệm đọc và lưu dữ liệu.
public sealed class ReviewStateMachine
{
    private readonly IReadOnlyDictionary<ReviewStage, IReviewState> _states;
    private IReviewState _state;

    // ---
    public ReviewStateMachine()
    {
        _states = new Dictionary<ReviewStage, IReviewState>
        {
            [ReviewStage.New] = new NewReviewState(),
            [ReviewStage.Learning] = new LearningReviewState(),
            [ReviewStage.Reviewing] = new ReviewingReviewState(),
            [ReviewStage.Relearning] = new RelearningReviewState()
        };

        // Dùng New làm giá trị ban đầu. Khi Rate chạy, Stage thật của thẻ sẽ thay thế nó.
        _state = _states[ReviewStage.New];
    }

    // Cho biết state machine đang giữ giai đoạn nào; chủ yếu hữu ích khi kiểm thử.
    public ReviewStage CurrentStage => _state.Stage;

    public ReviewTransition Rate(
        ReviewSchedule current,
        ReviewRating rating,
        DateTimeOffset now,
        int maximumIntervalDays = ReviewSettingsPolicy.DefaultMaxIntervalDays)
    {
        ReviewSettingsPolicy.ValidateMaxIntervalDays(maximumIntervalDays);

        // Chọn đúng state dựa trên tiến độ đã lưu của thẻ đang được đánh giá.
        // Cùng một state machine có thể xử lý nhiều thẻ khác nhau trong một request.
        SetState(current.Stage);

        // Giao toàn bộ việc tính lịch cho state hiện tại.
        return _state.Rate(this, rating, current, now, maximumIntervalDays);
    }

    // State hiện tại gọi hàm này để chuyển sang giai đoạn vừa tính được.
    // Kết quả được trả nguyên vẹn để ReviewService lưu vào database.
    internal ReviewTransition TransitionTo(ReviewTransition transition)
    {
        SetState(transition.NextStage);
        return transition;
    }

    // Đổi enum ReviewStage thành đúng object IReviewState.
    // Nếu có enum mới nhưng chưa có class xử lý, báo lỗi rõ ràng tại đây.
    private void SetState(ReviewStage stage)
    {
        if (!_states.TryGetValue(stage, out IReviewState? state))
        {
            throw new NotSupportedException($"Giai đoạn {stage} chưa được triển khai.");
        }

        _state = state;
    }
}
