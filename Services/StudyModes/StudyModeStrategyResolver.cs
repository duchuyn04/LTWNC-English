using ltwnc.Models.Entities;

namespace ltwnc.Services.StudyModes;

// Thực hiện resolve strategy theo mode với kiểm tra đăng ký duy nhất.
public class StudyModeStrategyResolver : IStudyModeStrategyResolver
{
    private readonly IEnumerable<IStudyModeStrategy> _strategies;

    public StudyModeStrategyResolver(IEnumerable<IStudyModeStrategy> strategies)
    {
        _strategies = strategies;
    }

    public IStudyModeStrategy Resolve(StudyMode mode)
    {
        var matches = _strategies.Where(s => s.Mode == mode).ToList();

        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"Không tìm thấy strategy cho {mode}.");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException($"Đã đăng ký nhiều strategy cho {mode}.");
        }

        return matches[0];
    }
}
