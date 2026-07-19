using ltwnc.Services.Auth;

namespace ltwnc.Services.AdminExports;

public static class AdminExportActorFactory
{
    // Dựng actor audit từ current user đã đi qua policy Admin, có fallback để audit không bị thiếu định danh.
    public static AdminExportActor FromCurrentUser(ICurrentUser currentUser)
    {
        string actorUserId = "unknown-admin";
        if (currentUser.UserId != null)
        {
            actorUserId = currentUser.UserId;
        }

        string actorDisplay = "Admin";
        if (currentUser.UserName != null)
        {
            actorDisplay = currentUser.UserName;
        }

        return new AdminExportActor(actorUserId, actorDisplay);
    }
}
