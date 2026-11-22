using System.Data.Common;
using System.Security.Cryptography;
using HobbyShopApp.Data;
using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HobbyShopApp.Services;

public interface ITopupService
{
    Task<ServiceResult<BankInfoDto>> GetBankInfoAsync();
    Task<ServiceResult<TopupDto>> CreateAsync(long userId, long amount);
    Task<ServiceResult<TopupDto>> UploadProofAsync(long userId, long topupId, IFormFile? file);
    Task<ServiceResult<List<TopupDto>>> ListMineAsync(long userId);
}

public class TopupService : ITopupService
{
    private const long MinAmount = 10_000;          // 10.000đ
    private const long MaxAmount = 50_000_000;      // 50.000.000đ
    private const int MaxPending = 3;               // tối đa 3 yêu cầu đang chờ duyệt mỗi người
    private const long MaxProofBytes = 5 * 1024 * 1024;
    private const int MaxListed = 50;

    // Mã nạp: "HSA" + 8 ký tự ngẫu nhiên, bỏ các ký tự dễ nhầm (0, O, 1, I).
    private const string CodePrefix = "HSA";
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeRandomLength = 8;
    private const int MaxCodeAttempts = 5;

    private const string KeyBankName = "topup.bank_name";
    private const string KeyAccountNumber = "topup.account_number";
    private const string KeyAccountName = "topup.account_name";

    private sealed record ProofType(string Extension, string ContentType);

    private static readonly ProofType Jpeg = new("jpg", "image/jpeg");
    private static readonly ProofType Png = new("png", "image/png");
    private static readonly ProofType WebP = new("webp", "image/webp");

    private readonly AppDbContext _db;
    private readonly IPrivateFileStorage _storage;
    private readonly IAuditService _audit;
    private readonly ILogger<TopupService> _logger;

    public TopupService(AppDbContext db, IPrivateFileStorage storage, IAuditService audit,
                        ILogger<TopupService> logger)
    {
        _db = db;
        _storage = storage;
        _audit = audit;
        _logger = logger;
    }

    // ---------- Thông tin tài khoản nhận tiền ----------

    public async Task<ServiceResult<BankInfoDto>> GetBankInfoAsync()
    {
        var keys = new[] { KeyBankName, KeyAccountNumber, KeyAccountName };

        var map = await _db.SystemSettings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        if (keys.Any(k => !map.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v)))
            return ServiceResult<BankInfoDto>.Fail(ServiceErrorType.Unavailable,
                "Chưa cấu hình thông tin nhận tiền, hãy liên hệ quản trị.");

