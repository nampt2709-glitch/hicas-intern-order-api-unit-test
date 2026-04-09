namespace OrderTestingLab.Dtos;

/// <summary>
/// DTO trả về cho client sau khi tạo hoặc lấy đơn hàng.
/// </summary>
public class OrderResponse
{
    public Guid Id { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; }
}
