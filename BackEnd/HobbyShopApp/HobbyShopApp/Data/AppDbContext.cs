using HobbyShopApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HobbyShopApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<User> Users => Set<User>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Productimage> ProductImages => Set<Productimage>();

        public DbSet<Address> Addresses => Set<Address>();
        public DbSet<Cart> Carts => Set<Cart>();
        public DbSet<CartItem> CartItems => Set<CartItem>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<Wallet> Wallets => Set<Wallet>();
        public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
        public DbSet<TopupRequest> TopupRequests => Set<TopupRequest>();
        public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>(e =>
            {
                e.HasIndex(x => x.Code).IsUnique();
                e.Property(x => x.Code).HasMaxLength(50);
                e.Property(x => x.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<Permission>(e =>
            {
                e.HasIndex(x => x.Code).IsUnique();
                e.Property(x => x.Code).HasMaxLength(100);
            });

            modelBuilder.Entity<RolePermission>(e =>
            {
                e.HasKey(x => new { x.RoleId, x.PermissionId });
            });

            modelBuilder.Entity<User>(e =>
            {
                e.HasIndex(x => x.Email).IsUnique();
                e.Property(x => x.Email).HasMaxLength(255);
                e.Property(x => x.FullName).HasMaxLength(150);
                e.Property(x => x.Phone).HasMaxLength(20);
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                e.HasOne(x => x.Role).WithMany(r => r.Users)
                    .HasForeignKey(x => x.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AuditLog>(e =>
            {
                e.Property(x => x.OldValues).HasColumnType("jsonb");
                e.Property(x => x.NewValues).HasColumnType("jsonb");
                e.HasIndex(x => x.ActorUserId);
                e.HasOne(x => x.Actor).WithMany()
                    .HasForeignKey(x => x.ActorUserId)
                    .OnDelete(DeleteBehavior.SetNull);

            });   // [SỬA] Đóng khối AuditLog ở đây. Trước đây thiếu dòng này nên toàn bộ các khối bên dưới
                  // (Address đến SystemSetting) bị lồng NGAY TRONG khối AuditLog. Vẫn chạy được nhưng sai cấu trúc.

            // ---------- [THÊM] Category (danh mục) ----------
            // Khối này trước đây bị thiếu, nên bảng categories không có chỉ mục duy nhất cho slug và cột chữ không giới hạn độ dài.
            modelBuilder.Entity<Category>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(150);
                e.Property(x => x.Slug).HasMaxLength(180);

                // Slug (tên rút gọn trong đường dẫn, ví dụ "mo-hinh-gundam") không được trùng.
                e.HasIndex(x => x.Slug).IsUnique();

                // Tăng tốc truy vấn "lấy các danh mục con của danh mục X".
                e.HasIndex(x => x.ParentId);

                // Danh mục tự tham chiếu chính nó: ParentId trỏ về Id của danh mục cha.
                // Restrict = không cho xóa danh mục cha khi còn danh mục con.
                e.HasOne(x => x.Parent).WithMany(x => x.Children)
                    .HasForeignKey(x => x.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------- [THÊM] Product (sản phẩm) ----------
            // Khối này trước đây bị thiếu và gây ra 4 vấn đề:
            //  (1) EF tự tạo thêm hai cột ẩn thừa seller_id và created_by_id (created_by_id còn bắt buộc, khiến tạo sản phẩm bị lỗi),
            //  (2) trạng thái (status) bị lưu dạng số 0, 1, 2 thay vì chữ,
            //  (3) không có chỉ mục duy nhất cho slug,
            //  (4) không có ràng buộc giá và tồn kho không âm.
            modelBuilder.Entity<Product>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(250);
                e.Property(x => x.Slug).HasMaxLength(280);

                // Lưu trạng thái dạng chữ ("Active") thay vì số, dễ đọc khi xem trong DBeaver.
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

                e.HasIndex(x => x.Slug).IsUnique();

                // Ba chỉ mục phục vụ các truy vấn hay dùng nhất:
                e.HasIndex(x => new { x.CategoryId, x.Status });   // lọc sản phẩm theo danh mục
                e.HasIndex(x => new { x.Status, x.IsFeatured });   // lấy sản phẩm nổi bật cho trang chủ
                e.HasIndex(x => x.Price);                          // lọc theo khoảng giá

                // Ràng buộc ngay trong database: giá và tồn kho không bao giờ âm.
                // Đây là "lưới an toàn" cuối cùng: dù code có lỗi, database vẫn từ chối dữ liệu sai.
                e.ToTable(t =>
                {
                    t.HasCheckConstraint("ck_products_price_non_negative", "price >= 0");
                    t.HasCheckConstraint("ck_products_stock_non_negative", "stock_quantity >= 0");
                });

                // Sản phẩm thuộc một danh mục. Restrict = không xóa danh mục khi còn sản phẩm.
                e.HasOne(x => x.Category).WithMany(c => c.Products)
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Bảng Product có HAI khóa ngoại cùng trỏ về User (người bán và người tạo).
                // HasForeignKey chỉ rõ cột khóa ngoại thật (SellerUserId, CreatedByUserId). Thiếu hai dòng này,
                // EF tự đoán tên (seller_id, created_by_id) và tạo cột ẩn thừa, bỏ qua cột của bạn.
                e.HasOne(x => x.Seller).WithMany()
                    .HasForeignKey(x => x.SellerUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CreatedBy).WithMany()
                    .HasForeignKey(x => x.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------- [THÊM] Productimage (ảnh sản phẩm) ----------
            modelBuilder.Entity<Productimage>(e =>
            {
                e.Property(x => x.StorageKey).HasMaxLength(500);
                e.Property(x => x.ContentType).HasMaxLength(100);

                // Chỉ mục thường: lấy nhanh tất cả ảnh của một sản phẩm.
                e.HasIndex(x => x.ProductId).HasDatabaseName("ix_product_images_product_id");

                // Chỉ mục duy nhất "một phần": chỉ tính trên các dòng is_primary = true,
                // nên mỗi sản phẩm tối đa MỘT ảnh đại diện (các ảnh khác không bị ảnh hưởng).
                e.HasIndex(x => x.ProductId)
                    .IsUnique()
                    .HasFilter("is_primary = true")
                    .HasDatabaseName("ux_product_images_one_primary");

                // Xóa sản phẩm thì xóa luôn các dòng ảnh của nó (file trên R2 xử lý riêng bằng code).
                e.HasOne(x => x.Product).WithMany(p => p.Images)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // (giữ nguyên) Các khối bên dưới là của bạn, chỉ lùi lề đúng chỗ (bỏ lồng trong AuditLog).

            // ---------- [THÊM] Address (sổ địa chỉ) ----------
            modelBuilder.Entity<Address>(e =>
            {
                e.Property(x => x.RecipientName).HasMaxLength(150);
                e.Property(x => x.Phone).HasMaxLength(20);
                e.Property(x => x.Province).HasMaxLength(100);
                e.Property(x => x.District).HasMaxLength(100);
                e.Property(x => x.Ward).HasMaxLength(100);
                e.Property(x => x.DetailLine).HasMaxLength(300);

                e.HasIndex(x => x.UserId);   // lấy nhanh các địa chỉ của một người
                e.HasIndex(x => x.UserId)
               .IsUnique()
               .HasFilter("is_default = true AND is_deleted = false")
               .HasDatabaseName("ux_addresses_one_default");

                e.HasOne(x => x.User).WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------- [THÊM] Cart và CartItem (giỏ hàng) ----------
            modelBuilder.Entity<Cart>(e =>
            {
                // Mỗi người dùng đúng một giỏ hàng.
                e.HasIndex(x => x.UserId).IsUnique();

                e.HasOne(x => x.User).WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CartItem>(e =>
            {
                // Một sản phẩm chỉ có một dòng trong giỏ (thêm lần nữa thì tăng số lượng).
                e.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique();

                e.ToTable(t => t.HasCheckConstraint("ck_cart_items_quantity_positive", "quantity > 0"));

                // Xóa giỏ hàng thì xóa luôn các dòng bên trong.
                e.HasOne(x => x.Cart).WithMany(c => c.Items)
                    .HasForeignKey(x => x.CartId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Product).WithMany()
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------- [THÊM] Order và OrderItem (đơn hàng) ----------
            modelBuilder.Entity<Order>(e =>
            {
                e.Property(x => x.OrderCode).HasMaxLength(30);
                e.Property(x => x.RecipientName).HasMaxLength(150);
                e.Property(x => x.Phone).HasMaxLength(20);
                e.Property(x => x.AddressText).HasMaxLength(600);
                e.Property(x => x.Note).HasMaxLength(500);
                e.Property(x => x.CancelReason).HasMaxLength(500);

                // Ba trạng thái lưu dạng chữ cho dễ đọc khi xem trong DBeaver.
                e.Property(x => x.OrderStatus).HasConversion<string>().HasMaxLength(30);
                e.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(30);
                e.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(30);

                e.HasIndex(x => x.OrderCode).IsUnique();
                e.HasIndex(x => new { x.BuyerUserId, x.CreatedAt });   // "đơn của tôi", mới nhất trước
                e.HasIndex(x => x.OrderStatus);                        // lọc đơn theo trạng thái (cho quản trị)

                // Lưới an toàn ngay trong database: tiền không âm và tổng luôn khớp.
                e.ToTable(t =>
                {
                    t.HasCheckConstraint("ck_orders_amounts_non_negative",
                        "subtotal >= 0 AND shipping_fee >= 0 AND total >= 0");
                    t.HasCheckConstraint("ck_orders_total_matches", "total = subtotal + shipping_fee");
                });

                // Bảng Order có HAI khóa ngoại cùng trỏ về User (người mua và người bán),
                // nên phải khai báo rõ từng cái.
                e.HasOne(x => x.Buyer).WithMany()
                    .HasForeignKey(x => x.BuyerUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Seller).WithMany()
                    .HasForeignKey(x => x.SellerUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<OrderItem>(e =>
            {
                e.Property(x => x.ProductName).HasMaxLength(250);

                e.HasIndex(x => x.OrderId);

                e.ToTable(t =>
                {
                    t.HasCheckConstraint("ck_order_items_quantity_positive", "quantity > 0");
                    t.HasCheckConstraint("ck_order_items_amounts_non_negative", "unit_price >= 0 AND line_total >= 0");
                });

                // Đơn hàng không bao giờ bị xóa thật nên dùng Restrict.
                e.HasOne(x => x.Order).WithMany(o => o.Items)
                    .HasForeignKey(x => x.OrderId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Product).WithMany()
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------- [THÊM] Wallet (ví) ----------
            modelBuilder.Entity<Wallet>(e =>
            {
                e.Property(x => x.WalletType).HasConversion<string>().HasMaxLength(20);

                // Mỗi người dùng tối đa một ví (các dòng UserId rỗng, tức ví hệ thống, không bị tính).
                e.HasIndex(x => x.UserId).IsUnique();

                // Chỉ được có MỘT ví hệ thống.
                e.HasIndex(x => x.WalletType)
                    .IsUnique()
                    .HasFilter("wallet_type = 'Platform'")
                    .HasDatabaseName("ux_wallets_single_platform");

                // Ví người dùng không bao giờ được âm. (Ví hệ thống không bị ràng buộc này.)
                e.ToTable(t => t.HasCheckConstraint("ck_wallets_user_balance_non_negative",
                    "wallet_type <> 'User' OR balance >= 0"));

                e.HasOne(x => x.User).WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------- [THÊM] LedgerEntry (sổ giao dịch) ----------
            modelBuilder.Entity<LedgerEntry>(e =>
            {
                e.Property(x => x.EntryType).HasConversion<string>().HasMaxLength(30);
                e.Property(x => x.RefType).HasMaxLength(50);
                e.Property(x => x.IdempotencyKey).HasMaxLength(150);
                e.Property(x => x.Note).HasMaxLength(500);

                // Khóa chống ghi trùng: cùng một khóa không thể tạo hai dòng.
                e.HasIndex(x => x.IdempotencyKey).IsUnique();
                e.HasIndex(x => new { x.WalletId, x.CreatedAt });   // xem lịch sử giao dịch của một ví

                // Mỗi dòng phải có số tiền khác 0.
                e.ToTable(t => t.HasCheckConstraint("ck_ledger_entries_amount_not_zero", "amount <> 0"));

                e.HasOne(x => x.Wallet).WithMany()
                    .HasForeignKey(x => x.WalletId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CreatedBy).WithMany()
                    .HasForeignKey(x => x.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------- [THÊM] TopupRequest (yêu cầu nạp ví) ----------
            modelBuilder.Entity<TopupRequest>(e =>
            {
                e.Property(x => x.TransferCode).HasMaxLength(50);
                e.Property(x => x.ProofStorageKey).HasMaxLength(500);
                e.Property(x => x.RejectReason).HasMaxLength(500);
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

                e.HasIndex(x => x.TransferCode).IsUnique();
                e.HasIndex(x => x.Status);   // quản trị lọc các yêu cầu đang chờ duyệt

                e.ToTable(t => t.HasCheckConstraint("ck_topup_requests_amount_positive", "amount > 0"));

                // Bảng này có ba khóa ngoại trỏ về User/LedgerEntry nên khai báo rõ từng cái.
                e.HasOne(x => x.User).WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ReviewedBy).WithMany()
                    .HasForeignKey(x => x.ReviewedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.LedgerEntry).WithMany()
                    .HasForeignKey(x => x.LedgerEntryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------- [THÊM] SystemSetting (cấu hình hệ thống) ----------
            modelBuilder.Entity<SystemSetting>(e =>
            {
                e.HasKey(x => x.Key);                  // khóa chính là chính cột Key (chuỗi)
                e.Property(x => x.Key).HasMaxLength(100);
                e.Property(x => x.Value).HasMaxLength(2000);

                e.HasOne(x => x.UpdatedBy).WithMany()
                    .HasForeignKey(x => x.UpdatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}