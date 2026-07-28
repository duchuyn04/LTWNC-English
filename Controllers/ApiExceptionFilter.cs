using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ltwnc.Controllers;

// Chuyển các exception thường gặp của API thành phản hồi JSON với mã HTTP phù hợp.
public class ApiExceptionFilter : ExceptionFilterAttribute
{
    // Bắt lỗi chưa được action xử lý để API không trả về trang lỗi HTML.
    public override void OnException(ExceptionContext context)
    {
        // 1. Lấy exception gốc từ ngữ cảnh của MVC.
        var exception = context.Exception;

        // 2. KeyNotFoundException tương ứng với HTTP 404.
        if (exception is KeyNotFoundException)
        {
            context.Result = new NotFoundObjectResult(new { error = exception.Message });
            context.ExceptionHandled = true;
            return;
        }

        // 3. ArgumentException tương ứng với HTTP 400.
        if (exception is ArgumentException)
        {
            context.Result = new BadRequestObjectResult(new { error = exception.Message });
            context.ExceptionHandled = true;
            return;
        }

        // 4. UnauthorizedAccessException tương ứng với HTTP 403.
        if (exception is UnauthorizedAccessException)
        {
            context.Result = new ObjectResult(new { error = exception.Message })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            context.ExceptionHandled = true;
            return;
        }

        // 5. Các lỗi còn lại trả JSON 500 thay vì rơi về trang lỗi HTML.
        context.Result = new ObjectResult(new { error = "Đã xảy ra lỗi máy chủ." })
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
        context.ExceptionHandled = true;
    }
}
