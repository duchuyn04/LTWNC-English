namespace ltwnc.Areas.Admin;

// Múi giờ hiển thị cho khu vực quản trị: lưu UTC, hiển thị giờ Việt Nam.
public static class AdminTimeZone
{
    public static TimeZoneInfo Vietnam { get; } = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    public static DateTimeOffset ToVietnamTime(DateTime utc) => TimeZoneInfo.ConvertTime(
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)),
        Vietnam);
}
