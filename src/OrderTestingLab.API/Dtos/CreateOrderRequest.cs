using System.ComponentModel.DataAnnotations;

namespace OrderTestingLab.Dtos;

/// <summary>
/// DTO nhận từ client khi tạo đơn hàng. Dùng DataAnnotations để ASP.NET Core trả 400 khi không hợp lệ.
/// </summary>
public class CreateOrderRequest
{
    [Required(ErrorMessage = "CustomerName is required.")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "UnitPrice must be greater than zero.")]
    public decimal UnitPrice { get; set; }
}
