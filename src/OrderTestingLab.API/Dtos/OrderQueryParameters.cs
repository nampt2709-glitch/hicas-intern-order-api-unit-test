using System.ComponentModel.DataAnnotations;

namespace OrderTestingLab.Dtos;

/// <summary>
/// Tham số phân trang cho GET /api/orders.
/// </summary>
public class OrderQueryParameters
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Trang bắt đầu từ 1.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")]
    public int Page { get; set; } = 1;

    /// <summary>Kích thước trang (1–100).</summary>
    [Range(1, MaxPageSize, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = DefaultPageSize;
}
