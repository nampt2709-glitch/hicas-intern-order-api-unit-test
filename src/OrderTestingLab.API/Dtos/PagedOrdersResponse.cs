namespace OrderTestingLab.Dtos;

/// <summary>
/// Kết quả phân trang danh sách đơn hàng.
/// </summary>
public class PagedOrdersResponse
{
    public IReadOnlyList<OrderResponse> Items { get; init; } = Array.Empty<OrderResponse>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
