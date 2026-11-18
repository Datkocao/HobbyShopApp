using System.Data.Common;
using System.Text.RegularExpressions;
using HobbyShopApp.Data;
using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Services;

public interface IAddressService
{
    Task<List<AddressDto>> ListAsync(long userId);
    Task<ServiceResult<AddressDto>> GetAsync(long userId, long id);
    Task<ServiceResult<AddressDto>> CreateAsync(long userId, AddressRequest request);
    Task<ServiceResult<AddressDto>> UpdateAsync(long userId, long id, AddressRequest request);
    Task<ServiceResult<AddressDto>> SetDefaultAsync(long userId, long id);
    Task<ServiceResult<bool>> DeleteAsync(long userId, long id);
}

public class AddressService : IAddressService
{
    private const int MaxAddresses = 10;

    private static readonly Regex PhoneRegex = new(@"^(0|\+84)\d{9,10}$", RegexOptions.Compiled);

    private readonly AppDbContext _db;

    public AddressService(AppDbContext db)
    {
        _db = db;
    }

    // ---------- Đọc ----------

    public async Task<List<AddressDto>> ListAsync(long userId)
    {
        var addresses = await _db.Addresses.AsNoTracking()
            .Where(a => a.UserId == userId && !a.IsDeleted)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.Id)   // mặc định lên đầu
            .ToListAsync();

