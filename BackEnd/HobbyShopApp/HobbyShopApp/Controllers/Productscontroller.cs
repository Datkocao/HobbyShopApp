using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using HobbyShopApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HobbyShopApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _products;

    public ProductsController(IProductService products)
    {
        _products = products;
    }

    // Công khai: khách vãng lai chỉ xem được sản phẩm nổi bật.
    [AllowAnonymous]
    [HttpGet("featured")]
    public async Task<IActionResult> GetFeatured([FromQuery] int take = 12)
    {
        return Ok(await _products.GetFeaturedAsync(take));
    }

    // Tìm kiếm, lọc, sắp xếp, phân trang: phải đăng nhập.
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] ProductQuery query)
    {
        return ToActionResult(await _products.GetListAsync(query, CanManage()));
    }

    // Khách vãng lai xem được chi tiết sản phẩm nổi bật; người đã đăng nhập xem mọi sản phẩm đang bán.
    // Dù để AllowAnonymous, nếu request có token hợp lệ thì User vẫn được nhận diện.
    [AllowAnonymous]
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        return ToActionResult(await _products.GetAsync(id, CanManage(), isAuthenticated));
    }

    [Authorize(Policy = PermissionCodes.ProductManage)]
    [HttpPost]
    public async Task<IActionResult> Create(ProductCreateRequest request)
    {
        var result = await _products.CreateAsync(request, GetActorId());
        return ToActionResult(result, dto => CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto));
    }

    [Authorize(Policy = PermissionCodes.ProductManage)]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, ProductUpdateRequest request)
    {
        return ToActionResult(await _products.UpdateAsync(id, request, GetActorId()));
    }

    // Đổi tồn kho theo mức tăng/giảm (không đặt số tuyệt đối) để an toàn khi nhiều người thao tác cùng lúc.
    [Authorize(Policy = PermissionCodes.ProductManage)]
    [HttpPatch("{id:long}/stock")]
    public async Task<IActionResult> AdjustStock(long id, StockAdjustRequest request)
    {
        return ToActionResult(await _products.AdjustStockAsync(id, request, GetActorId()));
    }

    [Authorize(Policy = PermissionCodes.ProductManage)]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        return ToActionResult(await _products.DeleteAsync(id, GetActorId()), _ => NoContent());
    }

    // ---------- Hàm hỗ trợ (giống CategoriesController, sẽ gom vào lớp dùng chung sau) ----------

    private bool CanManage() => User.HasClaim("permission", PermissionCodes.ProductManage);

    private long GetActorId() => long.Parse(User.FindFirst("sub")!.Value);

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