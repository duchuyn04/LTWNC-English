using ltwnc.Models.Entities;

namespace ltwnc.Services.StudyModes;

// Resolver lấy strategy theo StudyMode, đảm bảo mỗi mode chỉ có một strategy.
public interface IStudyModeStrategyResolver
{
    IStudyModeStrategy Resolve(StudyMode mode);
}
