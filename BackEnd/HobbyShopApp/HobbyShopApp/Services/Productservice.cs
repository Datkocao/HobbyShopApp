using System.Linq.Expressions;
using HobbyShopApp.Data;
using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Services;

public interface IProductService
{
    Task<List<ProductListItemDto>> GetFeaturedAsync(int take);
    Task<ServiceResult<PagedResult<ProductListItemDto>>> GetListAsync(ProductQuery query, bool isManager);
    Task<ServiceResult<ProductDetailDto>> GetAsync(long id, bool isManager, bool isAuthenticated);
    Task<ServiceResult<ProductDetailDto>> CreateAsync(ProductCreateRequest request, long actorId);
    Task<ServiceResult<ProductDetailDto>> UpdateAsync(long id, ProductUpdateRequest request, long actorId);
    Task<ServiceResult<ProductDetailDto>> AdjustStockAsync(long id, StockAdjustRequest request, long actorId);
    Task<ServiceResult<bool>> DeleteAsync(long id, long actorId);
}

public class ProductService : IProductService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly IImageUrlBuilder _urls;

    public ProductService(AppDbContext db, IAuditService audit, IImageUrlBuilder urls)
    {
        _db = db;
        _audit = audit;
        _urls = urls;
    }

    // Cách chuyển một Product thành dòng trong danh sách. Viết dạng Expression để EF dịch thẳng
    // thành SQL, chỉ lấy đúng các cột cần (không kéo cả bảng về).
    private static readonly Expression<Func<Product, ProductListItemDto>> ListItem = p => new ProductListItemDto
    {
        Id = p.Id,
        Name = p.Name,
        Slug = p.Slug,
        Price = p.Price,
        StockQuantity = p.StockQuantity,
        InStock = p.StockQuantity > 0,
        CategoryId = p.CategoryId,
        CategoryName = p.Category.Name,
        IsFeatured = p.IsFeatured,
        Status = p.Status,
        PrimaryImageKey = p.Images.Where(i => i.IsPrimary).Select(i => i.StorageKey).FirstOrDefault()
    };

    // Sản phẩm khách hàng được phép thấy: đang bán, chưa xóa, và danh mục còn hiển thị.
    private IQueryable<Product> VisibleToCustomers() =>
        _db.Products.AsNoTracking().Where(p =>
            !p.IsDeleted &&
            p.Status == ProductStatus.Active &&
            p.Category.IsActive && !p.Category.IsDeleted);

    // ---------- Đọc ----------

    public async Task<List<ProductListItemDto>> GetFeaturedAsync(int take)
    {
        take = Math.Clamp(take, 1, 50);

        var items = await VisibleToCustomers()
        .Where(p => p.IsFeatured)
        .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
        .Take(take)
        .Select(ListItem)
        .ToListAsync();

        FillImageUrls(items);                                   // [THÊM] điền PrimaryImageUrl
        return items;
    }

    public async Task<ServiceResult<PagedResult<ProductListItemDto>>> GetListAsync(ProductQuery query, bool isManager)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        if (query.MinPrice is not null && query.MaxPrice is not null && query.MinPrice > query.MaxPrice)
            return ServiceResult<PagedResult<ProductListItemDto>>.Fail(
                ServiceErrorType.Validation, "Giá tối thiểu không được lớn hơn giá tối đa.");

        // Người quản lý (và có yêu cầu) thấy mọi trạng thái; còn lại chỉ thấy hàng đang bán.
        IQueryable<Product> q;
        if (isManager && query.IncludeAllStatuses)
        {
            q = _db.Products.AsNoTracking().Where(p => !p.IsDeleted);
            if (query.Status is { } status)
                q = q.Where(p => p.Status == status);
        }
        else
        {
            q = VisibleToCustomers();
        }

        // Lọc theo danh mục: chọn danh mục gốc thì lấy luôn sản phẩm của các danh mục con.
        if (query.CategoryId is { } categoryId)
        {
            var categoryIds = await _db.Categories.AsNoTracking()
                .Where(c => !c.IsDeleted && (c.Id == categoryId || c.ParentId == categoryId))
                .Select(c => c.Id)
                .ToListAsync();
            q = q.Where(p => categoryIds.Contains(p.CategoryId));
        }

        // Tìm theo tên, không phân biệt hoa/thường (ILIKE của PostgreSQL).
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var pattern = $"%{EscapeLike(query.Keyword.Trim())}%";
            q = q.Where(p => EF.Functions.ILike(p.Name, pattern));
        }

        if (query.MinPrice is { } min)
            q = q.Where(p => p.Price >= min);
        if (query.MaxPrice is { } max)
            q = q.Where(p => p.Price <= max);

        // Luôn thêm Id vào cuối để thứ tự ổn định, nếu không phân trang có thể lặp hoặc sót dòng.
        var sorted = query.Sort switch
        {
            "price_asc" => q.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "price_desc" => q.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            "name" => q.OrderBy(p => p.Name).ThenBy(p => p.Id),
            _ => q.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
        };

        var totalItems = await q.CountAsync();
        var items = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ListItem)
            .ToListAsync();

        FillImageUrls(items);

        return ServiceResult<PagedResult<ProductListItemDto>>.Ok(new PagedResult<ProductListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        });
    }

    public async Task<ServiceResult<ProductDetailDto>> GetAsync(long id, bool isManager, bool isAuthenticated)
    {
        var q = _db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => p.Id == id && !p.IsDeleted);

        if (!isManager)
        {
            q = q.Where(p => p.Status == ProductStatus.Active &&
                             p.Category.IsActive && !p.Category.IsDeleted);

            // Khách chưa đăng nhập chỉ xem được chi tiết sản phẩm nổi bật.
            if (!isAuthenticated)
                q = q.Where(p => p.IsFeatured);
        }

        var product = await q.FirstOrDefaultAsync();

        // Không phân biệt "không tồn tại" với "không được xem" để không lộ thông tin.
        if (product is null)
            return ServiceResult<ProductDetailDto>.Fail(ServiceErrorType.NotFound, "Không tìm thấy sản phẩm.");

        var dto = ToDetailDto(product);
        foreach (var image in dto.Images)
            image.Url = _urls.GetUrl(image.StorageKey);             // [THÊM]

        return ServiceResult<ProductDetailDto>.Ok(dto);
    }

    // ---------- Tạo ----------

    public async Task<ServiceResult<ProductDetailDto>> CreateAsync(ProductCreateRequest request, long actorId)
    {
        var error = await ValidateAsync(request.CategoryId, request.Status);
        if (error is not null)
            return ServiceResult<ProductDetailDto>.Fail(ServiceErrorType.Validation, error);

        var name = request.Name.Trim();
        var product = new Product
        {
            CategoryId = request.CategoryId,
            SellerUserId = null,                 // rỗng = hàng của shop (B2C)
            CreatedByUserId = actorId,
            Name = name,
            Slug = await GenerateUniqueSlugAsync(name),
            Description = request.Description?.Trim(),
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            WeightGrams = request.WeightGrams,
            Status = request.Status,
            IsFeatured = request.IsFeatured,
        };

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            _audit.Add(actorId, "product.create", nameof(Product),
                       product.Id.ToString(), null, Snapshot(product));
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<ProductDetailDto>.Fail(ServiceErrorType.Conflict,
                "Không thể tạo sản phẩm (có thể bị trùng slug), hãy thử lại.");
        }

        return await GetAsync(product.Id, isManager: true, isAuthenticated: true);
    }

    // ---------- Sửa thông tin (không gồm tồn kho) ----------

    public async Task<ServiceResult<ProductDetailDto>> UpdateAsync(long id, ProductUpdateRequest request, long actorId)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (product is null)
            return ServiceResult<ProductDetailDto>.Fail(ServiceErrorType.NotFound, "Không tìm thấy sản phẩm.");

        // Hàng của người bán sẽ có luồng duyệt riêng ở giai đoạn sau.
        if (product.SellerUserId is not null)
            return ServiceResult<ProductDetailDto>.Fail(ServiceErrorType.Validation,
                "Chỉ sửa được sản phẩm của shop tại đây.");

        var error = await ValidateAsync(request.CategoryId, request.Status);
        if (error is not null)
            return ServiceResult<ProductDetailDto>.Fail(ServiceErrorType.Validation, error);

        var before = Snapshot(product);

        product.CategoryId = request.CategoryId;
        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Price = request.Price;
        product.WeightGrams = request.WeightGrams;
        product.IsFeatured = request.IsFeatured;
        product.Status = request.Status;
        product.UpdatedAt = DateTime.UtcNow;
        // Slug giữ nguyên khi đổi tên để link đã chia sẻ không bị hỏng.

        _audit.Add(actorId, "product.update", nameof(Product),
                   product.Id.ToString(), before, Snapshot(product));
        await _db.SaveChangesAsync();

        return await GetAsync(product.Id, isManager: true, isAuthenticated: true);
    }

    // ---------- Điều chỉnh tồn kho ----------

    public async Task<ServiceResult<ProductDetailDto>> AdjustStockAsync(long id, StockAdjustRequest request, long actorId)
    {
        if (request.Delta == 0)
            return ServiceResult<ProductDetailDto>.Fail(ServiceErrorType.Validation, "Delta phải khác 0.");

        if (!await _db.Products.AnyAsync(p => p.Id == id && !p.IsDeleted))
            return ServiceResult<ProductDetailDto>.Fail(ServiceErrorType.NotFound, "Không tìm thấy sản phẩm.");

        var delta = request.Delta;

        await using var tx = await _db.Database.BeginTransactionAsync();

        // Một câu UPDATE duy nhất, tính toán ngay trong database:
        //   UPDATE products SET stock_quantity = stock_quantity + @delta WHERE ... AND stock_quantity + @delta >= 0
        // Nhờ vậy hai người sửa cùng lúc không ghi đè lên nhau, và tồn kho không bao giờ xuống dưới 0.
        // (Cách "đọc số cũ, cộng trừ trong code, rồi ghi lại" sẽ bị mất cập nhật khi có xung đột.)
        var updated = await _db.Products
            .Where(p => p.Id == id && !p.IsDeleted && p.StockQuantity + delta >= 0)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.StockQuantity, p => p.StockQuantity + delta)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow));

        if (updated == 0)
            return ServiceResult<ProductDetailDto>.Fail(ServiceErrorType.Conflict, "Tồn kho không đủ để giảm.");

        var newStock = await _db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => p.StockQuantity)
            .FirstAsync();

        _audit.Add(actorId, "product.stock_adjust", nameof(Product), id.ToString(),
                   new { StockQuantity = newStock - delta },
                   new { StockQuantity = newStock, Delta = delta, request.Reason });
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        return await GetAsync(id, isManager: true, isAuthenticated: true);
    }

    // ---------- Xóa (xóa mềm) ----------

    public async Task<ServiceResult<bool>> DeleteAsync(long id, long actorId)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (product is null)
            return ServiceResult<bool>.Fail(ServiceErrorType.NotFound, "Không tìm thấy sản phẩm.");

        var before = Snapshot(product);

        product.IsDeleted = true;
        product.UpdatedAt = DateTime.UtcNow;

        _audit.Add(actorId, "product.delete", nameof(Product),
                   product.Id.ToString(), before, Snapshot(product));
        await _db.SaveChangesAsync();

        return ServiceResult<bool>.Ok(true);
    }

    // ---------- Hàm hỗ trợ ----------

    // Trả về thông báo lỗi, hoặc null nếu hợp lệ.

    private void FillImageUrls(List<ProductListItemDto> items)
    {
        foreach (var item in items)
            item.PrimaryImageUrl = item.PrimaryImageKey is null ? null : _urls.GetUrl(item.PrimaryImageKey);
    }
    private async Task<string?> ValidateAsync(long categoryId, ProductStatus status)
    {
        if (status != ProductStatus.Draft && status != ProductStatus.Active)
            return "Sản phẩm của shop chỉ có trạng thái Draft hoặc Active.";

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == categoryId && !c.IsDeleted);
        return categoryExists ? null : "Danh mục không tồn tại.";
    }

    private async Task<string> GenerateUniqueSlugAsync(string name)
    {
        var baseSlug = SlugHelper.Generate(name);
        if (string.IsNullOrEmpty(baseSlug))
            baseSlug = "san-pham";

        var slug = baseSlug;
        var counter = 2;
        while (await _db.Products.AnyAsync(p => p.Slug == slug))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }
        return slug;
    }

    // Thêm ký tự escape cho %, _ và \ để từ khóa của người dùng không bị hiểu thành ký tự đại diện của LIKE.
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");



    private static object Snapshot(Product p) => new
    {
        p.Id,
        p.CategoryId,
        p.Name,
        p.Slug,
        p.Price,
        p.StockQuantity,
        p.WeightGrams,
        Status = p.Status.ToString(),
        p.IsFeatured,
        p.IsDeleted
    };

    private static ProductDetailDto ToDetailDto(Product p) => new()
    {
        Id = p.Id,
        CategoryId = p.CategoryId,
        CategoryName = p.Category.Name,
        CategorySlug = p.Category.Slug,
        Name = p.Name,
        Slug = p.Slug,
        Description = p.Description,
        Price = p.Price,
        StockQuantity = p.StockQuantity,
        InStock = p.StockQuantity > 0,
        WeightGrams = p.WeightGrams,
        IsFeatured = p.IsFeatured,
        Status = p.Status,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
        Images = p.Images
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .Select(i => new ProductImageDto
            {
                Id = i.Id,
                StorageKey = i.StorageKey,
                SortOrder = i.SortOrder,
                IsPrimary = i.IsPrimary
            })
            .ToList()
    };
}