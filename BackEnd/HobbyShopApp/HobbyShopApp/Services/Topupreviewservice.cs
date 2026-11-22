using System.Data.Common;
using HobbyShopApp.Data;
using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Services;

public interface ITopupReviewService
{
    Task<ServiceResult<AdminTopupDetailDto>> ApproveAsync(long topupId, long actorId);
    Task<ServiceResult<AdminTopupDetailDto>> RejectAsync(long topupId, string? reason, long actorId);
}

public class TopupReviewService : ITopupReviewService
{
    private const int MaxReasonLength = 500;

    private readonly AppDbContext _db;
    private readonly IWalletService _wallets;
    private readonly ITopupAdminService _adminView;
    private readonly IAuditService _audit;
    private readonly ILogger<TopupReviewService> _logger;

    public TopupReviewService(AppDbContext db, IWalletService wallets, ITopupAdminService adminView,
                              IAuditService audit, ILogger<TopupReviewService> logger)
    {
        _db = db;
        _wallets = wallets;
        _adminView = adminView;
        _audit = audit;
        _logger = logger;
    }

    // ---------- Duyệt: cộng tiền vào ví ----------

    public async Task<ServiceResult<AdminTopupDetailDto>> ApproveAsync(long topupId, long actorId)
    {
        var request = await _db.TopupRequests.AsNoTracking().FirstOrDefaultAsync(t => t.Id == topupId);
        if (request is null)
            return Fail(ServiceErrorType.NotFound, "Không tìm thấy yêu cầu nạp.");

        if (request.Status != TopupStatus.Pending)
            return Fail(ServiceErrorType.Conflict, $"Yêu cầu đã được xử lý (trạng thái: {request.Status}).");

        if (request.ProofStorageKey is null)
            return Fail(ServiceErrorType.Validation, "Yêu cầu chưa có ảnh chứng từ, chưa thể duyệt.");

        // Bảo đảm ví của người dùng đã tồn tại (tự tạo nếu chưa có). Việc này nằm NGOÀI transaction bên dưới.
        var walletIdOrNull = await _wallets.GetOrCreateUserWalletIdAsync(request.UserId);
        if (walletIdOrNull is null)
            return Fail(ServiceErrorType.NotFound, "Không tìm thấy ví của người dùng.");

        var walletId = walletIdOrNull.Value;
        var amount = request.Amount;
        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Bước 1: "giành quyền" duyệt yêu cầu bằng MỘT câu UPDATE có điều kiện (chỉ khi còn Pending).
            // Hai người bấm duyệt cùng lúc (hoặc bấm đúp): chỉ một câu UPDATE thắng, câu còn lại được 0 dòng
            // và dừng ở đây, nên tiền không bao giờ bị cộng hai lần.
            var claimed = await _db.TopupRequests
                .Where(t => t.Id == topupId && t.Status == TopupStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, TopupStatus.Approved)
                    .SetProperty(t => t.ReviewedByUserId, (long?)actorId)
                    .SetProperty(t => t.ReviewedAt, (DateTime?)now));

            if (claimed == 0)
            {
                await tx.RollbackAsync();
                return Fail(ServiceErrorType.Conflict, "Yêu cầu vừa được xử lý bởi người khác.");
            }

            // Bước 2: cộng số dư bằng câu UPDATE "balance = balance + số tiền", không đọc số cũ rồi cộng trong code.
            await _db.Wallets
                .Where(w => w.Id == walletId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(w => w.Balance, w => w.Balance + amount)
                    .SetProperty(w => w.UpdatedAt, now));

            // Đọc lại số dư ngay sau khi cộng. Câu UPDATE trên đang khóa dòng ví cho tới khi transaction kết thúc,
            // nên không ai chen vào được và số đọc ra chính là số dư sau giao dịch này.
            var balanceAfter = await _db.Wallets.AsNoTracking()
                .Where(w => w.Id == walletId)
                .Select(w => w.Balance)
                .FirstAsync();

            // Bước 3: ghi sổ giao dịch (chỉ thêm, không sửa). Khóa chống ghi trùng gắn với Id yêu cầu nạp.
            var entry = new LedgerEntry
            {
                WalletId = walletId,
                Amount = amount,
                BalanceAfter = balanceAfter,
                EntryType = LedgerEntryType.Topup,
                RefType = nameof(TopupRequest),
                RefId = topupId,
                IdempotencyKey = $"topup-approve:{topupId}",
                Note = $"Nạp ví, mã {request.TransferCode}",
                CreatedByUserId = actorId,
                CreatedAt = now
            };
            _db.LedgerEntries.Add(entry);
            await _db.SaveChangesAsync();

            // Bước 4: gắn dòng sổ vừa tạo vào yêu cầu nạp, rồi ghi nhật ký thao tác.
            var entryId = entry.Id;
            await _db.TopupRequests
                .Where(t => t.Id == topupId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.LedgerEntryId, (long?)entryId));

            _audit.Add(actorId, "topup.approve", nameof(TopupRequest), topupId.ToString(),
                new { Status = "Pending" },
                new { Status = "Approved", Amount = amount, WalletId = walletId, LedgerEntryId = entryId, BalanceAfter = balanceAfter });
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        {
            // Lỗi giữa chừng: transaction tự hoàn tác, yêu cầu vẫn Pending và ví không đổi.
            _logger.LogError(ex, "Duyệt yêu cầu nạp {TopupId} thất bại", topupId);
            return Fail(ServiceErrorType.Conflict, "Không duyệt được yêu cầu, hãy thử lại.");
        }

        return await _adminView.GetAsync(topupId);
    }

    // ---------- Từ chối ----------

    public async Task<ServiceResult<AdminTopupDetailDto>> RejectAsync(long topupId, string? reason, long actorId)
    {
        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason))
            return Fail(ServiceErrorType.Validation, "Cần nhập lý do từ chối.");
        if (reason.Length > MaxReasonLength)
            return Fail(ServiceErrorType.Validation, $"Lý do từ chối tối đa {MaxReasonLength} ký tự.");

        var request = await _db.TopupRequests.AsNoTracking().FirstOrDefaultAsync(t => t.Id == topupId);
        if (request is null)
            return Fail(ServiceErrorType.NotFound, "Không tìm thấy yêu cầu nạp.");

        if (request.Status != TopupStatus.Pending)
            return Fail(ServiceErrorType.Conflict, $"Yêu cầu đã được xử lý (trạng thái: {request.Status}).");

        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var claimed = await _db.TopupRequests
                .Where(t => t.Id == topupId && t.Status == TopupStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, TopupStatus.Rejected)
                    .SetProperty(t => t.ReviewedByUserId, (long?)actorId)
                    .SetProperty(t => t.ReviewedAt, (DateTime?)now)
                    .SetProperty(t => t.RejectReason, reason));

            if (claimed == 0)
            {
                await tx.RollbackAsync();
                return Fail(ServiceErrorType.Conflict, "Yêu cầu vừa được xử lý bởi người khác.");
            }

            _audit.Add(actorId, "topup.reject", nameof(TopupRequest), topupId.ToString(),
                new { Status = "Pending" },
                new { Status = "Rejected", Reason = reason });
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        {
            _logger.LogError(ex, "Từ chối yêu cầu nạp {TopupId} thất bại", topupId);
            return Fail(ServiceErrorType.Conflict, "Không từ chối được yêu cầu, hãy thử lại.");
        }

        return await _adminView.GetAsync(topupId);
    }

    private static ServiceResult<AdminTopupDetailDto> Fail(ServiceErrorType type, string error) =>
        ServiceResult<AdminTopupDetailDto>.Fail(type, error);
}