using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using HobbyShopApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HobbyShopApp.Controllers;

// Đơn hàng của người đang đăng nhập. Chỉ buyer (policy CanShop); Staff/Admin nhận 403.
[ApiController]
[Route("api/orders")]
[Authorize(Policy = PolicyNames.CanShop)]
public class OrdersController : ApiControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    // POST /api/orders : đặt hàng toàn bộ giỏ, thanh toán bằng ví. Thành công trả 201 kèm đơn vừa tạo.
    [HttpPost]
    public async Task<IActionResult> Place([FromBody] PlaceOrderRequest request) =>
        ToActionResult(await _orders.PlaceOrderAsync(GetUserId(), request),
            dto => StatusCode(StatusCodes.Status201Created, dto));
}