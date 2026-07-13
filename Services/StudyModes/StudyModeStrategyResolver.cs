using ltwnc.Models.Entities;

namespace ltwnc.Services.StudyModes;

// Chọn đúng IStudyModeStrategy theo StudyMode từ danh sách đăng ký DI.
public class StudyModeStrategyResolver : IStudyModeStrategyResolver
{
    // Mọi strategy đã AddScoped trong Program.cs
    private readonly IEnumerable<IStudyModeStrategy> _strategies;

    // Inject tập strategy từ DI
    public StudyModeStrategyResolver(IEnumerable<IStudyModeStrategy> strategies)
    {
        _strategies = strategies;
    }

    // Đúng 1 match thì trả về; 0 hoặc >1 thì throw (lỗi cấu hình)
    public IStudyModeStrategy Resolve(StudyMode mode)
    {
        List<IStudyModeStrategy> matches = new List<IStudyModeStrategy>();

        foreach (IStudyModeStrategy strategy in _strategies)
        {
            if (strategy.Mode == mode)
            {
                matches.Add(strategy);
            }
        }

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
