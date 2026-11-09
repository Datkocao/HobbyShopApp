using HobbyShopApp.Data;
using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Services;

public interface IProductImageService
{
    Task<ServiceResult<List<ProductImageDto>>> UploadAsync(long productId, IReadOnlyList<IFormFile> files, long actorId);
    Task<ServiceResult<ProductImageDto>> SetPrimaryAsync(long productId, long imageId, long actorId);
    Task<ServiceResult<bool>> DeleteAsync(long productId, long imageId, long actorId);
}

public class ProductImageService : IProductImageService
{
    private const long MaxFileBytes = 5 * 1024 * 1024;   // 5 MB mỗi ảnh
    private const int MaxFilesPerRequest = 5;
    private const int MaxImagesPerProduct = 10;

    private sealed record ImageType(string Extension, string ContentType);

    private static readonly ImageType Jpeg = new("jpg", "image/jpeg");
    private static readonly ImageType Png = new("png", "image/png");
    private static readonly ImageType WebP = new("webp", "image/webp");

    private readonly AppDbContext _db;
    private readonly IImageStorage _storage;
    private readonly IImageUrlBuilder _urls;
    private readonly IAuditService _audit;
    private readonly ILogger<ProductImageService> _logger;

    public ProductImageService(AppDbContext db, IImageStorage storage, IImageUrlBuilder urls,
                               IAuditService audit, ILogger<ProductImageService> logger)
    {
        _db = db;
        _storage = storage;
        _urls = urls;
        _audit = audit;
        _logger = logger;
    }

    // ---------- Tải ảnh lên ----------

    public async Task<ServiceResult<List<ProductImageDto>>> UploadAsync(
        long productId, IReadOnlyList<IFormFile> files, long actorId)
    {
        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
        if (product is null)
            return Fail<List<ProductImageDto>>(ServiceErrorType.NotFound, "Không tìm thấy sản phẩm.");

        if (product.SellerUserId is not null)
            return Fail<List<ProductImageDto>>(ServiceErrorType.Validation,
                "Chỉ quản lý ảnh của sản phẩm shop tại đây.");

        if (files.Count == 0)
            return Fail<List<ProductImageDto>>(ServiceErrorType.Validation, "Chưa chọn file ảnh.");

        if (files.Count > MaxFilesPerRequest)
            return Fail<List<ProductImageDto>>(ServiceErrorType.Validation,
                $"Mỗi lần tải lên tối đa {MaxFilesPerRequest} ảnh.");

        var existingCount = await _db.ProductImages.CountAsync(i => i.ProductId == productId);
        if (existingCount + files.Count > MaxImagesPerProduct)
            return Fail<List<ProductImageDto>>(ServiceErrorType.Validation,
                $"Mỗi sản phẩm tối đa {MaxImagesPerProduct} ảnh (hiện có {existingCount}).");

        // Bước 1: kiểm tra TẤT CẢ file trước khi tải bất kỳ file nào, để không bị tải dở dang.
        var prepared = new List<(IFormFile File, ImageType Type)>();
        foreach (var file in files)
        {
            var displayName = Path.GetFileName(file.FileName);

            if (file.Length == 0)
                return Fail<List<ProductImageDto>>(ServiceErrorType.Validation, $"File '{displayName}' bị rỗng.");

            if (file.Length > MaxFileBytes)
                return Fail<List<ProductImageDto>>(ServiceErrorType.Validation,
                    $"File '{displayName}' vượt quá {MaxFileBytes / 1024 / 1024} MB.");

            var type = await DetectImageTypeAsync(file);
            if (type is null)
                return Fail<List<ProductImageDto>>(ServiceErrorType.Validation,
                    $"File '{displayName}' không phải ảnh JPEG, PNG hoặc WebP hợp lệ.");

            prepared.Add((file, type));
        }

        // Bước 2: tải lên R2. Có file nào lỗi thì xóa các file đã tải rồi báo lỗi.
        var uploadedKeys = new List<string>();
        var newImages = new List<Productimage>();
        try
        {
            foreach (var (file, type) in prepared)
            {
                // Tên file do hệ thống tự đặt (GUID + đuôi theo loại ảnh thật), không dùng tên người dùng gửi,
                // để tránh trùng tên và các trò đặt tên file độc hại.
                var key = $"products/{productId}/{Guid.NewGuid():N}.{type.Extension}";

                await using var stream = file.OpenReadStream();
                await _storage.UploadAsync(stream, key, type.ContentType);

                uploadedKeys.Add(key);
                newImages.Add(new Productimage
                {
                    ProductId = productId,
                    StorageKey = key,
                    ContentType = type.ContentType,
                    SizeBytes = file.Length,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tải ảnh lên R2 thất bại cho sản phẩm {ProductId}", productId);
            await CleanupAsync(uploadedKeys);
            return Fail<List<ProductImageDto>>(ServiceErrorType.Unavailable,
                "Không tải được ảnh lên kho lưu trữ, hãy thử lại sau.");
        }

        // Bước 3: ghi thông tin ảnh vào database. Lỗi thì xóa các file vừa tải để không để lại file mồ côi.
        var hasPrimary = await _db.ProductImages.AnyAsync(i => i.ProductId == productId && i.IsPrimary);
        var maxOrder = await _db.ProductImages
            .Where(i => i.ProductId == productId)
            .MaxAsync(i => (int?)i.SortOrder) ?? 0;

        for (var i = 0; i < newImages.Count; i++)
        {
            newImages[i].SortOrder = maxOrder + 1 + i;
            // Sản phẩm chưa có ảnh đại diện thì ảnh đầu tiên được chọn làm ảnh đại diện.
            newImages[i].IsPrimary = !hasPrimary && i == 0;
        }

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.ProductImages.AddRange(newImages);
            await _db.SaveChangesAsync();

            _audit.Add(actorId, "product.image_upload", nameof(Product), productId.ToString(), null,
                new { Images = newImages.Select(i => new { i.Id, i.StorageKey, i.IsPrimary }).ToList() });
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Lưu thông tin ảnh thất bại cho sản phẩm {ProductId}", productId);
            await CleanupAsync(uploadedKeys);
            return Fail<List<ProductImageDto>>(ServiceErrorType.Conflict,
                "Không lưu được thông tin ảnh, hãy thử lại.");
        }

        return ServiceResult<List<ProductImageDto>>.Ok(newImages.Select(ToDto).ToList());
    }

    // ---------- Chọn ảnh đại diện ----------

    public async Task<ServiceResult<ProductImageDto>> SetPrimaryAsync(long productId, long imageId, long actorId)
    {
        var image = await _db.ProductImages.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId);
        if (image is null || !await ProductExistsAsync(productId))
            return Fail<ProductImageDto>(ServiceErrorType.NotFound, "Không tìm thấy ảnh.");

        if (image.IsPrimary)
            return ServiceResult<ProductImageDto>.Ok(ToDto(image));

        await using var tx = await _db.Database.BeginTransactionAsync();

        // Phải bỏ cờ ảnh đại diện cũ TRƯỚC, rồi mới đặt cờ cho ảnh mới: chỉ mục duy nhất một phần
        // không cho phép có hai ảnh đại diện cùng lúc, kể cả trong khoảnh khắc ngắn.
        await _db.ProductImages
            .Where(i => i.ProductId == productId && i.IsPrimary)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsPrimary, false));

