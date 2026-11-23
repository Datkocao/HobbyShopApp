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
    private readonly IOrderQueryService _query;
    private readonly IOrderCancelService _cancel;   // [THÊM]

    public OrdersController(IOrderService orders, IOrderQueryService query, IOrderCancelService cancel)   // [SỬA] thêm tham số cancel
    {
        _orders = orders;
        _query = query;
        _cancel = cancel;   // [THÊM]
    }

    // POST /api/orders : đặt hàng toàn bộ giỏ, thanh toán bằng ví. Thành công trả 201 kèm đơn vừa tạo.
    [HttpPost]
    public async Task<IActionResult> Place([FromBody] PlaceOrderRequest request) =>
        ToActionResult(await _orders.PlaceOrderAsync(GetUserId(), request),
            dto => StatusCode(StatusCodes.Status201Created, dto));

    // GET /api/orders?page=1&pageSize=20 : đơn của tôi, mới nhất trước.
    [HttpGet]
    public async Task<IActionResult> ListMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        ToActionResult(await _query.ListMineAsync(GetUserId(), page, pageSize));

    // GET /api/orders/{id} : chi tiết một đơn của tôi (đơn của người khác trả 404).
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetMine(long id) =>
        ToActionResult(await _query.GetMineAsync(GetUserId(), id));

    // POST /api/orders/{id}/cancel : buyer hủy đơn của mình khi đơn còn PendingConfirmation.
    // Body {"reason": "..."} (không bắt buộc, có thể bỏ trống). Hoàn kho và hoàn toàn bộ tiền vào ví.
    // Đơn của người khác trả 404; đơn đã được xác nhận hoặc đã hủy trả 409.
    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> CancelMine(long id, [FromBody] CancelOrderRequest? request) =>
        ToActionResult(await _cancel.CancelMineAsync(GetUserId(), id, request?.Reason));
}