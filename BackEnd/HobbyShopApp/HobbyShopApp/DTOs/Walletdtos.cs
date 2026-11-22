using HobbyShopApp.Models;

namespace HobbyShopApp.DTOs;

// Thông tin ví của người đang đăng nhập. Số tiền tính bằng đồng.
public class WalletDto
{
    public long Balance { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Một dòng trong lịch sử giao dịch. Amount có dấu: dương = tiền vào, âm = tiền ra.
public class LedgerEntryDto
{
    public long Id { get; set; }
    public long Amount { get; set; }
    public long BalanceAfter { get; set; }
    public LedgerEntryType EntryType { get; set; }
    public string? RefType { get; set; }
    public long? RefId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Một trang lịch sử giao dịch (mới nhất trước).
public class WalletHistoryDto
{
    public List<LedgerEntryDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}