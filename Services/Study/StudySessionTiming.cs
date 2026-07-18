namespace ltwnc.Services.Study;

public static class StudySessionTiming
{
    public const int MaxDurationSeconds = 4 * 60 * 60;

    public static int CalculateDurationSeconds(DateTime startedAt, DateTime completedAt)
    {
        long elapsedSeconds = (long)Math.Floor((completedAt - startedAt).TotalSeconds);
        return (int)Math.Clamp(elapsedSeconds, 0L, MaxDurationSeconds);
    }
}
