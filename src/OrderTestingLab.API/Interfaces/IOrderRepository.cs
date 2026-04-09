using OrderTestingLab.Entities;

namespace OrderTestingLab.Interfaces;

/// <summary>
/// Trừu tượng hóa lớp truy cập dữ liệu Order (CRUD).
/// </summary>
public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetAllOrderedByCreatedAtDescAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Order> Items, int TotalCount)> GetPagedOrderedByCreatedAtDescAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>Cập nhật theo Id; trả về false nếu không tồn tại.</summary>
    Task<bool> UpdateAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>Xóa theo Id; trả về false nếu không tồn tại.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
