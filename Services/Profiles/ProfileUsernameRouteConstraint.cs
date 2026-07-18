using Microsoft.AspNetCore.Routing;

namespace ltwnc.Services.Profiles;

public sealed class ProfileUsernameRouteConstraint : IRouteConstraint
{
    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        return values.TryGetValue(routeKey, out object? routeValue) &&
            UsernamePolicy.IsValid(Convert.ToString(routeValue));
    }
}
