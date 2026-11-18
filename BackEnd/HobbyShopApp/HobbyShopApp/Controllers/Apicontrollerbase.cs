using HobbyShopApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace HobbyShopApp.Controllers;

// Lớp cha dùng chung cho các controller mới: gom hai hàm lặp lại ở mọi controller.
// (Các controller cũ vẫn có bản sao riêng; có thể chuyển sang lớp này sau khi mọi thứ đã ổn.)
public abstract class ApiControllerBase : ControllerBase
{
    // Id người đang gọi API, lấy từ claim "sub" trong token.
    protected long GetUserId() => long.Parse(User.FindFirst("sub")!.Value);

    // Đổi kết quả của service thành mã HTTP: thành công -> 200 (hoặc kết quả riêng), lỗi -> 400/404/409/503.
    protected IActionResult ToActionResult<T>(ServiceResult<T> result, Func<T, IActionResult>? onSuccess = null)
    {
        if (result.Success)
            return onSuccess is null ? Ok(result.Data) : onSuccess(result.Data!);

        return result.ErrorType switch
        {
            ServiceErrorType.NotFound => NotFound(new { message = result.Error }),
            ServiceErrorType.Conflict => Conflict(new { message = result.Error }),
            ServiceErrorType.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = result.Error }),
            _ => BadRequest(new { message = result.Error })
        };
    }
}