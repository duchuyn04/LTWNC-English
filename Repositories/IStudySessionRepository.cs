using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

// Interface định nghĩa các phương thức truy xuất bảng StudySessions và UserProgresses
public interface IStudySessionRepository
{
    // Thêm phiên học mới và lưu vào database
    Task AddAsync(StudySession session);

    // Lấy tiến trình học của một người dùng cho một thẻ cụ thể
    Task<UserProgress?> GetProgressAsync(string userId, int flashcardId);

    // Lấy tất cả tiến trình học của một người dùng trong một bộ thẻ
    Task<List<UserProgress>> GetProgressBySetAsync(string userId, int setId);

    // Đánh dấu tiến trình cần cập nhật
    void UpdateProgress(UserProgress progress);

    // Thêm tiến trình học mới vào database
    Task AddProgressAsync(UserProgress progress);
}
