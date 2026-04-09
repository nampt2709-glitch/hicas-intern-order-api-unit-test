using AutoMapper;
using OrderTestingLab.Dtos;
using OrderTestingLab.Entities;
using OrderTestingLab.Interfaces;

namespace OrderTestingLab.Services;

/// <summary>
/// Áp dụng quy tắc nghiệp vụ: trim tên, email lower-case, tính TotalAmount; map entity → DTO qua AutoMapper.
/// </summary>
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IMapper _mapper;

    public OrderService(IOrderRepository orderRepository, IMapper mapper)
    {
        _orderRepository = orderRepository;
        _mapper = mapper;
    }

    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        var customerName = request.CustomerName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var quantity = request.Quantity;
        var unitPrice = request.UnitPrice;
        var totalAmount = quantity * unitPrice;

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerName = customerName,
            Email = email,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalAmount = totalAmount,
            CreatedAt = DateTime.UtcNow
        };

        await _orderRepository.AddAsync(order, cancellationToken);
        return _mapper.Map<OrderResponse>(order);
    }

    public async Task<OrderResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken);
        return order is null ? null : _mapper.Map<OrderResponse>(order);
    }

    public async Task<IReadOnlyList<OrderResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await _orderRepository.GetAllOrderedByCreatedAtDescAsync(cancellationToken);
        return list.Select(o => _mapper.Map<OrderResponse>(o)).ToList();
    }

    public async Task<PagedOrdersResponse> GetPagedAsync(OrderQueryParameters query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, OrderQueryParameters.MaxPageSize);
        var skip = (page - 1) * pageSize;

        var (items, total) = await _orderRepository.GetPagedOrderedByCreatedAtDescAsync(skip, pageSize, cancellationToken);
        return new PagedOrdersResponse
        {
            Items = items.Select(o => _mapper.Map<OrderResponse>(o)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<OrderResponse?> UpdateAsync(Guid id, UpdateOrderRequest request, CancellationToken cancellationToken = default)
    {
        var customerName = request.CustomerName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var quantity = request.Quantity;
        var unitPrice = request.UnitPrice;
        var totalAmount = quantity * unitPrice;

        var order = new Order
        {
            Id = id,
            CustomerName = customerName,
            Email = email,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalAmount = totalAmount,
            CreatedAt = default // không ghi đè CreatedAt trong repository
        };

        var updated = await _orderRepository.UpdateAsync(order, cancellationToken);
        if (!updated)
            return null;

        return await GetByIdAsync(id, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _orderRepository.DeleteAsync(id, cancellationToken);
    }
}
