using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using HobbyShopApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HobbyShopApp.Controllers;

// Yêu cầu nạp ví của người đang đăng nhập. Chỉ buyer (policy CanShop); Staff/Admin nhận 403.
[ApiController]
[Route("api/topups")]
[Authorize(Policy = PolicyNames.CanShop)]
public class TopupsController : ApiControllerBase
{
    private readonly ITopupService _topups;

    public TopupsController(ITopupService topups)
    {
        _topups = topups;
    }

    // GET /api/topups/bank-info : tài khoản nhận tiền để người dùng chuyển khoản.
    [HttpGet("bank-info")]
    public async Task<IActionResult> GetBankInfo() =>
        ToActionResult(await _topups.GetBankInfoAsync());

    // GET /api/topups : các yêu cầu nạp của tôi, mới nhất trước.
    [HttpGet]
    public async Task<IActionResult> ListMine() =>
        ToActionResult(await _topups.ListMineAsync(GetUserId()));

    // POST /api/topups : tạo yêu cầu, nhận về mã nạp để ghi vào nội dung chuyển khoản.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTopupRequest request) =>
        ToActionResult(await _topups.CreateAsync(GetUserId(), request.Amount),
            dto => StatusCode(StatusCodes.Status201Created, dto));

    // POST /api/topups/{id}/proof : tải (hoặc thay) ảnh chứng từ chuyển khoản.
    [HttpPost("{id:long}/proof")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadProof(long id, IFormFile file) =>
        ToActionResult(await _topups.UploadProofAsync(GetUserId(), id, file));
}