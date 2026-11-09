using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using HobbyShopApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HobbyShopApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories)
    {
        _categories = categories;
    }

    // Người đã đăng nhập xem được cây danh mục. Khách vãng lai (chưa đăng nhập) nhận 401,
    // đúng với quy tắc "khách chỉ xem sản phẩm nổi bật". Muốn mở công khai thì đổi thành [AllowAnonymous].
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetTree([FromQuery] bool includeInactive = false)
    {
        // Chỉ người có quyền quản lý danh mục mới được xem cả danh mục đang ẩn.
        var allowInactive = includeInactive && CanManage();
        return Ok(await _categories.GetTreeAsync(allowInactive));
    }

    [Authorize]
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, [FromQuery] bool includeInactive = false)
    {
        var allowInactive = includeInactive && CanManage();
        return ToActionResult(await _categories.GetAsync(id, allowInactive));
    }

    [Authorize(Policy = PermissionCodes.CategoryManage)]
    [HttpPost]
    public async Task<IActionResult> Create(CategoryRequest request)
    {
        var result = await _categories.CreateAsync(request, GetActorId());
        return ToActionResult(result, dto => CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto));
    }

    [Authorize(Policy = PermissionCodes.CategoryManage)]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, CategoryRequest request)
    {
        return ToActionResult(await _categories.UpdateAsync(id, request, GetActorId()));
    }

    [Authorize(Policy = PermissionCodes.CategoryManage)]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        return ToActionResult(await _categories.DeleteAsync(id, GetActorId()), _ => NoContent());
    }

    // ---------- Hàm hỗ trợ ----------

    private bool CanManage() => User.HasClaim("permission", PermissionCodes.CategoryManage);

    // Id người đang gọi API, lấy từ claim "sub" trong token.
    private long GetActorId() => long.Parse(User.FindFirst("sub")!.Value);

    // Đổi kết quả của service thành mã HTTP: thành công -> 200 (hoặc kết quả riêng), lỗi -> 400/404/409.
    private IActionResult ToActionResult<T>(ServiceResult<T> result, Func<T, IActionResult>? onSuccess = null)
    {
        if (result.Success)
            return onSuccess is null ? Ok(result.Data) : onSuccess(result.Data!);

        return result.ErrorType switch
        {
            ServiceErrorType.NotFound => NotFound(new { message = result.Error }),
            ServiceErrorType.Conflict => Conflict(new { message = result.Error }),
            _ => BadRequest(new { message = result.Error })
        };
    }
}