using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using OrderTestingLab.Controllers;
using OrderTestingLab.Dtos;
using OrderTestingLab.Interfaces;

namespace OrderTestingLab.UnitTests;

/// <summary>
/// Mục đích: Kiểm tra <see cref="OrdersController"/> qua mock <see cref="IOrderService"/> (không DB, không HTTP host).
/// </summary>
/// <remarks>
/// <para><b>Tầng</b>: Unit — chỉ controller + mock service.</para>
/// <para><b>F.I.R.S.T</b>: Fast, Independent, Repeatable, Self-validating, Timely (mapping status code → IActionResult).</para>
/// <para><b>3A</b>: Mỗi test ghi Arrange–Act–Assert trong thân method.</para>
/// <para>Vì sao ở Unit: cô lập hành vi controller khỏi EF/JWT.</para>
/// <para>Mock IUrlHelper (CreatedAtAction) khi cần.</para>
/// </remarks>
public class OrdersControllerTests
{
    /// <summary>
    /// Nhiệm vụ: POST tạo đơn phải trả <see cref="CreatedAtActionResult"/> và ActionName trỏ tới GetById (REST quy ước).
    /// </summary>
    [Fact]
    public async Task UC01_Create_ReturnsCreatedAtAction_WithActionNameGetById()
    {
        // Arrange — Mock service trả OrderResponse; mock Url để CreatedAtAction không lỗi.
        var id = Guid.NewGuid();
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.CreateAsync(It.IsAny<CreateOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderResponse { Id = id, CustomerName = "J", Email = "j@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow });
        var c = new OrdersController(sm.Object);
        var mockUrl = new Mock<IUrlHelper>();
        mockUrl.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("http://localhost/x");
        c.Url = mockUrl.Object;

        // Act — Gọi action Create.
        var result = await c.Create(new CreateOrderRequest { CustomerName = "J", Email = "j@test.com", Quantity = 1, UnitPrice = 1 }, CancellationToken.None);

        // Assert — Kiểu CreatedAtAction và tên action đích.
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        ((CreatedAtActionResult)result.Result!).ActionName.Should().Be(nameof(OrdersController.GetById));
    }

    /// <summary>
    /// Nhiệm vụ: GET theo Id khi service có dữ liệu phải trả 200 OK.
    /// </summary>
    [Fact]
    public async Task UC02_GetById_ReturnsOk_WhenFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new OrderResponse
        {
            Id = id, CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow
        });
        var c = new OrdersController(sm.Object);

        // Act
        var result = await c.GetById(id, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    /// <summary>
    /// Nhiệm vụ: GET theo Id khi không có đơn phải trả 404 NotFound.
    /// </summary>
    [Fact]
    public async Task UC03_GetById_Returns404_WhenMissing()
    {
        // Arrange
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((OrderResponse?)null);
        var c = new OrdersController(sm.Object);

        // Act
        var result = await c.GetById(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    /// <summary>
    /// Nhiệm vụ: GET phân trang luôn bọc kết quả trong 200 OK (OkObjectResult).
    /// </summary>
    [Fact]
    public async Task UC04_GetPaged_ReturnsOk()
    {
        // Arrange
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.GetPagedAsync(It.IsAny<OrderQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedOrdersResponse { Items = Array.Empty<OrderResponse>(), Page = 1, PageSize = 20, TotalCount = 0 });
        var c = new OrdersController(sm.Object);

        // Act
        var result = await c.GetPaged(new OrderQueryParameters(), CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    /// <summary>
    /// Nhiệm vụ: GET toàn bộ đơn (all) trả 200 OK khi service trả danh sách (kể cả rỗng).
    /// </summary>
    [Fact]
    public async Task UC05_GetAll_ReturnsOk()
    {
        // Arrange
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<OrderResponse>());
        var c = new OrdersController(sm.Object);

        // Act
        var result = await c.GetAll(CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    /// <summary>
    /// Nhiệm vụ: PUT cập nhật khi service tìm thấy đơn phải trả 200 và body.
    /// </summary>
    [Fact]
    public async Task UC06_Update_ReturnsOk_WhenFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.UpdateAsync(id, It.IsAny<UpdateOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderResponse { Id = id, CustomerName = "U", Email = "u@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow });
        var c = new OrdersController(sm.Object);

        // Act
        var result = await c.Update(id, new UpdateOrderRequest { CustomerName = "U", Email = "u@test.com", Quantity = 1, UnitPrice = 1 }, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    /// <summary>
    /// Nhiệm vụ: PUT khi không tồn tại Id phải trả 404.
    /// </summary>
    [Fact]
    public async Task UC07_Update_Returns404_WhenMissing()
    {
        // Arrange
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateOrderRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((OrderResponse?)null);
        var c = new OrdersController(sm.Object);

        // Act
        var result = await c.Update(Guid.NewGuid(), new UpdateOrderRequest { CustomerName = "U", Email = "u@test.com", Quantity = 1, UnitPrice = 1 }, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    /// <summary>
    /// Nhiệm vụ: DELETE thành công phải trả 204 No Content (không body).
    /// </summary>
    [Fact]
    public async Task UC08_Delete_Returns204_WhenDeleted()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var c = new OrdersController(sm.Object);

        // Act
        var result = await c.Delete(id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    /// <summary>
    /// Nhiệm vụ: DELETE khi không có bản ghi phải trả 404.
    /// </summary>
    [Fact]
    public async Task UC09_Delete_Returns404_WhenMissing()
    {
        // Arrange
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var c = new OrdersController(sm.Object);

        // Act
        var result = await c.Delete(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    /// <summary>
    /// Nhiệm vụ: Controller phải gọi đúng IOrderService.CreateAsync đúng một lần với request đã truyền.
    /// </summary>
    [Fact]
    public async Task UC10_Create_CallsServiceCreate()
    {
        // Arrange
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.CreateAsync(It.IsAny<CreateOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderResponse { Id = Guid.NewGuid(), CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow });
        var c = new OrdersController(sm.Object);
        var mockUrl = new Mock<IUrlHelper>();
        mockUrl.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("x");
        c.Url = mockUrl.Object;
        var req = new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1 };

        // Act
        await c.Create(req, CancellationToken.None);

        // Assert
        sm.Verify(s => s.CreateAsync(req, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: GetPaged chuyển nguyên OrderQueryParameters xuống service (đúng page/size).
    /// </summary>
    [Fact]
    public async Task UC11_GetPaged_PassesQueryToService()
    {
        // Arrange
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.GetPagedAsync(It.IsAny<OrderQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedOrdersResponse { Items = Array.Empty<OrderResponse>(), Page = 2, PageSize = 5, TotalCount = 0 });
        var c = new OrdersController(sm.Object);
        var q = new OrderQueryParameters { Page = 2, PageSize = 5 };

        // Act
        await c.GetPaged(q, CancellationToken.None);

        // Assert
        sm.Verify(s => s.GetPagedAsync(q, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: GetAll gọi IOrderService.GetAllAsync đúng một lần.
    /// </summary>
    [Fact]
    public async Task UC12_GetAll_CallsService()
    {
        // Arrange
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<OrderResponse>());
        var c = new OrdersController(sm.Object);

        // Act
        await c.GetAll(CancellationToken.None);

        // Assert
        sm.Verify(s => s.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: GetById truyền đúng Guid lên service.
    /// </summary>
    [Fact]
    public async Task UC13_GetById_PassesId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((OrderResponse?)null);
        var c = new OrdersController(sm.Object);

        // Act
        await c.GetById(id, CancellationToken.None);

        // Assert
        sm.Verify(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: Update truyền đúng Id và body xuống service.
    /// </summary>
    [Fact]
    public async Task UC14_Update_PassesIdAndBody()
    {
        // Arrange
        var id = Guid.NewGuid();
        var body = new UpdateOrderRequest { CustomerName = "X", Email = "x@test.com", Quantity = 2, UnitPrice = 3m };
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.UpdateAsync(id, body, It.IsAny<CancellationToken>())).ReturnsAsync((OrderResponse?)null);
        var c = new OrdersController(sm.Object);

        // Act
        await c.Update(id, body, CancellationToken.None);

        // Assert
        sm.Verify(s => s.UpdateAsync(id, body, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: Delete truyền đúng Id xuống service.
    /// </summary>
    [Fact]
    public async Task UC15_Delete_PassesId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var c = new OrdersController(sm.Object);

        // Act
        await c.Delete(id, CancellationToken.None);

        // Assert
        sm.Verify(s => s.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: CreatedAtAction chứa route value "id" khớp Id đơn vừa tạo (để client điều hướng GET).
    /// </summary>
    [Fact]
    public async Task UC16_Create_ResponseContainsLocationRouteValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.CreateAsync(It.IsAny<CreateOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderResponse { Id = id, CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow });
        var c = new OrdersController(sm.Object);
        var mockUrl = new Mock<IUrlHelper>();
        mockUrl.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("http://localhost");
        c.Url = mockUrl.Object;

        // Act
        var result = await c.Create(new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1 }, CancellationToken.None);
        var created = (CreatedAtActionResult)result.Result!;

        // Assert
        created.RouteValues!["id"].Should().Be(id);
    }

    /// <summary>
    /// Nhiệm vụ: GetPaged trả về đúng object PagedOrdersResponse mà service cung cấp (không bọc sai lớp).
    /// </summary>
    [Fact]
    public async Task UC17_GetPaged_ReturnsPagedPayload()
    {
        // Arrange
        var sm = new Mock<IOrderService>();
        var payload = new PagedOrdersResponse
        {
            Items = new[] { new OrderResponse { Id = Guid.NewGuid(), CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow } },
            Page = 1,
            PageSize = 10,
            TotalCount = 1
        };
        sm.Setup(s => s.GetPagedAsync(It.IsAny<OrderQueryParameters>(), It.IsAny<CancellationToken>())).ReturnsAsync(payload);
        var c = new OrdersController(sm.Object);

        // Act
        var result = await c.GetPaged(new OrderQueryParameters(), CancellationToken.None);
        var ok = (OkObjectResult)result.Result!;

        // Assert
        ok.Value.Should().BeSameAs(payload);
    }

    /// <summary>
    /// Nhiệm vụ: GetById trả về cùng tham chiếu DTO mà service trả (OkObjectResult.Value).
    /// </summary>
    [Fact]
    public async Task UC18_GetById_ReturnsSameDto()
    {
        // Arrange
        var dto = new OrderResponse { Id = Guid.NewGuid(), CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow };
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.GetByIdAsync(dto.Id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        var c = new OrdersController(sm.Object);

        // Act
        var result = await c.GetById(dto.Id, CancellationToken.None);

        // Assert
        ((OkObjectResult)result.Result!).Value.Should().BeSameAs(dto);
    }

    /// <summary>
    /// Nhiệm vụ: Update thành công trả về DTO đúng như service trả (200 OK).
    /// </summary>
    [Fact]
    public async Task UC19_Update_ReturnsOkBody()
    {
        // Arrange
        var dto = new OrderResponse { Id = Guid.NewGuid(), CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow };
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.UpdateAsync(dto.Id, It.IsAny<UpdateOrderRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        var c = new OrdersController(sm.Object);

        // Act
        var result = await c.Update(dto.Id, new UpdateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1 }, CancellationToken.None);

        // Assert
        ((OkObjectResult)result.Result!).Value.Should().BeSameAs(dto);
    }

    /// <summary>
    /// Nhiệm vụ: NoContent 204 không có body; StatusCode đúng chuẩn.
    /// </summary>
    [Fact]
    public async Task UC20_Delete_NoContent_HasNoBody()
    {
        // Arrange
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var c = new OrdersController(sm.Object);

        // Act
        var result = (NoContentResult)await c.Delete(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(204);
    }

    /// <summary>
    /// Nhiệm vụ: CreatedAtAction phải mang HTTP 201 Created (REST tạo tài nguyên).
    /// </summary>
    [Fact]
    public async Task UC21_Create_ReturnsStatusCode201_OnCreatedAtActionResult()
    {
        var id = Guid.NewGuid();
        var sm = new Mock<IOrderService>();
        sm.Setup(s => s.CreateAsync(It.IsAny<CreateOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderResponse { Id = id, CustomerName = "J", Email = "j@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow });
        var c = new OrdersController(sm.Object);
        var mockUrl = new Mock<IUrlHelper>();
        mockUrl.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("http://localhost/x");
        c.Url = mockUrl.Object;

        var result = await c.Create(new CreateOrderRequest { CustomerName = "J", Email = "j@test.com", Quantity = 1, UnitPrice = 1 }, CancellationToken.None);

        var created = (CreatedAtActionResult)result.Result!;
        created.StatusCode.Should().Be(201);
    }
}
