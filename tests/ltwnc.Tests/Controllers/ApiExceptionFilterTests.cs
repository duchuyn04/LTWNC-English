using ltwnc.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace ltwnc.Tests.Controllers;

public class ApiExceptionFilterTests
{
    private static ExceptionContext CreateContext(Exception exception)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());

        return new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = exception
        };
    }

    [Fact]
    public void OnException_UnauthorizedAccessException_Returns403()
    {
        var filter = new ApiExceptionFilter();
        var context = CreateContext(new UnauthorizedAccessException("Không có quyền."));

        filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void OnException_ArgumentException_ReturnsBadRequest()
    {
        var filter = new ApiExceptionFilter();
        var context = CreateContext(new ArgumentException("Dữ liệu không hợp lệ."));

        filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        Assert.IsType<BadRequestObjectResult>(context.Result);
    }

    [Fact]
    public void OnException_KeyNotFoundException_ReturnsNotFound()
    {
        var filter = new ApiExceptionFilter();
        var context = CreateContext(new KeyNotFoundException("Không tìm thấy."));

        filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        Assert.IsType<NotFoundObjectResult>(context.Result);
    }

    [Fact]
    public void OnException_UnhandledException_ReturnsJson500()
    {
        var filter = new ApiExceptionFilter();
        var context = CreateContext(new InvalidOperationException("Lỗi bất ngờ."));

        filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }
}
