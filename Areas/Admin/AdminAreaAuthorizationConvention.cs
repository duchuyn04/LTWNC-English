using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace ltwnc.Areas.Admin;

public sealed class AdminAreaAuthorizationConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        if (controller.RouteValues.TryGetValue("area", out string? area)
            && string.Equals(area, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            controller.Filters.Add(new AuthorizeFilter(AdminAreaPolicy.Name));
        }
    }
}
