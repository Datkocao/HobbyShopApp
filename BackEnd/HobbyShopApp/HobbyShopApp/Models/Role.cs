namespace HobbyShopApp.Models;

public class Role
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    public List<User> Users { get; set; } = new();
    public List<RolePermission> RolePermissions { get; set; } = new();
}