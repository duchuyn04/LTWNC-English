namespace ltwnc.Areas.Admin;

public static class AdminAreaPolicy
{
    public const string Name = "AdminAreaAccess";
    public const string RecentAuthenticationName = "AdminRecentAuthentication";
    public static readonly TimeSpan RecentAuthenticationLifetime = TimeSpan.FromMinutes(15);
}
