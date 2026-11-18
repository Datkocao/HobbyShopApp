using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using HobbyShopApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HobbyShopApp.Controllers;

// Giỏ hàng của chính người đang đăng nhập. STAFF và ADMIN không có giỏ hàng (policy CanShop trả 403).
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = PolicyNames.CanShop)]
public class CartController : ApiControllerBase
{
    private readonly ICartService _cart;

    public CartController(ICartService cart)
    {
        _cart = cart;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(await _cart.GetAsync(GetUserId()));
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem(CartItemRequest request)
    {
        return ToActionResult(await _cart.AddItemAsync(GetUserId(), request));
    }

    [HttpPut("items/{productId:long}")]
    public async Task<IActionResult> SetQuantity(long productId, CartQuantityRequest request)
    {
        return ToActionResult(await _cart.SetQuantityAsync(GetUserId(), productId, request.Quantity));
    }

    [HttpDelete("items/{productId:long}")]
    public async Task<IActionResult> RemoveItem(long productId)
    {
        return ToActionResult(await _cart.RemoveItemAsync(GetUserId(), productId));
    }

    [HttpDelete]
    public async Task<IActionResult> Clear()
    {
        return Ok(await _cart.ClearAsync(GetUserId()));
    }
}