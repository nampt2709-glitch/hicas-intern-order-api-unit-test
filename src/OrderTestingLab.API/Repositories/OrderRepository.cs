using Microsoft.EntityFrameworkCore;
using OrderTestingLab.Entities;
using OrderTestingLab.Interfaces;
using OrderTestingLab.Persistence;

namespace OrderTestingLab.Repositories;

/// <summary>
/// Triển khai lưu/đọc/cập nhật/xóa Order qua EF Core.
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _context.Orders.AddAsync(order, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetAllOrderedByCreatedAtDescAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> GetPagedOrderedByCreatedAtDescAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Orders.AsNoTracking();
        var total = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery
            .OrderByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<bool> UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Orders.FindAsync(new object[] { order.Id }, cancellationToken);
        if (existing is null)
            return false;

        existing.CustomerName = order.CustomerName;
        existing.Email = order.Email;
        existing.Quantity = order.Quantity;
        existing.UnitPrice = order.UnitPrice;
        existing.TotalAmount = order.TotalAmount;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Orders.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null)
            return false;

        _context.Orders.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
