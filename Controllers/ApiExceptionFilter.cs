using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ltwnc.Controllers;

public class ApiExceptionFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var exception = context.Exception;

        if (exception is KeyNotFoundException)
        {
            context.Result = new NotFoundObjectResult(new { error = exception.Message });
            context.ExceptionHandled = true;
            return;
        }

        if (exception is ArgumentException)
        {
            context.Result = new BadRequestObjectResult(new { error = exception.Message });
            context.ExceptionHandled = true;
            return;
        }

        if (exception is UnauthorizedAccessException)
        {
            context.Result = new ObjectResult(new { error = exception.Message })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            context.ExceptionHandled = true;
            return;
        }

        base.OnException(context);
    }
}
