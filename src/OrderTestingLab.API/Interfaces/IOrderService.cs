using OrderTestingLab.Dtos;

namespace OrderTestingLab.Interfaces;

/// <summary>
/// Dịch vụ nghiệp vụ Order (CRUD + phân trang).
/// </summary>
public interface IOrderService
{
    Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);

    Task<OrderResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PagedOrdersResponse> GetPagedAsync(OrderQueryParameters query, CancellationToken cancellationToken = default);

    Task<OrderResponse?> UpdateAsync(Guid id, UpdateOrderRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
