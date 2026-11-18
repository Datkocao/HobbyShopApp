using HobbyShopApp.Data;
using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Services;

public interface ICartService
{
    Task<CartDto> GetAsync(long userId);
    Task<ServiceResult<CartDto>> AddItemAsync(long userId, CartItemRequest request);
    Task<ServiceResult<CartDto>> SetQuantityAsync(long userId, long productId, int quantity);
    Task<ServiceResult<CartDto>> RemoveItemAsync(long userId, long productId);
    Task<CartDto> ClearAsync(long userId);
}

public class CartService : ICartService
{
    private const int MaxQuantityPerLine = 99;

    private readonly AppDbContext _db;
    private readonly IImageUrlBuilder _urls;

    public CartService(AppDbContext db, IImageUrlBuilder urls)
    {
        _db = db;
        _urls = urls;
    }

    // ---------- Xem giỏ ----------

    public async Task<CartDto> GetAsync(long userId)
    {
        // Lấy giá, tồn kho và tình trạng bán HIỆN TẠI của từng sản phẩm trong giỏ, chỉ các cột cần thiết.
        var rows = await _db.CartItems.AsNoTracking()
            .Where(i => i.Cart.UserId == userId)
            .OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)   // mới thêm lên đầu
            .Select(i => new
            {
                i.Product.Id,
                i.Product.Name,
                i.Product.Slug,
                i.Product.Price,
                i.Product.StockQuantity,
                Visible = !i.Product.IsDeleted
                          && i.Product.Status == ProductStatus.Active
                          && i.Product.Category.IsActive
                          && !i.Product.Category.IsDeleted,
                i.Quantity,
                PrimaryImageKey = i.Product.Images
                    .Where(x => x.IsPrimary)
                    .Select(x => x.StorageKey)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var items = rows.Select(r =>
        {
            string? issue = null;
            if (!r.Visible)
                issue = "Sản phẩm không còn được bán.";
            else if (r.StockQuantity <= 0)
                issue = "Sản phẩm đã hết hàng.";
            else if (r.Quantity > r.StockQuantity)
                issue = $"Chỉ còn {r.StockQuantity} sản phẩm trong kho.";

            return new CartItemDto
            {
                ProductId = r.Id,
                Name = r.Name,
                Slug = r.Slug,
                UnitPrice = r.Price,
                Quantity = r.Quantity,
                LineTotal = r.Price * r.Quantity,
                StockQuantity = r.StockQuantity,
                PrimaryImageUrl = r.PrimaryImageKey is null ? null : _urls.GetUrl(r.PrimaryImageKey),
                Available = r.Visible,
                Issue = issue
            };
        }).ToList();

        return new CartDto
        {
            Items = items,
            TotalQuantity = items.Sum(i => i.Quantity),
            Subtotal = items.Where(i => i.Available).Sum(i => i.LineTotal),
            HasIssues = items.Any(i => i.Issue is not null)
        };
    }

    // ---------- Thêm vào giỏ ----------

    public async Task<ServiceResult<CartDto>> AddItemAsync(long userId, CartItemRequest request)
    {
        var cart = await GetOrCreateCartAsync(userId);

        var line = await _db.CartItems
            .FirstOrDefaultAsync(i => i.CartId == cart.Id && i.ProductId == request.ProductId);

        // Thêm sản phẩm đã có trong giỏ thì CỘNG DỒN số lượng.
        var newQuantity = (line?.Quantity ?? 0) + request.Quantity;

        var problem = await CheckProductAsync(userId, request.ProductId, newQuantity);
        if (problem is not null)
            return problem;

        if (line is null)
            _db.CartItems.Add(new CartItem { CartId = cart.Id, ProductId = request.ProductId, Quantity = newQuantity });
        else
            line.Quantity = newQuantity;

        cart.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Hai yêu cầu thêm cùng một sản phẩm cùng lúc: chỉ mục duy nhất (cart_id, product_id) chặn dòng thứ hai.
            return ServiceResult<CartDto>.Fail(ServiceErrorType.Conflict, "Giỏ hàng vừa thay đổi, hãy thử lại.");
        }

        return ServiceResult<CartDto>.Ok(await GetAsync(userId));
    }

    // ---------- Đổi số lượng ----------

