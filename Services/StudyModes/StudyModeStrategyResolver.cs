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
        // 1. Lưu dependency `_strategies` để các phương thức khác sử dụng.
        _strategies = strategies;
    }

    // Đúng 1 match thì trả về; 0 hoặc >1 thì throw (lỗi cấu hình)
    public IStudyModeStrategy Resolve(StudyMode mode)
    {
        // 1. Khởi tạo `matches` với dữ liệu ban đầu cần thiết.
        List<IStudyModeStrategy> matches = new List<IStudyModeStrategy>();

        // 2. Duyệt từng `strategy` trong `_strategies` để xử lý lần lượt.
        foreach (IStudyModeStrategy strategy in _strategies)
        {
            // 3. Kiểm tra `strategy.Mode == mode` để chọn nhánh xử lý phù hợp.
            if (strategy.Mode == mode)
            {
                // 4. Gọi `Add` để thực hiện bước nghiệp vụ này.
                matches.Add(strategy);
            }
        }

        // 5. Kiểm tra `matches.Count == 0` để chọn nhánh xử lý phù hợp.
        if (matches.Count == 0)
        {
            // 6. Dừng xử lý và phát sinh lỗi `new InvalidOperationException($"Không tìm thấy strategy cho {mode}.")`.
            throw new InvalidOperationException($"Không tìm thấy strategy cho {mode}.");
        }

        // 7. Kiểm tra `matches.Count > 1` để chọn nhánh xử lý phù hợp.
        if (matches.Count > 1)
        {
            // 8. Dừng xử lý và phát sinh lỗi `new InvalidOperationException($"Đã đăng ký nhiều strategy cho {mode...`.
            throw new InvalidOperationException($"Đã đăng ký nhiều strategy cho {mode}.");
        }

        // 9. Trả `matches[0]` cho nơi gọi.
        return matches[0];
    }
}
