using HobbyShopApp.Data;
using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Services;

public interface ITopupAdminService
{
    Task<ServiceResult<AdminTopupListDto>> ListAsync(TopupStatus status, int page, int pageSize);
    Task<ServiceResult<AdminTopupDetailDto>> GetAsync(long id);
}

public class TopupAdminService : ITopupAdminService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    // Link xem ảnh chứng từ chỉ sống ngắn: hết hạn thì gọi lại API chi tiết để lấy link mới.
    private static readonly TimeSpan ProofUrlLifetime = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly IPrivateFileStorage _storage;

    public TopupAdminService(AppDbContext db, IPrivateFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    // ---------- Danh sách ----------

    public async Task<ServiceResult<AdminTopupListDto>> ListAsync(TopupStatus status, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = DefaultPageSize;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var query = _db.TopupRequests.AsNoTracking().Where(t => t.Status == status);

        var total = await query.CountAsync();

        // Yêu cầu đang chờ thì xử lý theo thứ tự đến trước làm trước; các trạng thái khác xem mới nhất trước.
        var ordered = status == TopupStatus.Pending
            ? query.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            : query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id);

        var items = await ordered
            .Include(t => t.User)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return ServiceResult<AdminTopupListDto>.Ok(new AdminTopupListDto
        {
            Items = items.Select(t => Fill(new AdminTopupDto(), t)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    // ---------- Chi tiết ----------

    public async Task<ServiceResult<AdminTopupDetailDto>> GetAsync(long id)
    {
        var t = await _db.TopupRequests.AsNoTracking()
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (t is null)
            return ServiceResult<AdminTopupDetailDto>.Fail(ServiceErrorType.NotFound, "Không tìm thấy yêu cầu nạp.");

        var dto = Fill(new AdminTopupDetailDto(), t);

        if (t.ProofStorageKey is not null)
        {
            dto.ProofUrl = _storage.GetTemporaryUrl(t.ProofStorageKey, ProofUrlLifetime);
            dto.ProofUrlExpiresAt = DateTime.UtcNow.Add(ProofUrlLifetime);
        }

        return ServiceResult<AdminTopupDetailDto>.Ok(dto);
    }

    // ---------- Hàm hỗ trợ ----------

    private static TDto Fill<TDto>(TDto dto, TopupRequest t) where TDto : AdminTopupDto
    {
        dto.Id = t.Id;
        dto.UserId = t.UserId;
        dto.UserEmail = t.User.Email;
        dto.UserFullName = t.User.FullName;
        dto.Amount = t.Amount;
        dto.TransferCode = t.TransferCode;
        dto.Status = t.Status;
        dto.HasProof = t.ProofStorageKey is not null;
        dto.RejectReason = t.RejectReason;
        dto.CreatedAt = t.CreatedAt;
        dto.ReviewedAt = t.ReviewedAt;
        dto.ReviewedByUserId = t.ReviewedByUserId;
        return dto;
    }
}