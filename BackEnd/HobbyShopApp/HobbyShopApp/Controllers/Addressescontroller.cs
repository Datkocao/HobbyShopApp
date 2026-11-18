using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using HobbyShopApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HobbyShopApp.Controllers;

// Sổ địa chỉ của chính người đang đăng nhập. Mọi thao tác đều tự lọc theo Id trong token,
// nên không ai xem hay sửa được địa chỉ của người khác.
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = PolicyNames.CanShop)]
public class AddressesController : ApiControllerBase
{
    private readonly IAddressService _addresses;

    public AddressesController(IAddressService addresses)
    {
        _addresses = addresses;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        return Ok(await _addresses.ListAsync(GetUserId()));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        return ToActionResult(await _addresses.GetAsync(GetUserId(), id));
    }

    [HttpPost]
    public async Task<IActionResult> Create(AddressRequest request)
    {
        var result = await _addresses.CreateAsync(GetUserId(), request);
        return ToActionResult(result, dto => CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, AddressRequest request)
    {
        return ToActionResult(await _addresses.UpdateAsync(GetUserId(), id, request));
    }

    [HttpPut("{id:long}/default")]
    public async Task<IActionResult> SetDefault(long id)
    {
        return ToActionResult(await _addresses.SetDefaultAsync(GetUserId(), id));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        return ToActionResult(await _addresses.DeleteAsync(GetUserId(), id), _ => NoContent());
    }
} 