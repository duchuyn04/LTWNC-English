using ltwnc.Models.Entities;
using MissionEntity = ltwnc.Models.Entities.EnglishMission;

namespace ltwnc.Services.EnglishMission;

// Chủ đề hội thoại người dùng có thể chọn để bắt đầu nhiệm vụ.
public sealed record EnglishMissionTopic(string Id, string Name, string Description);

// Dữ liệu nhiệm vụ, từ mục tiêu và lượt hội thoại sau khi bắt đầu hoặc tải lại.
public sealed class EnglishMissionStartResult
{
    public required MissionEntity Mission { get; init; }
    public required IReadOnlyList<EnglishMissionTargetWord> TargetWords { get; init; }
    public required IReadOnlyList<EnglishMissionTurn> Turns { get; init; }
}

// Kết quả sau một câu trả lời gồm lượt mới, trạng thái nhiệm vụ và từ mục tiêu.
public sealed class EnglishMissionRespondResult
{
    public required EnglishMissionTurn Turn { get; init; }
    public required MissionEntity Mission { get; init; }
    public required IReadOnlyList<EnglishMissionTargetWord> TargetWords { get; init; }
}

// Hợp đồng điều phối toàn bộ vòng đời nhiệm vụ hội thoại tiếng Anh.
public interface IEnglishMissionService
{
    // Trả danh sách chủ đề nhiệm vụ được hỗ trợ.
    IReadOnlyList<EnglishMissionTopic> GetTopics();

    // Tạo nhiệm vụ và phiên học mới từ bộ thẻ, chủ đề đã chọn.
    Task<EnglishMissionStartResult> StartAsync(string userId, int setId, string topic, CancellationToken cancellationToken = default);
    // Lấy nhiệm vụ hiện tại cùng lịch sử hội thoại.
    Task<EnglishMissionStartResult> GetAsync(string userId, int setId, int sessionId, CancellationToken cancellationToken = default);
    // Gửi câu trả lời của người dùng tới AI và cập nhật tiến độ nhiệm vụ.
    Task<EnglishMissionRespondResult> RespondAsync(string userId, int setId, int sessionId, string clientTurnId, string userText, CancellationToken cancellationToken = default);
    // Đánh dấu nhiệm vụ đã hoàn thành và chốt kết quả.
    Task CompleteAsync(string userId, int setId, int sessionId, CancellationToken cancellationToken = default);
}
