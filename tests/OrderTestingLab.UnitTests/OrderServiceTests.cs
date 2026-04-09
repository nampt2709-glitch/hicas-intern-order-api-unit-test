using FluentAssertions;
using Moq;
using OrderTestingLab.Dtos;
using OrderTestingLab.Entities;
using OrderTestingLab.Interfaces;
using OrderTestingLab.Services;

namespace OrderTestingLab.UnitTests;

/// <summary>
/// Unit test cho <see cref="OrderService"/> — mock <see cref="IOrderRepository"/> (không DB).
/// </summary>
/// <remarks>
/// <para><b>Quy tắc F.I.R.S.T</b> (mỗi test cố gắng đáp ứng):</para>
/// <list type="bullet">
/// <item><b>F</b>ast — chạy nhanh, không I/O thật.</item>
/// <item><b>I</b>ndependent — không phụ thuộc thứ tự chạy; mock tự chứa kịch bản.</item>
/// <item><b>R</b>epeatable — kết quả ổn định mỗi lần chạy.</item>
/// <item><b>S</b>elf-validating — kết luận đúng/sai nhờ Assert rõ ràng, không cần kiểm tra tay.</item>
/// <item><b>T</b>imely — kiểm tra hành vi theo đúng hợp đồng service/repository.</item>
/// </list>
/// <para>Mỗi test dùng cấu trúc <b>Arrange – Act – Assert</b> và ghi chú trong code.</para>
/// </remarks>
public class OrderServiceTests
{
    /// <summary>Arrange — tạo mock repository với hành vi mặc định an toàn cho hầu hết test.</summary>
    private static Mock<IOrderRepository> CreateDefaultRepositoryMock()
    {
        var mock = new Mock<IOrderRepository>();
        mock.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Order?)null);
        mock.Setup(r => r.GetAllOrderedByCreatedAtDescAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Order>());
        mock.Setup(r => r.GetPagedOrderedByCreatedAtDescAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Order>(), 0));
        mock.Setup(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        mock.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        return mock;
    }

    /// <summary>
    /// Nhiệm vụ: Xác nhận khi tạo đơn, <see cref="OrderService"/> loại bỏ khoảng trắng thừa ở tên khách (quy tắc nghiệp vụ trim).
    /// </summary>
    [Fact]
    public async Task US01_CreateAsync_TrimsCustomerName()
    {
        // Arrange — Mock repository + service; request có khoảng trắng quanh tên.
        var mock = CreateDefaultRepositoryMock();
        var sut = new OrderService(mock.Object);
        var request = new CreateOrderRequest { CustomerName = "  A  ", Email = "a@test.com", Quantity = 1, UnitPrice = 1m };

        // Act — Gọi tạo đơn.
        var res = await sut.CreateAsync(request);

        // Assert — Tên trong response đã được trim về "A".
        res.CustomerName.Should().Be("A");
    }

    /// <summary>
    /// Nhiệm vụ: Xác nhận email sau khi tạo được chuẩn hóa về chữ thường (lower-case).
    /// </summary>
    [Fact]
    public async Task US02_CreateAsync_LowercasesEmail()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.CreateAsync(new CreateOrderRequest { CustomerName = "A", Email = "  A@Test.COM ", Quantity = 1, UnitPrice = 1m });

        // Assert
        res.Email.Should().Be("a@test.com");
    }

    /// <summary>
    /// Nhiệm vụ: Kiểm tra TotalAmount = Quantity × UnitPrice khi tạo đơn.
    /// </summary>
    [Fact]
    public async Task US03_CreateAsync_ComputesTotalAmount()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.CreateAsync(new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 4, UnitPrice = 2.5m });

        // Assert
        res.TotalAmount.Should().Be(10m);
    }

    /// <summary>
    /// Nhiệm vụ: Đảm bảo mỗi lần tạo đơn chỉ gọi repository Add đúng một lần (không lặp lưu).
    /// </summary>
    [Fact]
    public async Task US04_CreateAsync_CallsAddAsyncOnce()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        var sut = new OrderService(mock.Object);

        // Act
        await sut.CreateAsync(new CreateOrderRequest { CustomerName = "B", Email = "b@test.com", Quantity = 1, UnitPrice = 5m });

        // Assert
        mock.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: Khi repository không có bản ghi, GetById phải trả null (không bịa dữ liệu).
    /// </summary>
    [Fact]
    public async Task US05_GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.GetByIdAsync(Guid.NewGuid());

        // Assert
        res.Should().BeNull();
    }

    /// <summary>
    /// Nhiệm vụ: Khi repository trả entity, service map đủ sang OrderResponse (Id, TotalAmount, v.v.).
    /// </summary>
    [Fact]
    public async Task US06_GetByIdAsync_ReturnsMapped_WhenFound()
    {
        // Arrange — Cố định Id và dữ liệu trả về từ mock.
        var id = Guid.NewGuid();
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Order
        {
            Id = id,
            CustomerName = "C",
            Email = "c@test.com",
            Quantity = 2,
            UnitPrice = 3m,
            TotalAmount = 6m,
            CreatedAt = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        });
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.GetByIdAsync(id);

        // Assert
        res.Should().NotBeNull();
        res!.Id.Should().Be(id);
        res.TotalAmount.Should().Be(6m);
    }

    /// <summary>
    /// Nhiệm vụ: GetAll trả về danh sách rỗng khi repository không có đơn nào.
    /// </summary>
    [Fact]
    public async Task US07_GetAllAsync_ReturnsEmpty_WhenNone()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.GetAllAsync();

        // Assert
        res.Should().BeEmpty();
    }

    /// <summary>
    /// Nhiệm vụ: GetAll map từng Order entity sang OrderResponse (ít nhất một phần tử).
    /// </summary>
    [Fact]
    public async Task US08_GetAllAsync_MapsAllItems()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        var o1 = new Order { Id = Guid.NewGuid(), CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow };
        mock.Setup(r => r.GetAllOrderedByCreatedAtDescAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { o1 });
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.GetAllAsync();

        // Assert
        res.Should().HaveCount(1);
        res[0].CustomerName.Should().Be("A");
    }

    /// <summary>
    /// Nhiệm vụ: GetPaged trả về Items, TotalCount, Page, TotalPages nhất quán khi có 1 bản ghi.
    /// </summary>
    [Fact]
    public async Task US09_GetPagedAsync_ReturnsItemsAndTotal()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        var o1 = new Order { Id = Guid.NewGuid(), CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow };
        mock.Setup(r => r.GetPagedOrderedByCreatedAtDescAsync(0, 20, It.IsAny<CancellationToken>())).ReturnsAsync((new[] { o1 }, 1));
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.GetPagedAsync(new OrderQueryParameters { Page = 1, PageSize = 20 });

        // Assert
        res.Items.Should().HaveCount(1);
        res.TotalCount.Should().Be(1);
        res.Page.Should().Be(1);
        res.TotalPages.Should().Be(1);
    }

    /// <summary>
    /// Nhiệm vụ: Trang 2 phải gọi repository với skip = pageSize (ở đây skip 10 khi page=2, size=10).
    /// </summary>
    [Fact]
    public async Task US10_GetPagedAsync_SecondPage_UsesSkip()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.GetPagedOrderedByCreatedAtDescAsync(10, 10, It.IsAny<CancellationToken>())).ReturnsAsync((Array.Empty<Order>(), 25));
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.GetPagedAsync(new OrderQueryParameters { Page = 2, PageSize = 10 });

        // Assert
        res.Page.Should().Be(2);
        res.TotalCount.Should().Be(25);
        mock.Verify(r => r.GetPagedOrderedByCreatedAtDescAsync(10, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: Page &lt; 1 được chuẩn hóa thành 1 (skip vẫn 0 cho trang đầu).
    /// </summary>
    [Fact]
    public async Task US11_GetPagedAsync_PageLessThanOne_NormalizedToOne()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.GetPagedOrderedByCreatedAtDescAsync(0, 20, It.IsAny<CancellationToken>())).ReturnsAsync((Array.Empty<Order>(), 0));
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.GetPagedAsync(new OrderQueryParameters { Page = 0, PageSize = 20 });

        // Assert
        res.Page.Should().Be(1);
        mock.Verify(r => r.GetPagedOrderedByCreatedAtDescAsync(0, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: PageSize vượt max được kẹp về 100 (tránh query quá lớn).
    /// </summary>
    [Fact]
    public async Task US12_GetPagedAsync_PageSizeClampedToMax()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.GetPagedOrderedByCreatedAtDescAsync(0, 100, It.IsAny<CancellationToken>())).ReturnsAsync((Array.Empty<Order>(), 0));
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.GetPagedAsync(new OrderQueryParameters { Page = 1, PageSize = 500 });

        // Assert
        res.PageSize.Should().Be(100);
    }

    /// <summary>
    /// Nhiệm vụ: PageSize &lt; 1 được kẹp tối thiểu là 1.
    /// </summary>
    [Fact]
    public async Task US13_GetPagedAsync_PageSizeClampedToMin()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.GetPagedOrderedByCreatedAtDescAsync(0, 1, It.IsAny<CancellationToken>())).ReturnsAsync((Array.Empty<Order>(), 0));
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.GetPagedAsync(new OrderQueryParameters { Page = 1, PageSize = 0 });

        // Assert
        res.PageSize.Should().Be(1);
    }

    /// <summary>
    /// Nhiệm vụ: Update khi không tồn tại Id phải trả null (service không tự tạo bản ghi).
    /// </summary>
    [Fact]
    public async Task US14_UpdateAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.UpdateAsync(Guid.NewGuid(), new UpdateOrderRequest { CustomerName = "X", Email = "x@test.com", Quantity = 1, UnitPrice = 1 });

        // Assert
        res.Should().BeNull();
    }

    /// <summary>
    /// Nhiệm vụ: Update thành công trả về DTO đọc lại từ DB (sau khi mock GetById).
    /// </summary>
    [Fact]
    public async Task US15_UpdateAsync_ReturnsDto_WhenUpdated()
    {
        // Arrange
        var id = Guid.NewGuid();
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.UpdateAsync(It.Is<Order>(o => o.Id == id), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Order
        {
            Id = id,
            CustomerName = "New",
            Email = "new@test.com",
            Quantity = 2,
            UnitPrice = 5m,
            TotalAmount = 10m,
            CreatedAt = DateTime.UtcNow
        });
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.UpdateAsync(id, new UpdateOrderRequest { CustomerName = " New ", Email = " NEW@Test.com ", Quantity = 2, UnitPrice = 5m });

        // Assert
        res.Should().NotBeNull();
        res!.CustomerName.Should().Be("New");
        res.Email.Should().Be("new@test.com");
    }

    /// <summary>
    /// Nhiệm vụ: Gọi Update với TotalAmount đã tính lại đúng (Quantity × UnitPrice).
    /// </summary>
    [Fact]
    public async Task US16_UpdateAsync_RecalculatesTotal()
    {
        // Arrange
        var id = Guid.NewGuid();
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Order
        {
            Id = id, CustomerName = "A", Email = "a@test.com", Quantity = 3, UnitPrice = 4m, TotalAmount = 12m, CreatedAt = DateTime.UtcNow
        });
        var sut = new OrderService(mock.Object);
        mock.Invocations.Clear();

        // Act
        await sut.UpdateAsync(id, new UpdateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 3, UnitPrice = 4m });

        // Assert
        mock.Verify(r => r.UpdateAsync(It.Is<Order>(o => o.TotalAmount == 12m), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: Delete trả false khi repository không xóa được (Id không tồn tại).
    /// </summary>
    [Fact]
    public async Task US17_DeleteAsync_ReturnsFalse_WhenMissing()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        var sut = new OrderService(mock.Object);

        // Act
        var ok = await sut.DeleteAsync(Guid.NewGuid());

        // Assert
        ok.Should().BeFalse();
    }

    /// <summary>
    /// Nhiệm vụ: Delete trả true khi repository báo xóa thành công.
    /// </summary>
    [Fact]
    public async Task US18_DeleteAsync_ReturnsTrue_WhenDeleted()
    {
        // Arrange
        var id = Guid.NewGuid();
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var sut = new OrderService(mock.Object);

        // Act
        var ok = await sut.DeleteAsync(id);

        // Assert
        ok.Should().BeTrue();
    }

    /// <summary>
    /// Nhiệm vụ: Mỗi đơn mới có Id không rỗng (Guid được sinh).
    /// </summary>
    [Fact]
    public async Task US19_CreateAsync_GeneratesNewGuid()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        var sut = new OrderService(mock.Object);

        // Act
        await sut.CreateAsync(new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1 });

        // Assert
        mock.Verify(r => r.AddAsync(It.Is<Order>(o => o.Id != Guid.Empty), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: CreatedAt khi tạo là UTC và khác default.
    /// </summary>
    [Fact]
    public async Task US20_CreateAsync_SetsCreatedAtUtc()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        var sut = new OrderService(mock.Object);

        // Act
        await sut.CreateAsync(new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1 });

        // Assert
        mock.Verify(r => r.AddAsync(It.Is<Order>(o => o.CreatedAt != default && o.CreatedAt.Kind == DateTimeKind.Utc), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: TotalPages làm tròn lên khi TotalCount không chia hết PageSize.
    /// </summary>
    [Fact]
    public async Task US21_GetPagedAsync_TotalPages_WhenNotDivisible()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.GetPagedOrderedByCreatedAtDescAsync(0, 3, It.IsAny<CancellationToken>())).ReturnsAsync((Array.Empty<Order>(), 10));
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.GetPagedAsync(new OrderQueryParameters { Page = 1, PageSize = 3 });

        // Assert
        res.TotalPages.Should().Be(4);
    }

    /// <summary>
    /// Nhiệm vụ: GetAll map đủ số phần tử khi repository trả nhiều Order.
    /// </summary>
    [Fact]
    public async Task US22_GetAllAsync_MultipleOrders_AllMapped()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        var list = new[]
        {
            new Order { Id = Guid.NewGuid(), CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow },
            new Order { Id = Guid.NewGuid(), CustomerName = "B", Email = "b@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow }
        };
        mock.Setup(r => r.GetAllOrderedByCreatedAtDescAsync(It.IsAny<CancellationToken>())).ReturnsAsync(list);
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.GetAllAsync();

        // Assert
        res.Should().HaveCount(2);
    }

    /// <summary>
    /// Nhiệm vụ: Dữ liệu gửi xuống repository khi Update đã trim + lowercase trước khi lưu.
    /// </summary>
    [Fact]
    public async Task US23_UpdateAsync_PassesTrimmedNames_ToRepository()
    {
        // Arrange
        var id = Guid.NewGuid();
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Order
        {
            Id = id, CustomerName = "X", Email = "x@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow
        });
        var sut = new OrderService(mock.Object);

        // Act
        await sut.UpdateAsync(id, new UpdateOrderRequest { CustomerName = "  Y  ", Email = "  Y@Z.COM ", Quantity = 1, UnitPrice = 1 });

        // Assert
        mock.Verify(r => r.UpdateAsync(It.Is<Order>(o => o.CustomerName == "Y" && o.Email == "y@z.com"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: Trang vượt quá số bản ghi vẫn trả TotalCount đúng, Items rỗng.
    /// </summary>
    [Fact]
    public async Task US24_GetPagedAsync_EmptyPage_StillReturnsMeta()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.GetPagedOrderedByCreatedAtDescAsync(100, 10, It.IsAny<CancellationToken>())).ReturnsAsync((Array.Empty<Order>(), 5));
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.GetPagedAsync(new OrderQueryParameters { Page = 11, PageSize = 10 });

        // Assert
        res.Items.Should().BeEmpty();
        res.TotalCount.Should().Be(5);
    }

    /// <summary>
    /// Nhiệm vụ: GetById map đầy đủ Quantity, UnitPrice, CreatedAt từ entity.
    /// </summary>
    [Fact]
    public async Task US25_GetByIdAsync_MapsAllFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var created = new DateTime(2023, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Order
        {
            Id = id,
            CustomerName = "Full",
            Email = "full@test.com",
            Quantity = 7,
            UnitPrice = 2m,
            TotalAmount = 14m,
            CreatedAt = created
        });
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.GetByIdAsync(id);

        // Assert
        res!.Quantity.Should().Be(7);
        res.UnitPrice.Should().Be(2m);
        res.CreatedAt.Should().Be(created);
    }

    /// <summary>
    /// Nhiệm vụ: Tổng tiền lưu xuống repository khi Create khớp Quantity × UnitPrice (số thập phân).
    /// </summary>
    [Fact]
    public async Task US26_CreateAsync_VerifyRepositoryReceivesNormalizedTotals()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        var sut = new OrderService(mock.Object);

        // Act
        await sut.CreateAsync(new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 10, UnitPrice = 0.5m });

        // Assert
        mock.Verify(r => r.AddAsync(It.Is<Order>(o => o.TotalAmount == 5m), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: Delete ủy quyền đúng Guid xuống repository.
    /// </summary>
    [Fact]
    public async Task US27_DeleteAsync_DelegatesToRepository()
    {
        // Arrange
        var id = Guid.NewGuid();
        var mock = CreateDefaultRepositoryMock();
        var sut = new OrderService(mock.Object);

        // Act
        await sut.DeleteAsync(id);

        // Assert
        mock.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: OrderQueryParameters mặc định dùng DefaultPageSize khi gọi GetPaged.
    /// </summary>
    [Fact]
    public async Task US28_GetPagedAsync_PageSize_DefaultFromQueryObject()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.GetPagedOrderedByCreatedAtDescAsync(0, OrderQueryParameters.DefaultPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Order>(), 0));
        var sut = new OrderService(mock.Object);

        // Act
        await sut.GetPagedAsync(new OrderQueryParameters());

        // Assert
        mock.Verify(r => r.GetPagedOrderedByCreatedAtDescAsync(0, OrderQueryParameters.DefaultPageSize, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: Sau Update thành công, service gọi GetById một lần để trả DTO mới nhất.
    /// </summary>
    [Fact]
    public async Task US29_UpdateAsync_GetByIdCalledAfterSuccessfulUpdate()
    {
        // Arrange
        var id = Guid.NewGuid();
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Order
        {
            Id = id, CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1, TotalAmount = 1, CreatedAt = DateTime.UtcNow
        });
        var sut = new OrderService(mock.Object);

        // Act
        await sut.UpdateAsync(id, new UpdateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1 });

        // Assert
        mock.Verify(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nhiệm vụ: PageSize âm được kẹp về 1 (an toàn khi tham số lỗi).
    /// </summary>
    [Fact]
    public async Task US30_GetPagedAsync_ClampsPageSize_WhenNegative()
    {
        // Arrange
        var mock = CreateDefaultRepositoryMock();
        mock.Setup(r => r.GetPagedOrderedByCreatedAtDescAsync(0, 1, It.IsAny<CancellationToken>())).ReturnsAsync((Array.Empty<Order>(), 0));
        var sut = new OrderService(mock.Object);

        // Act
        var res = await sut.GetPagedAsync(new OrderQueryParameters { Page = 1, PageSize = -5 });

        // Assert
        res.PageSize.Should().Be(1);
    }
}
