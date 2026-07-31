namespace ltwnc.Tests.Views;

public sealed class EnglishMissionViewTests
{
    private static readonly string TopicView = ReadFile("Views", "EnglishMission", "SelectTopic.cshtml");
    private static readonly string ChatView = ReadFile("Views", "EnglishMission", "Chat.cshtml");
    private static readonly string MissionScript = ReadFile("wwwroot", "js", "english-mission.js");
    private static readonly string MissionStyles = ReadFile("wwwroot", "css", "english-mission.css");
    private static readonly string SiteStyles = ReadFile("wwwroot", "css", "site.css");

    [Fact]
    public void EnglishMission_chat_exposes_progress_and_accessible_conversation_contract()
    {
        Assert.Contains("data-max-turns=\"8\"", ChatView);
        Assert.Contains("aria-busy=\"false\"", ChatView);
        Assert.Contains("role=\"log\"", ChatView);
        Assert.Contains("aria-relevant=\"additions\"", ChatView);
        Assert.Contains("data-mission-progress", ChatView);
        Assert.Contains("role=\"progressbar\"", ChatView);
        Assert.Contains("aria-valuenow=\"@Model.Mission.TurnCount\"", ChatView);
        Assert.Contains("for=\"mission-answer\"", ChatView);
        Assert.Contains("role=\"alert\"", ChatView);
    }

    [Fact]
    public void EnglishMission_script_updates_progress_and_supports_replay_for_dynamic_ai_turns()
    {
        Assert.Contains("data-mission-progress-bar", MissionScript);
        Assert.Contains("function updateProgress", MissionScript);
        Assert.Contains("progress.setAttribute('aria-valuenow'", MissionScript);
        Assert.Contains("function configureSpeechButtons", MissionScript);
        Assert.Contains("class=\"mission-play\"", MissionScript);
        Assert.Contains("input.disabled = true", MissionScript);
        Assert.Contains("page.setAttribute('aria-busy', 'true')", MissionScript);
        Assert.Contains("window.speechSynthesis", MissionScript);
    }

    [Fact]
    public void EnglishMission_topic_navigation_and_controls_have_focus_and_motion_fallbacks()
    {
        Assert.Contains("aria-label=\"Quay lại Study Hub\"", TopicView);
        Assert.Contains(".mission-back:focus-visible", MissionStyles);
        Assert.Contains(".mission-send:focus-visible", MissionStyles);
        Assert.Contains("@media (prefers-reduced-motion:reduce)", MissionStyles);
    }

    [Fact]
    public void EnglishMission_topic_submission_exposes_pending_feedback_and_prevents_duplicate_starts()
    {
        Assert.Contains("data-mission-topic-page", TopicView);
        Assert.Contains("data-mission-start-form", TopicView);
        Assert.Contains("data-mission-start-status", TopicView);
        Assert.Contains("data-mission-start-skeleton", TopicView);
        Assert.Contains("aria-live=\"polite\"", TopicView);
        Assert.Contains("function configureTopicStart", MissionScript);
        Assert.Contains("topicPage.setAttribute('aria-busy', 'true')", MissionScript);
        Assert.Contains("button.disabled = true", MissionScript);
        Assert.Contains(".mission-start-skeleton", MissionStyles);
    }

    [Fact]
    public void EnglishMission_chat_shows_typing_feedback_and_progressively_renders_ai_turns()
    {
        Assert.Contains("function appendPendingNpc", MissionScript);
        Assert.Contains("mission-typing-dots", MissionScript);
        Assert.Contains("function streamNpcText", MissionScript);
        Assert.Contains("await streamNpcText", MissionScript);
        Assert.Contains("@keyframes mission-typing", MissionStyles);
    }

    [Fact]
    public void EnglishMission_chat_autoplays_ai_and_exposes_bilingual_reply_suggestion()
    {
        Assert.Contains("data-mission-suggestion", ChatView);
        Assert.Contains("data-suggestion-en", ChatView);
        Assert.Contains("data-suggestion-vi", ChatView);
        Assert.Contains("function speakText", MissionScript);
        Assert.Contains("speakText(turn.npcText)", MissionScript);
        Assert.Contains("function updateSuggestion", MissionScript);
        Assert.Contains("data.suggestedReplyEn", MissionScript);
        Assert.Contains("data.suggestedReplyVi", MissionScript);
    }

    [Fact]
    public void EnglishMission_responsive_dock_does_not_cover_conversation_and_controls_are_touch_sized()
    {
        Assert.Contains(".mission-response-dock{position:static", MissionStyles);
        Assert.DoesNotContain(".mission-response-dock{position:sticky", MissionStyles);
        Assert.Contains(".mission-send{width:2.75rem;height:2.75rem", MissionStyles);
        Assert.Contains(".app-nav .navbar-toggler", SiteStyles);
        Assert.Contains("min-height: 44px", SiteStyles);
    }

    private static string ReadFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        return string.Empty;
    }
}
