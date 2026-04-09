namespace OrderTestingLab.Entities;

/// <summary>
/// Entity Order lưu trong database (bảng Orders).
/// </summary>
public class Order
{
    public Guid Id { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Tổng tiền = Quantity * UnitPrice (lưu trong DB để truy vấn/report).
    /// </summary>
    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; }
}
