using ltwnc.Models.Entities;

namespace ltwnc.Services.StudyModes;

// Service truy vấn thẻ dùng chung cho các chế độ học.
// Trả về IQueryable để strategy có thể thêm điều kiện riêng trước khi thực thi.
public interface IStudyCardQueryService
{
    IQueryable<Flashcard> CreateFilteredQuery(
        int setId,
        UserStudySettings settings,
        string? userId);
}
