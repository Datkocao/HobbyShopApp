using HobbyShopApp.Models;
using HobbyShopApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HobbyShopApp.DTOs;

namespace HobbyShopApp.Controllers;

// Quản trị xem các yêu cầu nạp ví. Cần permission wallet.approve_topup (Staff và Admin có).
[ApiController]
[Route("api/admin/topups")]
[Authorize(Policy = PermissionCodes.WalletApproveTopup)]
public class AdminTopupsController : ApiControllerBase
{
    private readonly ITopupAdminService _topups;
    private readonly ITopupReviewService _review;

    public AdminTopupsController(ITopupAdminService topups, ITopupReviewService review)
    {
        _topups = topups;
        _review = review;
    }

    // GET /api/admin/topups?status=Pending&page=1&pageSize=20
    // status nhận Pending, Approved hoặc Rejected; bỏ trống thì mặc định là Pending.
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] TopupStatus status = TopupStatus.Pending,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToActionResult(await _topups.ListAsync(status, page, pageSize));

    // GET /api/admin/topups/{id} : chi tiết kèm link xem ảnh chứng từ (hết hạn sau 5 phút).
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id) =>
        ToActionResult(await _topups.GetAsync(id));
    // POST /api/admin/topups/{id}/approve : duyệt, cộng tiền vào ví người nạp.
    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id) =>
        ToActionResult(await _review.ApproveAsync(id, GetUserId()));

    // POST /api/admin/topups/{id}/reject : từ chối, bắt buộc có lý do.
    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] RejectTopupRequest request) =>
        ToActionResult(await _review.RejectAsync(id, request.Reason, GetUserId()));
}