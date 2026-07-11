using ltwnc.Models.Entities;
using ltwnc.Services.StudyModes;
using Xunit;

namespace ltwnc.Tests.StudyModes;

// Kiểm tra resolver tìm đúng strategy theo StudyMode và xử lý lỗi đăng ký
public class StudyModeStrategyResolverTests
{
    private static IStudyModeStrategy CreateFake(StudyMode mode)
    {
        return new FakeStrategy(mode);
    }

    [Fact]
    // Resolver trả về strategy khớp với mode yêu cầu
    public void Resolve_returns_matching_strategy()
    {
        var strategies = new List<IStudyModeStrategy>
        {
            CreateFake(StudyMode.Flashcard),
            CreateFake(StudyMode.Dictation)
        };
        var resolver = new StudyModeStrategyResolver(strategies);

        var result = resolver.Resolve(StudyMode.Dictation);

        Assert.Equal(StudyMode.Dictation, result.Mode);
    }

    [Fact]
    // Ném lỗi khi không có strategy nào cho mode
    public void Resolve_throws_when_no_strategy_registered()
    {
        var resolver = new StudyModeStrategyResolver(Array.Empty<IStudyModeStrategy>());

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(StudyMode.Flashcard));
        Assert.Contains("Không tìm thấy strategy", ex.Message);
    }

    [Fact]
    // Ném lỗi khi đăng ký trùng strategy cho cùng một mode
    public void Resolve_throws_when_multiple_strategies_for_same_mode()
    {
        var strategies = new List<IStudyModeStrategy>
        {
            CreateFake(StudyMode.Flashcard),
            CreateFake(StudyMode.Flashcard)
        };
        var resolver = new StudyModeStrategyResolver(strategies);

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(StudyMode.Flashcard));
        Assert.Contains("nhiều strategy", ex.Message);
    }

    private sealed class FakeStrategy : IStudyModeStrategy
    {
        public StudyMode Mode { get; }

        public FakeStrategy(StudyMode mode)
        {
            Mode = mode;
        }

        public Task<List<Flashcard>> GetCardsAsync(int setId, UserStudySettings settings, string? userId)
            => Task.FromResult(new List<Flashcard>());

        public Models.ViewModels.Study.StudyModeOptionViewModel BuildOption(
            int setId,
            IReadOnlyList<Flashcard> cards,
            UserStudySettings settings)
            => new() { Mode = Mode };
    }
}
