using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

// Giai đoạn ghi nhớ riêng của hoạt động Ôn tập đến hạn.
public enum ReviewStage
{
    New,
    Learning,
    Reviewing,
    Relearning
}

// Mức nhớ do người học tự chọn sau khi hiện đáp án.
public enum ReviewRating
{
    Again,
    Hard,
    Good,
    Easy
}

// Lịch ôn riêng cho một cặp user-thẻ. Không tạo dòng cho tới lần đánh giá đầu tiên.
public class ReviewProgress
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int FlashcardId { get; set; }

    public ReviewStage Stage { get; set; } = ReviewStage.New;

    public DateTimeOffset? NextReviewAtUtc { get; set; }

    // Khoảng cơ sở theo ngày, dùng cho giai đoạn Reviewing/Relearning.
    public int LongTermIntervalDays { get; set; }

    public DateTimeOffset? LastRatedAtUtc { get; set; }

    [ForeignKey(nameof(FlashcardId))]
    public Flashcard? Flashcard { get; set; }
}

// Một lượt Ôn tập được lưu để có thể tiếp tục sau khi tải lại trang.
public class ReviewSession
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public ICollection<ReviewSessionItem> Items { get; set; } = new List<ReviewSessionItem>();
}

// Lịch sử phân thẻ và đánh giá trong một lượt; mỗi item chỉ nhận đánh giá đầu tiên.
public class ReviewSessionItem
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ReviewSessionId { get; set; }

    [Required]
    public int FlashcardId { get; set; }

    public int OrderIndex { get; set; }

    public bool IsNewCardAtAssignment { get; set; }

    public ReviewRating? Rating { get; set; }

    public DateTimeOffset? RatedAtUtc { get; set; }

    public ReviewStage PreviousStage { get; set; } = ReviewStage.New;

    public ReviewStage NextStage { get; set; } = ReviewStage.New;

    public DateTimeOffset? PreviousNextReviewAtUtc { get; set; }

    public DateTimeOffset? NextReviewAtUtc { get; set; }

    public int PreviousLongTermIntervalDays { get; set; }

    public int NextLongTermIntervalDays { get; set; }

    [ForeignKey(nameof(ReviewSessionId))]
    public ReviewSession? ReviewSession { get; set; }

    [ForeignKey(nameof(FlashcardId))]
    public Flashcard? Flashcard { get; set; }
}
