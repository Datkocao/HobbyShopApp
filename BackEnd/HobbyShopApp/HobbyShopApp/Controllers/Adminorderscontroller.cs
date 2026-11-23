using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using HobbyShopApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HobbyShopApp.Controllers;

// Quản trị xem và xử lý đơn hàng. Cần permission order.view_all (Staff và Admin có).
[ApiController]
[Route("api/admin/orders")]
[Authorize(Policy = PermissionCodes.OrderViewAll)]
public class AdminOrdersController : ApiControllerBase
{
    private readonly IOrderQueryService _query;
    private readonly IOrderStatusService _status;
    private readonly IOrderCancelService _cancel;   // [THÊM]

    public AdminOrdersController(IOrderQueryService query, IOrderStatusService status, IOrderCancelService cancel)   // [SỬA] thêm tham số cancel
    {
        _query = query;
        _status = status;
        _cancel = cancel;   // [THÊM]
    }

    // GET /api/admin/orders?status=PendingConfirmation&page=1&pageSize=20
    // status bỏ trống thì lấy mọi trạng thái; mới nhất trước.
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] OrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToActionResult(await _query.ListAllAsync(status, page, pageSize));

    // GET /api/admin/orders/{id} : chi tiết một đơn kèm thông tin người mua.
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id) =>
        ToActionResult(await _query.GetAnyAsync(id));

    // POST /api/admin/orders/{id}/status : chuyển đơn sang bước kế tiếp, body {"status": "Confirmed"}.
    // Cần thêm permission order.update_status (cộng với order.view_all của cả controller).
    [HttpPost("{id:long}/status")]
    [Authorize(Policy = PermissionCodes.OrderUpdateStatus)]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateOrderStatusRequest request) =>
        ToActionResult(await _status.UpdateStatusAsync(id, request.Status, GetUserId()));

    // POST /api/admin/orders/{id}/cancel : Staff/Admin hủy đơn đang PendingConfirmation, hoàn kho và hoàn tiền.
    // Body {"reason": "..."} BẮT BUỘC (tối đa 500 ký tự). Cần permission order.update_status.
    [HttpPost("{id:long}/cancel")]
    [Authorize(Policy = PermissionCodes.OrderUpdateStatus)]
    public async Task<IActionResult> Cancel(long id, [FromBody] CancelOrderRequest request) =>
        ToActionResult(await _cancel.CancelAsStaffAsync(GetUserId(), id, request.Reason));
}