        return ServiceResult<BankInfoDto>.Ok(new BankInfoDto
        {
            BankName = map[KeyBankName],
            AccountNumber = map[KeyAccountNumber],
            AccountName = map[KeyAccountName]
        });
    }

    // ---------- Tạo yêu cầu nạp ----------

    public async Task<ServiceResult<TopupDto>> CreateAsync(long userId, long amount)
    {
        if (amount < MinAmount || amount > MaxAmount)
            return Fail<TopupDto>(ServiceErrorType.Validation,
                $"Số tiền nạp phải từ {MinAmount:N0}đ đến {MaxAmount:N0}đ.");

        // Giới hạn này chỉ để chống làm rác, không liên quan tới tiền: hai request đồng thời
        // có thể vượt giới hạn một chút, chấp nhận được.
        var pending = await _db.TopupRequests
            .CountAsync(t => t.UserId == userId && t.Status == TopupStatus.Pending);
        if (pending >= MaxPending)
            return Fail<TopupDto>(ServiceErrorType.Conflict,
                $"Bạn đang có {pending} yêu cầu chờ duyệt (tối đa {MaxPending}). Hãy đợi các yêu cầu đó được xử lý.");

        // Mã nạp ngẫu nhiên có thể trùng (rất hiếm): chỉ mục duy nhất sẽ báo, ta thử lại với mã khác.
        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var request = new TopupRequest
            {
                UserId = userId,
                Amount = amount,
                TransferCode = GenerateCode(),
                Status = TopupStatus.Pending
            };

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                _db.TopupRequests.Add(request);
                await _db.SaveChangesAsync();

                // Cần Id của yêu cầu mới để ghi nhật ký nên lưu làm hai lần trong cùng một transaction.
                _audit.Add(userId, "topup.create", nameof(TopupRequest), request.Id.ToString(), null,
                    new { request.Amount, request.TransferCode });
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
                return ServiceResult<TopupDto>.Ok(ToDto(request));
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                _db.Entry(request).State = EntityState.Detached;
                _logger.LogWarning("Mã nạp {Code} bị trùng, thử lại (lần {Attempt})", request.TransferCode, attempt + 1);
            }
        }

        return Fail<TopupDto>(ServiceErrorType.Conflict, "Không tạo được mã nạp, hãy thử lại.");
    }

    // ---------- Tải ảnh chứng từ ----------

    public async Task<ServiceResult<TopupDto>> UploadProofAsync(long userId, long topupId, IFormFile? file)
    {
        // Dữ liệu của người khác trả 404 (không lộ việc nó có tồn tại).
        var request = await _db.TopupRequests.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == topupId && t.UserId == userId);
        if (request is null)
            return Fail<TopupDto>(ServiceErrorType.NotFound, "Không tìm thấy yêu cầu nạp.");

        if (request.Status != TopupStatus.Pending)
            return Fail<TopupDto>(ServiceErrorType.Conflict, "Yêu cầu đã được xử lý, không thể đổi chứng từ.");

        if (file is null || file.Length == 0)
            return Fail<TopupDto>(ServiceErrorType.Validation, "Chưa chọn file ảnh chứng từ.");

        if (file.Length > MaxProofBytes)
            return Fail<TopupDto>(ServiceErrorType.Validation,
                $"Ảnh vượt quá {MaxProofBytes / 1024 / 1024} MB.");

        var type = await DetectProofTypeAsync(file);
        if (type is null)
            return Fail<TopupDto>(ServiceErrorType.Validation, "File không phải ảnh JPEG, PNG hoặc WebP hợp lệ.");

        // Tên file do hệ thống tự đặt (GUID + đuôi theo loại ảnh thật).
        var key = $"topup-proofs/{userId}/{topupId}/{Guid.NewGuid():N}.{type.Extension}";

        try
        {
            await using var stream = file.OpenReadStream();
            await _storage.UploadAsync(stream, key, type.ContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tải chứng từ lên R2 thất bại cho yêu cầu nạp {TopupId}", topupId);
            return Fail<TopupDto>(ServiceErrorType.Unavailable,
                "Không tải được ảnh lên kho lưu trữ, hãy thử lại sau.");
        }

        var oldKey = request.ProofStorageKey;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Chỉ ghi nếu yêu cầu VẪN đang chờ duyệt: nếu Admin vừa duyệt xong thì không đổi chứng từ nữa.
            var rows = await _db.TopupRequests
                .Where(t => t.Id == topupId && t.UserId == userId && t.Status == TopupStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ProofStorageKey, key));

            if (rows == 0)
            {
                await tx.RollbackAsync();
                await DeleteQuietlyAsync(key);
                return Fail<TopupDto>(ServiceErrorType.Conflict, "Yêu cầu đã được xử lý, không thể đổi chứng từ.");
            }

            _audit.Add(userId, "topup.upload_proof", nameof(TopupRequest), topupId.ToString(),
                new { HadProof = oldKey is not null }, new { ProofStorageKey = key });
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        {
            _logger.LogError(ex, "Lưu chứng từ thất bại cho yêu cầu nạp {TopupId}", topupId);
            await DeleteQuietlyAsync(key);
            return Fail<TopupDto>(ServiceErrorType.Conflict, "Không lưu được chứng từ, hãy thử lại.");
        }

        // Đổi chứng từ thì xóa file cũ SAU khi database đã trỏ sang file mới.
        if (oldKey is not null)
            await DeleteQuietlyAsync(oldKey);

        request.ProofStorageKey = key;   // bản AsNoTracking, chỉ để dựng DTO
        return ServiceResult<TopupDto>.Ok(ToDto(request));
    }

    // ---------- Danh sách yêu cầu của tôi ----------

    public async Task<ServiceResult<List<TopupDto>>> ListMineAsync(long userId)
    {
        var items = await _db.TopupRequests.AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Take(MaxListed)
            .ToListAsync();

        return ServiceResult<List<TopupDto>>.Ok(items.Select(ToDto).ToList());
    }

    // ---------- Hàm hỗ trợ ----------

    private static string GenerateCode()
    {
        var chars = new char[CodeRandomLength];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return CodePrefix + new string(chars);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private async Task DeleteQuietlyAsync(string key)
    {
        try
        {
            await _storage.DeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không xóa được file chứng từ trên R2 (cần dọn tay): {Key}", key);
        }
    }

    // Xác định loại ảnh THẬT bằng vài byte đầu file, không tin đuôi file hay Content-Type do máy khách gửi.
    // (Cùng cách làm với ProductImageService; có thể gom về một lớp dùng chung sau.)
    private static async Task<ProofType?> DetectProofTypeAsync(IFormFile file)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);

        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return Jpeg;

        if (read >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return Png;

        if (read >= 12 &&
            header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' &&
            header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
            return WebP;

        return null;
    }

    private static ServiceResult<T> Fail<T>(ServiceErrorType type, string error) =>
        ServiceResult<T>.Fail(type, error);

    private static TopupDto ToDto(TopupRequest t) => new()
    {
        Id = t.Id,
        Amount = t.Amount,
        TransferCode = t.TransferCode,
        Status = t.Status,
        HasProof = t.ProofStorageKey is not null,
        RejectReason = t.RejectReason,
        CreatedAt = t.CreatedAt,
        ReviewedAt = t.ReviewedAt
    };
}