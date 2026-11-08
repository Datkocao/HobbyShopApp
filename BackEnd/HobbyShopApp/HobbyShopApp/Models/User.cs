namespace HobbyShopApp.Models
{
    public enum UserStatus
    {
        Active,
        Locked
    }

    public class User
    {
        public long Id { get; set; }
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? Phone { get; set; }

        public long RoleId { get; set; }
        public Role Role { get; set; } = null!;

        public UserStatus Status { get; set; } = UserStatus.Active;
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}