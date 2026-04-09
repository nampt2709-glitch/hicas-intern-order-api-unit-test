using Microsoft.AspNetCore.Mvc;
using OrderTestingLab.Dtos;
using OrderTestingLab.Interfaces;

namespace OrderTestingLab.Controllers;

/// <summary>
/// API CRUD Order — validation tự động nhờ [ApiController] + DataAnnotations trên DTO/query.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>Danh sách có phân trang (mặc định page=1, pageSize=20).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedOrdersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedOrdersResponse>> GetPaged([FromQuery] OrderQueryParameters query, CancellationToken cancellationToken)
    {
        var result = await _orderService.GetPagedAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy toàn bộ đơn (không phân trang) — dùng khi dataset nhỏ.</summary>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _orderService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Lấy một đơn theo Id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderService.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();
        return Ok(order);
    }

    /// <summary>Tạo đơn mới.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderResponse>> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _orderService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Cập nhật toàn phần đơn (PUT).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> Update(Guid id, [FromBody] UpdateOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _orderService.UpdateAsync(id, request, cancellationToken);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    /// <summary>Xóa đơn.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _orderService.DeleteAsync(id, cancellationToken);
        if (!deleted)
            return NotFound();
        return NoContent();
    }
}
