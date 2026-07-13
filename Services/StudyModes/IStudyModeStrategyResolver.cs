using ltwnc.Models.Entities;

namespace ltwnc.Services.StudyModes;

// Tìm strategy đúng cho một StudyMode (0 hoặc >1 strategy cùng mode = lỗi cấu hình).
public interface IStudyModeStrategyResolver
{
    // Trả về strategy duy nhất cho mode; throw nếu thiếu hoặc trùng
    IStudyModeStrategy Resolve(StudyMode mode);
}