        return addresses.Select(ToDto).ToList();
    }

    public async Task<ServiceResult<AddressDto>> GetAsync(long userId, long id)
    {
        // Luôn lọc theo UserId: địa chỉ của người khác trả về 404 như thể không tồn tại.
        var address = await _db.Addresses.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId && !a.IsDeleted);

        return address is null ? NotFound() : ServiceResult<AddressDto>.Ok(ToDto(address));
    }

    // ---------- Tạo ----------

    public async Task<ServiceResult<AddressDto>> CreateAsync(long userId, AddressRequest request)
    {
        var phone = NormalizePhone(request.Phone);
        if (!PhoneRegex.IsMatch(phone))
            return ServiceResult<AddressDto>.Fail(ServiceErrorType.Validation, "Số điện thoại không hợp lệ.");

        var count = await _db.Addresses.CountAsync(a => a.UserId == userId && !a.IsDeleted);
        if (count >= MaxAddresses)
            return ServiceResult<AddressDto>.Fail(ServiceErrorType.Validation,
                $"Mỗi người chỉ lưu tối đa {MaxAddresses} địa chỉ.");

        // Địa chỉ đầu tiên luôn là mặc định.
        var makeDefault = request.IsDefault || count == 0;

        var address = new Address
        {
            UserId = userId,
            RecipientName = request.RecipientName.Trim(),
            Phone = phone,
            Province = request.Province.Trim(),
            District = request.District.Trim(),
            Ward = request.Ward.Trim(),
            DetailLine = request.DetailLine.Trim(),
            IsDefault = makeDefault,
        };

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Bỏ cờ mặc định của địa chỉ cũ TRƯỚC, rồi mới thêm địa chỉ mặc định mới
            // (chỉ mục duy nhất một phần không cho phép có hai địa chỉ mặc định cùng lúc).
            if (makeDefault)
                await ClearDefaultAsync(userId);

            _db.Addresses.Add(address);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex) when (IsDbError(ex))
        {
            return ServiceResult<AddressDto>.Fail(ServiceErrorType.Conflict,
                "Không lưu được địa chỉ, hãy thử lại.");
        }

        return ServiceResult<AddressDto>.Ok(ToDto(address));
    }

    // ---------- Sửa ----------

    public async Task<ServiceResult<AddressDto>> UpdateAsync(long userId, long id, AddressRequest request)
    {
        var address = await _db.Addresses
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId && !a.IsDeleted);
        if (address is null)
            return NotFound();

        var phone = NormalizePhone(request.Phone);
        if (!PhoneRegex.IsMatch(phone))
            return ServiceResult<AddressDto>.Fail(ServiceErrorType.Validation, "Số điện thoại không hợp lệ.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Chỉ có thể BẬT cờ mặc định ở đây. Muốn đổi địa chỉ mặc định thì đặt địa chỉ khác làm mặc định
            // (người dùng luôn phải có một địa chỉ mặc định nếu còn địa chỉ).
            if (request.IsDefault && !address.IsDefault)
            {
                await ClearDefaultAsync(userId);
                address.IsDefault = true;
            }

            address.RecipientName = request.RecipientName.Trim();
            address.Phone = phone;
            address.Province = request.Province.Trim();
            address.District = request.District.Trim();
            address.Ward = request.Ward.Trim();
            address.DetailLine = request.DetailLine.Trim();
            address.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex) when (IsDbError(ex))
        {
            return ServiceResult<AddressDto>.Fail(ServiceErrorType.Conflict,
                "Không lưu được địa chỉ, hãy thử lại.");
        }

        return ServiceResult<AddressDto>.Ok(ToDto(address));
    }

    // ---------- Đặt làm mặc định ----------

    public async Task<ServiceResult<AddressDto>> SetDefaultAsync(long userId, long id)
    {
        var address = await _db.Addresses.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId && !a.IsDeleted);
        if (address is null)
            return NotFound();

        if (address.IsDefault)
            return ServiceResult<AddressDto>.Ok(ToDto(address));

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            await ClearDefaultAsync(userId);

            await _db.Addresses
                .Where(a => a.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.IsDefault, true)
                    .SetProperty(a => a.UpdatedAt, DateTime.UtcNow));

            await tx.CommitAsync();
        }
        catch (Exception ex) when (IsDbError(ex))
        {
            return ServiceResult<AddressDto>.Fail(ServiceErrorType.Conflict,
                "Không đổi được địa chỉ mặc định, hãy thử lại.");
        }

        address.IsDefault = true;
        return ServiceResult<AddressDto>.Ok(ToDto(address));
    }

    // ---------- Xóa (xóa mềm) ----------

    public async Task<ServiceResult<bool>> DeleteAsync(long userId, long id)
    {
        var address = await _db.Addresses
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId && !a.IsDeleted);
        if (address is null)
            return ServiceResult<bool>.Fail(ServiceErrorType.NotFound, "Không tìm thấy địa chỉ.");

        var wasDefault = address.IsDefault;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            address.IsDeleted = true;
            address.IsDefault = false;
            address.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Vừa xóa địa chỉ mặc định thì chuyển cờ sang địa chỉ mới nhất còn lại (nếu có).
            if (wasDefault)
            {
                var next = await _db.Addresses
                    .Where(a => a.UserId == userId && !a.IsDeleted)
                    .OrderByDescending(a => a.Id)
                    .FirstOrDefaultAsync();

                if (next is not null)
                {
                    next.IsDefault = true;
                    next.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }

            await tx.CommitAsync();
        }
        catch (Exception ex) when (IsDbError(ex))
        {
            return ServiceResult<bool>.Fail(ServiceErrorType.Conflict, "Không xóa được địa chỉ, hãy thử lại.");
        }

        return ServiceResult<bool>.Ok(true);
    }

    // ---------- Hàm hỗ trợ ----------

    // Bỏ cờ mặc định của mọi địa chỉ đang là mặc định của người dùng (một câu UPDATE duy nhất).
    private Task<int> ClearDefaultAsync(long userId) =>
        _db.Addresses
            .Where(a => a.UserId == userId && a.IsDefault && !a.IsDeleted)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false));

    private static string NormalizePhone(string phone) => Regex.Replace(phone, @"[\s.\-]", "");

    // Lỗi từ database (vi phạm chỉ mục duy nhất, mất kết nối...). ExecuteUpdate ném lỗi gốc của nhà cung cấp,
    // còn SaveChanges ném DbUpdateException, nên bắt cả hai.
    private static bool IsDbError(Exception ex) => ex is DbUpdateException or DbException;

    private static ServiceResult<AddressDto> NotFound() =>
        ServiceResult<AddressDto>.Fail(ServiceErrorType.NotFound, "Không tìm thấy địa chỉ.");

    private static AddressDto ToDto(Address a) => new()
    {
        Id = a.Id,
        RecipientName = a.RecipientName,
        Phone = a.Phone,
        Province = a.Province,
        District = a.District,
        Ward = a.Ward,
        DetailLine = a.DetailLine,
        FullAddress = $"{a.DetailLine}, {a.Ward}, {a.District}, {a.Province}",
        IsDefault = a.IsDefault
    };
}