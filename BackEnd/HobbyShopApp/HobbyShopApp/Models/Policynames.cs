namespace HobbyShopApp.Models;

// Tên các policy KHÔNG phải permission (permission nằm trong PermissionCodes).
public static class PolicyNames
{
    // Người được phép mua hàng: đã đăng nhập và không phải nhân viên quản trị (STAFF, ADMIN).
    public const string CanShop = "CanShop";
}