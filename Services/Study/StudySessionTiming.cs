namespace ltwnc.Services.Study;

public static class StudySessionTiming
{
    public const int MaxDurationSeconds = 4 * 60 * 60;

    public static int CalculateDurationSeconds(DateTime startedAt, DateTime completedAt)
    {
        // 1. Tính giá trị và lưu vào `elapsedSeconds` để dùng ở bước tiếp theo.
        long elapsedSeconds = (long)Math.Floor((completedAt - startedAt).TotalSeconds);
        // 2. Trả `(int)Math.Clamp(elapsedSeconds, 0L, MaxDurationSeconds)` cho nơi gọi.
        return (int)Math.Clamp(elapsedSeconds, 0L, MaxDurationSeconds);
    }
}
