namespace HobbyShopApp.Models;

public static class PermissionCodes
{
    public const string ProductManage = "product.manage";
    public const string CategoryManage = "category.manage";
    public const string OrderViewAll = "order.view_all";
    public const string OrderUpdateStatus = "order.update_status";
    public const string UserManage = "user.manage";
    public const string UserManageAdmin = "user.manage_admin";
    public const string WalletApproveTopup = "wallet.approve_topup";
    public const string AuditView = "audit.view";
    public const string SystemConfigure = "system.configure";

    public static readonly string[] All =
    {
        ProductManage, CategoryManage, OrderViewAll, OrderUpdateStatus,
        UserManage, UserManageAdmin, WalletApproveTopup, AuditView, SystemConfigure
    };
}