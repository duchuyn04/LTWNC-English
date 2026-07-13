using ltwnc.Models.Entities;

namespace ltwnc.Services.StudyModes;

// Bộ lọc thẻ dùng chung mọi mode: setId, chỉ sao, chỉ chưa thuộc.
// Trả IQueryable để strategy còn gắn thêm điều kiện riêng trước khi materialize.
public interface IStudyCardQueryService
{
    // Query thẻ đã lọc theo settings; chưa ToList
    IQueryable<Flashcard> CreateFilteredQuery(
        int setId,
        UserStudySettings settings,
        string? userId);
}
