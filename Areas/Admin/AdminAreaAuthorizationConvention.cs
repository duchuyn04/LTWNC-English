using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace ltwnc.Areas.Admin;

public sealed class AdminAreaAuthorizationConvention : IControllerModelConvention
{
    // Gắn policy Admin cho mọi controller thuộc Area để controller mới không thể quên khai báo quyền.
    public void Apply(ControllerModel controller)
    {
        if (controller.RouteValues.TryGetValue("area", out string? area)
            && string.Equals(area, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            controller.Filters.Add(new AuthorizeFilter(AdminAreaPolicy.Name));
        }
    }
}