    public async Task<ServiceResult<CartDto>> SetQuantityAsync(long userId, long productId, int quantity)
    {
        var line = await _db.CartItems
            .Include(i => i.Cart)
            .FirstOrDefaultAsync(i => i.Cart.UserId == userId && i.ProductId == productId);
        if (line is null)
            return ServiceResult<CartDto>.Fail(ServiceErrorType.NotFound, "Sản phẩm không có trong giỏ.");

        var problem = await CheckProductAsync(userId, productId, quantity);
        if (problem is not null)
            return problem;

        line.Quantity = quantity;
        line.Cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ServiceResult<CartDto>.Ok(await GetAsync(userId));
    }

    // ---------- Bỏ một dòng ----------

    public async Task<ServiceResult<CartDto>> RemoveItemAsync(long userId, long productId)
    {
        var line = await _db.CartItems
            .Include(i => i.Cart)
            .FirstOrDefaultAsync(i => i.Cart.UserId == userId && i.ProductId == productId);
        if (line is null)
            return ServiceResult<CartDto>.Fail(ServiceErrorType.NotFound, "Sản phẩm không có trong giỏ.");

        // Bỏ dòng thì KHÔNG kiểm tra sản phẩm: người dùng phải bỏ được cả những sản phẩm đã ngừng bán.
        _db.CartItems.Remove(line);
        line.Cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ServiceResult<CartDto>.Ok(await GetAsync(userId));
    }

    // ---------- Xóa cả giỏ ----------

    public async Task<CartDto> ClearAsync(long userId)
    {
        // Một câu DELETE duy nhất, không cần nạp từng dòng lên bộ nhớ.
        await _db.CartItems.Where(i => i.Cart.UserId == userId).ExecuteDeleteAsync();
        return await GetAsync(userId);
    }

    // ---------- Hàm hỗ trợ ----------

    // Mỗi người có đúng một giỏ, tạo khi cần.
    private async Task<Cart> GetOrCreateCartAsync(long userId)
    {
        var cart = await _db.Carts.FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart is not null)
            return cart;

        cart = new Cart { UserId = userId };
        _db.Carts.Add(cart);

        try
        {
            await _db.SaveChangesAsync();
            return cart;
        }
        catch (DbUpdateException)
        {
            // Một yêu cầu khác vừa tạo giỏ cho người này (chỉ mục duy nhất theo user_id): lấy giỏ đó.
            _db.Entry(cart).State = EntityState.Detached;
            return await _db.Carts.FirstAsync(c => c.UserId == userId);
        }
    }

    // Kiểm tra sản phẩm có thể đặt với số lượng cho trước. Trả về kết quả lỗi, hoặc null nếu ổn.
    // Đây chỉ là kiểm tra sớm cho người dùng; khi đặt hàng sẽ kiểm tra lại trong transaction vì tồn kho có thể đổi.
    private async Task<ServiceResult<CartDto>?> CheckProductAsync(long userId, long productId, int quantity)
    {
        var product = await _db.Products.AsNoTracking()
            .Where(p => p.Id == productId
                        && !p.IsDeleted
                        && p.Status == ProductStatus.Active
                        && p.Category.IsActive && !p.Category.IsDeleted)
            .Select(p => new { p.SellerUserId, p.StockQuantity })
            .FirstOrDefaultAsync();

        if (product is null)
            return ServiceResult<CartDto>.Fail(ServiceErrorType.NotFound,
                "Sản phẩm không tồn tại hoặc đã ngừng bán.");

        if (product.SellerUserId == userId)
            return ServiceResult<CartDto>.Fail(ServiceErrorType.Validation,
                "Bạn không thể mua sản phẩm của chính mình.");

        if (product.StockQuantity <= 0)
            return ServiceResult<CartDto>.Fail(ServiceErrorType.Conflict, "Sản phẩm đã hết hàng.");

        if (quantity > product.StockQuantity)
            return ServiceResult<CartDto>.Fail(ServiceErrorType.Conflict,
                $"Chỉ còn {product.StockQuantity} sản phẩm trong kho.");

        if (quantity > MaxQuantityPerLine)
            return ServiceResult<CartDto>.Fail(ServiceErrorType.Validation,
                $"Mỗi sản phẩm tối đa {MaxQuantityPerLine} trong giỏ.");

        return null;
    }
}