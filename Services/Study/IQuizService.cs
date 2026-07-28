using ltwnc.Models.Entities;

namespace ltwnc.Services.Study;

// Hợp đồng quản lý thiết lập, vòng đời, câu hỏi và kết quả Quiz.
public interface IQuizService
{
    // Lấy thông tin bộ thẻ và phiên Quiz đang hoạt động.
    Task<QuizSetupState> GetSetupAsync(int setId, string userId);

    // Tạo phiên Quiz mới theo bộ lọc và giới hạn thời gian.
    Task<StudySession> StartNewAsync(
        int setId,
        string userId,
        UserStudySettings settings,
        int? timeLimitMinutes);

    // Tiếp tục phiên đang hoạt động hoặc tạo phiên mới nếu chưa có.
    Task<StudySession> StartOrResumeAsync(
        int setId,
        string userId,
        UserStudySettings settings);

    // Lấy câu hỏi hiện tại hoặc câu hỏi cụ thể cần xem lại.
    Task<QuizQuestionState> GetCurrentQuestionAsync(
        int setId,
        int sessionId,
        string userId,
        int? questionId = null);

    // Chấm một lựa chọn và cập nhật tiến độ phiên Quiz.
    Task<QuizAnswerResult> AnswerAsync(
        int setId,
        int sessionId,
        int questionId,
        int selectedChoiceIndex,
        string userId);

    // Hoàn tất phiên đã hết thời gian làm bài.
    Task CompleteExpiredAsync(int setId, int sessionId, string userId);

    // Lấy điểm và danh sách câu trả lời sai của phiên.
    Task<QuizSessionResult> GetResultAsync(int setId, int sessionId, string userId);
    // Khởi động lại dựa trên cấu hình của phiên cũ.
    Task<StudySession> RestartAsync(int setId, int sessionId, string userId);
    // Tạo phiên mới chỉ gồm các câu trả lời sai.
    Task<StudySession> RetryWrongAsync(int setId, int sessionId, string userId);
    // Tạo phiên mới để làm lại toàn bộ câu hỏi.
    Task<StudySession> RetryAllAsync(int setId, int sessionId, string userId);
}
