using HobbyShopApp.Data;
using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Services;

public interface IWalletService
{
    // Trả Id ví của người dùng; chưa có thì tạo. Trả null nếu không tạo được (ví dụ người dùng không tồn tại).
    Task<long?> GetOrCreateUserWalletIdAsync(long userId);

    Task<ServiceResult<WalletDto>> GetMyWalletAsync(long userId);
    Task<ServiceResult<WalletHistoryDto>> GetMyHistoryAsync(long userId, int page, int pageSize);
}

public class WalletService : IWalletService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    private readonly AppDbContext _db;

    public WalletService(AppDbContext db)
    {
        _db = db;
    }

    // ---------- Lấy hoặc tạo ví ----------

    public async Task<long?> GetOrCreateUserWalletIdAsync(long userId)
    {
        var existingId = await FindWalletIdAsync(userId);
        if (existingId is not null) return existingId;

        var wallet = new Wallet { UserId = userId, WalletType = WalletType.User };
        try
        {
            _db.Wallets.Add(wallet);
            await _db.SaveChangesAsync();
            return wallet.Id;
        }
        catch (DbUpdateException)
        {
            // Hai request cùng tạo ví một lúc: chỉ mục duy nhất trên user_id chặn bản thứ hai.
            // Gỡ bản nháp khỏi DbContext rồi đọc lại ví đã được tạo bởi request kia.
            _db.Entry(wallet).State = EntityState.Detached;
            return await FindWalletIdAsync(userId);
        }
    }

    private Task<long?> FindWalletIdAsync(long userId) =>
        _db.Wallets.AsNoTracking()
            .Where(w => w.UserId == userId && w.WalletType == WalletType.User)
            .Select(w => (long?)w.Id)
            .FirstOrDefaultAsync();

    // ---------- Số dư ----------

    public async Task<ServiceResult<WalletDto>> GetMyWalletAsync(long userId)
    {
        var walletId = await GetOrCreateUserWalletIdAsync(userId);
        if (walletId is null)
            return ServiceResult<WalletDto>.Fail(ServiceErrorType.NotFound, "Không tìm thấy ví.");

        var dto = await _db.Wallets.AsNoTracking()
            .Where(w => w.Id == walletId)
            .Select(w => new WalletDto { Balance = w.Balance, UpdatedAt = w.UpdatedAt })
            .FirstAsync();

        return ServiceResult<WalletDto>.Ok(dto);
    }

    // ---------- Lịch sử giao dịch ----------

    public async Task<ServiceResult<WalletHistoryDto>> GetMyHistoryAsync(long userId, int page, int pageSize)
    {
        // Giá trị ngoài khoảng thì chỉnh về khoảng hợp lệ chứ không báo lỗi.
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var walletId = await GetOrCreateUserWalletIdAsync(userId);
        if (walletId is null)
            return ServiceResult<WalletHistoryDto>.Fail(ServiceErrorType.NotFound, "Không tìm thấy ví.");

        var query = _db.LedgerEntries.AsNoTracking().Where(e => e.WalletId == walletId);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new LedgerEntryDto
            {
                Id = e.Id,
                Amount = e.Amount,
                BalanceAfter = e.BalanceAfter,
                EntryType = e.EntryType,
                RefType = e.RefType,
                RefId = e.RefId,
                Note = e.Note,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();

        return ServiceResult<WalletHistoryDto>.Ok(new WalletHistoryDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }
}