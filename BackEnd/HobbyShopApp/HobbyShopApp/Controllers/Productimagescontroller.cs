using HobbyShopApp.Models;
using HobbyShopApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HobbyShopApp.Controllers;

// Quản lý ảnh của một sản phẩm: chỉ người có quyền product.manage (Staff, Admin).
[ApiController]
[Route("api/products/{productId:long}/images")]
[Authorize(Policy = PermissionCodes.ProductManage)]
public class ProductImagesController : ControllerBase
{
    private readonly IProductImageService _images;

    public ProductImagesController(IProductImageService images)
    {
        _images = images;
    }

    // Tải lên một hoặc nhiều ảnh (multipart/form-data, trường tên "files").
    // RequestSizeLimit: từ chối sớm các request quá lớn trước khi đọc hết nội dung.
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Upload(long productId, [FromForm] List<IFormFile> files)
    {
        var result = await _images.UploadAsync(productId, files, GetActorId());
        return ToActionResult(result, data => StatusCode(StatusCodes.Status201Created, data));
    }

    [HttpPut("{imageId:long}/primary")]
    public async Task<IActionResult> SetPrimary(long productId, long imageId)
    {
        return ToActionResult(await _images.SetPrimaryAsync(productId, imageId, GetActorId()));
    }

    [HttpDelete("{imageId:long}")]
    public async Task<IActionResult> Delete(long productId, long imageId)
    {
        return ToActionResult(await _images.DeleteAsync(productId, imageId, GetActorId()), _ => NoContent());
    }

    private long GetActorId() => long.Parse(User.FindFirst("sub")!.Value);

    private IActionResult ToActionResult<T>(ServiceResult<T> result, Func<T, IActionResult>? onSuccess = null)
    {
        if (result.Success)
            return onSuccess is null ? Ok(result.Data) : onSuccess(result.Data!);

        return result.ErrorType switch
        {
            ServiceErrorType.NotFound => NotFound(new { message = result.Error }),
            ServiceErrorType.Conflict => Conflict(new { message = result.Error }),
            // Kho lưu trữ (R2) đang lỗi: 503 báo "dịch vụ tạm thời không dùng được".
            ServiceErrorType.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = result.Error }),
            _ => BadRequest(new { message = result.Error })
        };
    }
}