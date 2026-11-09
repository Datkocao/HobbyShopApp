using HobbyShopApp.Data;
using HobbyShopApp.DTOs;
using HobbyShopApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Services;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetTreeAsync(bool includeInactive);
    Task<ServiceResult<CategoryDto>> GetAsync(long id, bool includeInactive);
    Task<ServiceResult<CategoryDto>> CreateAsync(CategoryRequest request, long actorId);
    Task<ServiceResult<CategoryDto>> UpdateAsync(long id, CategoryRequest request, long actorId);
    Task<ServiceResult<bool>> DeleteAsync(long id, long actorId);
}

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public CategoryService(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // ---------- Đọc ----------

    public async Task<List<CategoryDto>> GetTreeAsync(bool includeInactive)
    {
        var all = await LoadAsync(includeInactive);

        // Chỉ bắt đầu từ danh mục gốc; danh mục con được gắn vào bên trong.
        return all.Where(c => c.ParentId == null)
                  .Select(c => ToDto(c, all))
                  .ToList();
    }

    public async Task<ServiceResult<CategoryDto>> GetAsync(long id, bool includeInactive)
    {
        var all = await LoadAsync(includeInactive);
        var category = all.FirstOrDefault(c => c.Id == id);

        return category is null
            ? ServiceResult<CategoryDto>.Fail(ServiceErrorType.NotFound, "Không tìm thấy danh mục.")
            : ServiceResult<CategoryDto>.Ok(ToDto(category, all));
    }

    // ---------- Tạo ----------

    public async Task<ServiceResult<CategoryDto>> CreateAsync(CategoryRequest request, long actorId)
    {
        var name = request.Name.Trim();

        var parentError = await ValidateParentAsync(request.ParentId, null);
        if (parentError is not null)
            return ServiceResult<CategoryDto>.Fail(ServiceErrorType.Validation, parentError);

        var category = new Category
        {
            Name = name,
            Slug = await GenerateUniqueSlugAsync(name),
            ParentId = request.ParentId,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
        };

        // Dùng transaction: lưu danh mục để lấy Id, rồi ghi nhật ký. Lỗi ở bước nào cũng hoàn tác cả hai.
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Categories.Add(category);
            await _db.SaveChangesAsync();

            _audit.Add(actorId, "category.create", nameof(Category),
                       category.Id.ToString(), null, Snapshot(category));
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<CategoryDto>.Fail(ServiceErrorType.Conflict,
                "Không thể tạo danh mục (có thể bị trùng slug), hãy thử lại.");
        }

        return ServiceResult<CategoryDto>.Ok(ToDto(category, new List<Category>()));
    }

    // ---------- Sửa ----------

    public async Task<ServiceResult<CategoryDto>> UpdateAsync(long id, CategoryRequest request, long actorId)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (category is null)
            return ServiceResult<CategoryDto>.Fail(ServiceErrorType.NotFound, "Không tìm thấy danh mục.");

        var parentError = await ValidateParentAsync(request.ParentId, id);
        if (parentError is not null)
            return ServiceResult<CategoryDto>.Fail(ServiceErrorType.Validation, parentError);

        var before = Snapshot(category);

        category.Name = request.Name.Trim();
        category.ParentId = request.ParentId;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;
        // Slug giữ nguyên khi đổi tên để các đường dẫn đã chia sẻ không bị hỏng.

        _audit.Add(actorId, "category.update", nameof(Category),
                   category.Id.ToString(), before, Snapshot(category));
        await _db.SaveChangesAsync();

        var children = await _db.Categories.AsNoTracking()
            .Where(c => c.ParentId == id && !c.IsDeleted)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync();

        return ServiceResult<CategoryDto>.Ok(ToDto(category, children));
    }

    // ---------- Xóa (xóa mềm) ----------

    public async Task<ServiceResult<bool>> DeleteAsync(long id, long actorId)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (category is null)
            return ServiceResult<bool>.Fail(ServiceErrorType.NotFound, "Không tìm thấy danh mục.");

        if (await _db.Categories.AnyAsync(c => c.ParentId == id && !c.IsDeleted))
            return ServiceResult<bool>.Fail(ServiceErrorType.Conflict,
                "Danh mục còn danh mục con, hãy xóa hoặc chuyển chúng trước.");

        if (await _db.Products.AnyAsync(p => p.CategoryId == id && !p.IsDeleted))
            return ServiceResult<bool>.Fail(ServiceErrorType.Conflict,
                "Danh mục còn sản phẩm, hãy chuyển sản phẩm sang danh mục khác trước.");

        var before = Snapshot(category);

        category.IsDeleted = true;
        category.UpdatedAt = DateTime.UtcNow;

        _audit.Add(actorId, "category.delete", nameof(Category),
                   category.Id.ToString(), before, Snapshot(category));
        await _db.SaveChangesAsync();

        return ServiceResult<bool>.Ok(true);
    }

    // ---------- Hàm hỗ trợ ----------

    // Lấy danh sách phẳng các danh mục chưa xóa, đã sắp xếp. Số danh mục ít nên dựng cây trong bộ nhớ.
    private async Task<List<Category>> LoadAsync(bool includeInactive)
    {
        var query = _db.Categories.AsNoTracking().Where(c => !c.IsDeleted);
        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();
    }

    // Kiểm tra danh mục cha hợp lệ. Trả về thông báo lỗi, hoặc null nếu hợp lệ.
    private async Task<string?> ValidateParentAsync(long? parentId, long? selfId)
    {
        if (parentId is null)
            return null;

        if (selfId is not null && parentId == selfId)
            return "Danh mục không thể là cha của chính nó.";

        var parent = await _db.Categories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == parentId && !c.IsDeleted);
        if (parent is null)
            return "Danh mục cha không tồn tại.";

        // Giới hạn 2 tầng: danh mục cha phải là danh mục gốc.
        if (parent.ParentId is not null)
            return "Chỉ hỗ trợ tối đa 2 tầng danh mục: danh mục cha phải là danh mục gốc.";

        // Danh mục đang có con mà lại trở thành con của danh mục khác sẽ tạo ra tầng thứ ba.
        if (selfId is not null &&
            await _db.Categories.AnyAsync(c => c.ParentId == selfId && !c.IsDeleted))
            return "Danh mục đang có danh mục con nên không thể chuyển thành danh mục con.";

        return null;
    }

    // Sinh slug từ tên; nếu trùng thì thêm -2, -3... Kiểm tra cả danh mục đã xóa vì chỉ mục duy nhất tính trên mọi dòng.
    private async Task<string> GenerateUniqueSlugAsync(string name)
    {
        var baseSlug = SlugHelper.Generate(name);
        if (string.IsNullOrEmpty(baseSlug))
            baseSlug = "danh-muc";

        var slug = baseSlug;
        var counter = 2;
        while (await _db.Categories.AnyAsync(c => c.Slug == slug))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }
        return slug;
    }

    // Ảnh chụp các trường chính để ghi vào nhật ký (giá trị trước và sau khi sửa).
    private static object Snapshot(Category c) => new
    {
        c.Id,
        c.ParentId,
        c.Name,
        c.Slug,
        c.SortOrder,
        c.IsActive,
        c.IsDeleted
    };

    private static CategoryDto ToDto(Category c, IReadOnlyCollection<Category> all) => new()
    {
        Id = c.Id,
        ParentId = c.ParentId,
        Name = c.Name,
        Slug = c.Slug,
        SortOrder = c.SortOrder,
        IsActive = c.IsActive,
        Children = all.Where(x => x.ParentId == c.Id)
                      .Select(x => ToDto(x, all))
                      .ToList()
    };
}