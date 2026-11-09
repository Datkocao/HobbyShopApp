using System.Text.Json;
using HobbyShopApp.Data;
using HobbyShopApp.Models;

namespace HobbyShopApp.Services;

public interface IAuditService
{
    // Chỉ THÊM dòng nhật ký vào DbContext, chưa lưu. Nơi gọi sẽ SaveChanges một lần
    // để thao tác chính và nhật ký được lưu cùng nhau (cùng thành công hoặc cùng thất bại).
    void Add(long? actorUserId, string action, string entityType, string? entityId,
             object? oldValues, object? newValues);
}

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public void Add(long? actorUserId, string action, string entityType, string? entityId,
                    object? oldValues, object? newValues)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
        });
    }
}