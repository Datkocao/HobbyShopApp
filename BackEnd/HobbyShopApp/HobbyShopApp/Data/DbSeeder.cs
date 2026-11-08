using HobbyShopApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Data
{
    public class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext db, IConfiguration config, ILogger logger)
        {
            if (!await db.Roles.AnyAsync())
            {
                var roles = new List<Role>
            {
                new() { Code = "BUYER", Name = "Người mua" },
                new() { Code = "SELLER", Name = "Người bán" },
                new() { Code = "COURIER_MANAGER", Name = "Quản lý đơn vị vận chuyển" },
                new() { Code = "HUB_MANAGER", Name = "Quản lý kho" },
                new() { Code = "SHIPPER", Name = "Shipper" },
                new() { Code = "STAFF", Name = "Nhân viên" },
                new() { Code = "ADMIN", Name = "Quản trị viên" },
            };

                var permissions = new List<Permission>
            {
                new() { Code = "product.manage", Description = "Quản lý sản phẩm của shop" },
                new() { Code = "category.manage", Description = "Quản lý danh mục" },
                new() { Code = "order.view_all", Description = "Xem mọi đơn hàng" },
                new() { Code = "order.update_status", Description = "Cập nhật trạng thái đơn" },
                new() { Code = "user.manage", Description = "Quản lý người dùng (trừ Admin)" },
                new() { Code = "user.manage_admin", Description = "Quản lý Staff và Admin" },
                new() { Code = "wallet.approve_topup", Description = "Duyệt yêu cầu nạp ví" },
                new() { Code = "audit.view", Description = "Xem nhật ký thao tác" },
                new() { Code = "system.configure", Description = "Cấu hình hệ thống" },
            };

                db.Roles.AddRange(roles);
                db.Permissions.AddRange(permissions);
                await db.SaveChangesAsync();

                var perm = permissions.ToDictionary(p => p.Code, p => p);
                var role = roles.ToDictionary(r => r.Code, r => r);

                var staffPermissions = new[]
                {
                "product.manage", "category.manage", "order.view_all",
                "order.update_status", "user.manage", "wallet.approve_topup"
            };

                foreach (var code in staffPermissions)
                    db.RolePermissions.Add(new RolePermission { RoleId = role["STAFF"].Id, PermissionId = perm[code].Id });

                foreach (var p in permissions)
                    db.RolePermissions.Add(new RolePermission { RoleId = role["ADMIN"].Id, PermissionId = p.Id });

                await db.SaveChangesAsync();
                logger.LogInformation("Đã seed vai trò và quyền.");
            }

            var email = config["Seed:AdminEmail"];
            var password = config["Seed:AdminPassword"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("Thiếu Seed:AdminEmail hoặc Seed:AdminPassword, bỏ qua tạo admin.");
                return;
            }

            if (!await db.Users.AnyAsync(u => u.Email == email))
            {
                var adminRole = await db.Roles.FirstAsync(r => r.Code == "ADMIN");
                var admin = new User
                {
                    Email = email,
                    FullName = "Quản trị viên",
                    RoleId = adminRole.Id,
                };
                admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, password);

                db.Users.Add(admin);
                await db.SaveChangesAsync();
                logger.LogInformation("Đã tạo tài khoản admin.");
            }
        }
    }
}
