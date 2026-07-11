using ltwnc.Services.StudyEvents;

namespace ltwnc.Tests.StudyEvents;

public class AchievementCatalogTests
{
    [Fact]
    public void All_contains_expected_medium_scope_codes()
    {
        var codes = AchievementCatalog.All.Select(d => d.Code).ToHashSet();
        Assert.Contains(AchievementCatalog.FirstCardMastered, codes);
        Assert.Contains(AchievementCatalog.CardsMastered10, codes);
        Assert.Contains("cards_mastered_25", codes);
        Assert.Contains("cards_mastered_50", codes);
        Assert.Contains("cards_mastered_100", codes);
        Assert.Contains("flashcard_sessions_5", codes);
        Assert.Contains("flashcard_sessions_10", codes);
        Assert.Contains("flashcard_sessions_20", codes);
        Assert.Contains("dictation_sessions_5", codes);
        Assert.Contains("dictation_correct_10", codes);
        Assert.Contains("dictation_correct_50", codes);
        Assert.Contains(AchievementCatalog.DictationPerfectSession, codes);
    }

    [Fact]
    public void Every_definition_has_positive_target_and_cta()
    {
        Assert.All(AchievementCatalog.All, d =>
        {
            Assert.True(d.Target > 0);
            Assert.False(string.IsNullOrWhiteSpace(d.CtaText));
            Assert.False(string.IsNullOrWhiteSpace(d.CtaPath));
            Assert.StartsWith("/", d.CtaPath);
        });
    }

    [Theory]
    [InlineData(AchievementCatalog.CardsMastered10, AchievementMetricKind.CardsMastered, 10)]
    [InlineData(AchievementCatalog.DictationPerfectSession, AchievementMetricKind.DictationPerfectSessions, 1)]
    public void Find_returns_metric_and_target(string code, AchievementMetricKind metric, int target)
    {
        var def = AchievementCatalog.Find(code);
        Assert.NotNull(def);
        Assert.Equal(metric, def!.Metric);
        Assert.Equal(target, def.Target);
    }
}
