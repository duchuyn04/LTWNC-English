namespace ltwnc.Tests.Views;

public sealed class EnglishMissionViewTests
{
    private static readonly string TopicView = ReadFile("Views", "EnglishMission", "SelectTopic.cshtml");
    private static readonly string ChatView = ReadFile("Views", "EnglishMission", "Chat.cshtml");
    private static readonly string MissionScript = ReadFile("wwwroot", "js", "english-mission.js");
    private static readonly string MissionStyles = ReadFile("wwwroot", "css", "english-mission.css");

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
