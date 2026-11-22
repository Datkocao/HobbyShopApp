using HobbyShopApp.Models;
using HobbyShopApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HobbyShopApp.Controllers;

// Ví của người đang đăng nhập. Chỉ buyer (policy CanShop); Staff/Admin nhận 403.
[ApiController]
[Route("api/wallet")]
[Authorize(Policy = PolicyNames.CanShop)]
public class WalletController : ApiControllerBase
{
    private readonly IWalletService _wallets;

    public WalletController(IWalletService wallets)
    {
        _wallets = wallets;
    }

    // GET /api/wallet : số dư hiện tại.
    [HttpGet]
    public async Task<IActionResult> GetMyWallet() =>
        ToActionResult(await _wallets.GetMyWalletAsync(GetUserId()));

    // GET /api/wallet/transactions?page=1&pageSize=20 : lịch sử giao dịch, mới nhất trước.
    [HttpGet("transactions")]
    public async Task<IActionResult> GetMyTransactions([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        ToActionResult(await _wallets.GetMyHistoryAsync(GetUserId(), page, pageSize));
}