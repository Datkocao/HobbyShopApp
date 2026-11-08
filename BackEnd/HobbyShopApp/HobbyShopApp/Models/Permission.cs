namespace HobbyShopApp.Models
{
    public class Permission
    {
        public long Id { get; set; }
        public string Code { get; set; } = null!;
        public string? Description { get; set; }

        public List<RolePermission> RolePermissions { get; set; } = new();
    }
}