        await _db.ProductImages
            .Where(i => i.Id == imageId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsPrimary, true));

        _audit.Add(actorId, "product.image_set_primary", nameof(Product), productId.ToString(),
                   null, new { PrimaryImageId = imageId });
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        image.IsPrimary = true;
        return ServiceResult<ProductImageDto>.Ok(ToDto(image));
    }

    // ---------- Xóa ảnh ----------

    public async Task<ServiceResult<bool>> DeleteAsync(long productId, long imageId, long actorId)
    {
        var image = await _db.ProductImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId);
        if (image is null || !await ProductExistsAsync(productId))
            return Fail<bool>(ServiceErrorType.NotFound, "Không tìm thấy ảnh.");

        var key = image.StorageKey;
        var wasPrimary = image.IsPrimary;

        await using var tx = await _db.Database.BeginTransactionAsync();

        _db.ProductImages.Remove(image);
        _audit.Add(actorId, "product.image_delete", nameof(Product), productId.ToString(),
                   new { image.Id, image.StorageKey, image.IsPrimary }, null);
        await _db.SaveChangesAsync();

        // Vừa xóa ảnh đại diện thì chuyển cờ sang ảnh đầu danh sách (nếu còn ảnh).
        if (wasPrimary)
        {
            var next = await _db.ProductImages
                .Where(i => i.ProductId == productId)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                .FirstOrDefaultAsync();

            if (next is not null)
            {
                next.IsPrimary = true;
                await _db.SaveChangesAsync();
            }
        }

        await tx.CommitAsync();

        // Xóa dòng trong database TRƯỚC, xóa file trên R2 SAU. Nếu làm ngược lại mà database lỗi,
        // sản phẩm sẽ trỏ tới file đã mất (ảnh vỡ ngay trên trang). Còn nếu xóa file R2 lỗi thì chỉ
        // dư một file không ai thấy; ta ghi log để dọn sau.
        try
        {
            await _storage.DeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không xóa được file trên R2 (cần dọn tay): {Key}", key);
        }

        return ServiceResult<bool>.Ok(true);
    }

    // ---------- Hàm hỗ trợ ----------

    private Task<bool> ProductExistsAsync(long productId) =>
        _db.Products.AnyAsync(p => p.Id == productId && !p.IsDeleted);

    // Xác định loại ảnh THẬT bằng vài byte đầu của file ("chữ ký" file), không tin đuôi file
    // hay Content-Type do máy khách gửi (cả hai đều dễ bị giả mạo).
    private static async Task<ImageType?> DetectImageTypeAsync(IFormFile file)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);

        // JPEG: FF D8 FF
        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return Jpeg;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (read >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return Png;

        // WebP: "RIFF" .... "WEBP"
        if (read >= 12 &&
            header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' &&
            header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
            return WebP;

        return null;
    }

    // Xóa các file đã tải lên R2 khi thao tác thất bại giữa chừng. Làm tối đa có thể, lỗi chỉ ghi log.
    private async Task CleanupAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await _storage.DeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không xóa được file mồ côi trên R2 (cần dọn tay): {Key}", key);
            }
        }
    }

    private static ServiceResult<T> Fail<T>(ServiceErrorType type, string error) =>
        ServiceResult<T>.Fail(type, error);

    private ProductImageDto ToDto(Productimage i) => new()
    {
        Id = i.Id,
        StorageKey = i.StorageKey,
        Url = _urls.GetUrl(i.StorageKey),
        SortOrder = i.SortOrder,
        IsPrimary = i.IsPrimary
    };
}