using ltwnc.Models.Entities;

namespace ltwnc.Services.Review;

// Value Object mô tả trạng thái bền vững hiện tại của một thẻ. Dữ liệu này được
// lấy từ ReviewProgress để Context khôi phục đúng Concrete State trước khi xử lý.
public sealed record ReviewSchedule(
    ReviewStage Stage,
    DateTimeOffset? NextReviewAtUtc,
    int LongTermIntervalDays);

// Kết quả của một lần xử lý hành động Rate. Concrete State tạo kết quả này,
// Context chuyển sang NextStage, sau đó ReviewService lưu kết quả vào database.
public sealed record ReviewTransition(
    ReviewStage NextStage,
    DateTimeOffset NextReviewAtUtc,
    int LongTermIntervalDays);

// State abstraction trong mẫu GoF State. Nhờ phụ thuộc vào abstraction này,
// Context không cần chứa switch theo ReviewStage để thực hiện hành vi nghiệp vụ.
public interface IReviewState
{
    // Mỗi Concrete State khai báo giai đoạn mà nó đại diện. Context dùng giá trị
    // này để khôi phục đúng State object từ trạng thái đã lưu trong database.
    ReviewStage Stage { get; }

    // Theo GoF State, Context được truyền vào để Concrete State có thể yêu cầu
    // chuyển trạng thái sau khi xử lý hành động Rate.
    ReviewTransition Rate(
        ReviewStateMachine context,
        ReviewRating rating,
        ReviewSchedule current,
        DateTimeOffset now,
        int maximumIntervalDays);
}

// Concrete State của thẻ mới. Lần đánh giá đầu tiên đưa thẻ vào Learning nếu
// người học còn gặp khó khăn, hoặc vào Reviewing nếu đã ghi nhớ đủ tốt.
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

        // Concrete State quyết định State kế tiếp rồi yêu cầu Context thực hiện
        // chuyển đổi, thay vì để ReviewService tự chọn một IReviewState mới.
        return context.TransitionTo(transition);
    }
}

// Concrete State cho giai đoạn học ngắn hạn. Again và Hard giữ thẻ trong
// Learning; Good và Easy tốt nghiệp thẻ sang lịch ôn dài hạn Reviewing.
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

// Concrete State cho giai đoạn ôn dài hạn. Again làm thẻ rơi về Relearning;
// các mức còn lại giữ Reviewing và điều chỉnh khoảng cách ôn theo hệ số.
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

// Concrete State cho thẻ đang học lại sau khi quên. Again và Hard tiếp tục chu
// kỳ Relearning; Good và Easy đưa thẻ trở lại Reviewing với interval đã giảm.
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

// Hàm dùng chung cho các Concrete State khi tạo lịch dài hạn. Lớp này chỉ làm
// nhiệm vụ tính toán, không phải một State và không thay đổi Context.
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

// Context trong mẫu GoF State. Context giữ State object hiện tại, chuyển toàn bộ
// hành vi Rate cho State đó và cung cấp TransitionTo để Concrete State thay đổi
// hành vi của Context cho lần xử lý tiếp theo.
//
// ReviewProgress trong database mới là nguồn dữ liệu bền vững. Vì Context được DI
// theo request và chỉ sống trong bộ nhớ, mỗi lệnh Rate phải khôi phục State object
// từ ReviewSchedule trước khi xử lý. Các Concrete State không truy cập database;
// ReviewService chịu trách nhiệm đọc và lưu ReviewProgress.
public sealed class ReviewStateMachine
{
    private readonly IReadOnlyDictionary<ReviewStage, IReviewState> _states;
    private IReviewState _state;

    public ReviewStateMachine()
    {
        _states = new Dictionary<ReviewStage, IReviewState>
        {
            [ReviewStage.New] = new NewReviewState(),
            [ReviewStage.Learning] = new LearningReviewState(),
            [ReviewStage.Reviewing] = new ReviewingReviewState(),
            [ReviewStage.Relearning] = new RelearningReviewState()
        };

        // New là trạng thái khởi tạo hợp lệ trước khi Context được hydrate từ
        // ReviewSchedule của một thẻ cụ thể.
        _state = _states[ReviewStage.New];
    }

    // Thuộc tính này thể hiện State object mà Context đang sở hữu. Ngoài việc
    // giúp quan sát đúng cấu trúc GoF, nó còn cho phép kiểm thử việc chuyển state.
    public ReviewStage CurrentStage => _state.Stage;

    public ReviewTransition Rate(
        ReviewSchedule current,
        ReviewRating rating,
        DateTimeOffset now,
        int maximumIntervalDays = ReviewSettingsPolicy.DefaultMaxIntervalDays)
    {
        ReviewSettingsPolicy.ValidateMaxIntervalDays(maximumIntervalDays);

        // Hydrate Context từ trạng thái bền vững của thẻ. Việc này cần thiết vì
        // một ReviewStateMachine có thể lần lượt xử lý nhiều thẻ trong một request.
        SetState(current.Stage);

        // Context không chứa điều kiện nghiệp vụ của từng giai đoạn. Concrete
        // State hiện tại xử lý Rating và chủ động gọi TransitionTo bên dưới.
        return _state.Rate(this, rating, current, now, maximumIntervalDays);
    }

    // Concrete State gọi phương thức này sau khi đã tính xong kết quả. Context
    // thay State object hiện tại, còn ReviewTransition được trả về nguyên vẹn để
    // ReviewService lưu các giá trị lịch ôn vào database.
    internal ReviewTransition TransitionTo(ReviewTransition transition)
    {
        SetState(transition.NextStage);
        return transition;
    }

    // Mọi thay đổi State object đều đi qua một điểm duy nhất để bảo đảm enum luôn
    // ánh xạ tới một Concrete State đã được triển khai.
    private void SetState(ReviewStage stage)
    {
        if (!_states.TryGetValue(stage, out IReviewState? state))
        {
            throw new NotSupportedException($"Giai đoạn {stage} chưa được triển khai.");
        }

        _state = state;
    }
}
