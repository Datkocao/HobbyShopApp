namespace HobbyShopApp.Models
{
    public class AuditLog
    {
        public long Id { get; set; }

        public long? ActorUserId { get; set; }
        public User? Actor { get; set; }

        public string Action { get; set; } = null!;
        public string EntityType { get; set; } = null!;
        public string? EntityId { get; set; }

        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
