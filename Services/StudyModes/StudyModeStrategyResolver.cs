using ltwnc.Models.Entities;

namespace ltwnc.Services.StudyModes;

// Resolver: đứng giữa StudyService và các strategy.
// Nhiệm vụ: tìm đúng strategy cho một StudyMode và bảo vệ rằng mỗi mode chỉ có 1 strategy.
public class StudyModeStrategyResolver : IStudyModeStrategyResolver
{
    private readonly IEnumerable<IStudyModeStrategy> _strategies;

    public StudyModeStrategyResolver(IEnumerable<IStudyModeStrategy> strategies)
    {
        _strategies = strategies;
    }

    public IStudyModeStrategy Resolve(StudyMode mode)
    {
        // Tìm tất cả strategy phụ trách mode này
        var matches = _strategies.Where(s => s.Mode == mode).ToList();

        // Không có strategy nào -> báo lỗi rõ ràng để dễ debug cấu hình DI
        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"Không tìm thấy strategy cho {mode}.");
        }

        // Có nhiều hơn 1 strategy -> cấu hình DI sai, cũng báo lỗi rõ ràng
        if (matches.Count > 1)
        {
            throw new InvalidOperationException($"Đã đăng ký nhiều strategy cho {mode}.");
        }

        return matches[0];
    }
}
