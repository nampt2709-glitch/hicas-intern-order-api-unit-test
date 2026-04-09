using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderTestingLab.Dtos;
using OrderTestingLab.Persistence;

namespace OrderTestingLab.IntegrationTests;

/// <summary>
/// Integration test — gọi HTTP qua host thật (<see cref="CustomWebApplicationFactory"/>), SQLite In-Memory, có khi đọc <see cref="AppDbContext"/>.
/// </summary>
/// <remarks>
/// <para><b>F.I.R.S.T</b>:</para>
/// <list type="bullet">
/// <item><b>F</b>ast — so với E2E GUI; không mạng ngoài.</item>
/// <item><b>I</b>ndependent — mỗi test gọi <c>ResetOrdersTableAsync()</c> trước (dữ liệu sạch).</item>
/// <item><b>R</b>epeatable — kết quả ổn định với DB in-memory.</item>
/// <item><b>S</b>elf-validating — assert status/body/DB rõ ràng.</item>
/// <item><b>T</b>imely — phản ánh hệ đường ống HTTP + EF thật.</item>
/// </list>
/// <para>Mỗi test ghi <b>Arrange – Act – Assert</b>; nhiệm vụ mô tả trong thẻ summary của từng test.</para>
/// </remarks>
public class OrdersIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OrdersIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>Arrange — xóa bảng Orders để test độc lập (F.I.R.S.T: Independent).</summary>
    private async Task ResetOrdersTableAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Orders");
    }

    /// <summary>
    /// Nhiệm vụ: POST hợp lệ trả 201, dữ liệu chuẩn hóa email trong DB và response.
    /// </summary>
    [Fact]
    public async Task IT01_Post_Valid_Returns201_And_Persisted()
    {
        // Arrange — DB sạch; body có khoảng trắng và email hỗn hợp.
        await ResetOrdersTableAsync();
        var request = new CreateOrderRequest { CustomerName = "  A  ", Email = "A@Test.com", Quantity = 2, UnitPrice = 3m };

        // Act — Gửi POST /api/orders.
        var response = await _client.PostAsJsonAsync("/api/orders", request);

        // Assert — 201 + kiểm tra dòng trong DB.
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        body.Should().NotBeNull();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == body!.Id);
        row.Should().NotBeNull();
        row!.Email.Should().Be("a@test.com");
    }

    /// <summary>
    /// Nhiệm vụ: Thiếu Email phải 400 và thông báo lỗi có chữ Email (validation).
    /// </summary>
    [Fact]
    public async Task IT02_Post_MissingEmail_Returns400()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", new { customerName = "X", quantity = 1, unitPrice = 1m });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("Email");
    }

    /// <summary>
    /// Nhiệm vụ: Quantity = 0 vi phạm Range → 400.
    /// </summary>
    [Fact]
    public async Task IT03_Post_InvalidQuantity_Returns400()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 0, UnitPrice = 1m });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Nhiệm vụ: UnitPrice = 0 vi phạm Range → 400.
    /// </summary>
    [Fact]
    public async Task IT04_Post_InvalidUnitPrice_Returns400()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 0m });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Nhiệm vụ: Sau khi tạo đơn, GET cùng Id trả 200.
    /// </summary>
    [Fact]
    public async Task IT05_GetById_Exists_Returns200()
    {
        // Arrange — Tạo một đơn.
        await ResetOrdersTableAsync();
        var created = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "G", Email = "g@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await created.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        // Act
        var get = await _client.GetAsync($"/api/orders/{id}");

        // Assert
        get.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Nhiệm vụ: GET Id ngẫu nhiên không tồn tại → 404.
    /// </summary>
    [Fact]
    public async Task IT06_GetById_NotExists_Returns404()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var r = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Nhiệm vụ: GET phân trang mặc định khi DB rỗng → 200, TotalCount 0.
    /// </summary>
    [Fact]
    public async Task IT07_GetPaged_Default_Returns200_And_Empty()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var r = await _client.GetAsync("/api/orders");
        var page = await r.Content.ReadFromJsonAsync<PagedOrdersResponse>();

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        page!.TotalCount.Should().Be(0);
        page.Items.Should().BeEmpty();
    }

    /// <summary>
    /// Nhiệm vụ: Sau khi tạo 1 đơn, phân trang phản ánh TotalCount = 1.
    /// </summary>
    [Fact]
    public async Task IT08_GetPaged_AfterCreate_ShowsTotalCount()
    {
        // Arrange
        await ResetOrdersTableAsync();
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });

        // Act
        var r = await _client.GetAsync("/api/orders?page=1&pageSize=10");
        var page = await r.Content.ReadFromJsonAsync<PagedOrdersResponse>();

        // Assert
        page!.TotalCount.Should().Be(1);
        page.Items.Should().HaveCount(1);
    }

    /// <summary>
    /// Nhiệm vụ: GET /all trả đủ số đơn đã tạo (2).
    /// </summary>
    [Fact]
    public async Task IT09_GetAll_ReturnsAllOrders()
    {
        // Arrange
        await ResetOrdersTableAsync();
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "B", Email = "b@test.com", Quantity = 1, UnitPrice = 1m });

        // Act
        var r = await _client.GetAsync("/api/orders/all");
        var list = await r.Content.ReadFromJsonAsync<List<OrderResponse>>();

        // Assert
        list.Should().HaveCount(2);
    }

    /// <summary>
    /// Nhiệm vụ: PUT cập nhật hợp lệ → 200, body phản ánh tên và TotalAmount mới.
    /// </summary>
    [Fact]
    public async Task IT10_Put_Update_Returns200()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        // Act
        var put = await _client.PutAsJsonAsync($"/api/orders/{id}", new UpdateOrderRequest { CustomerName = "Z", Email = "z@test.com", Quantity = 2, UnitPrice = 2m });
        var dto = await put.Content.ReadFromJsonAsync<OrderResponse>();

        // Assert
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        dto!.CustomerName.Should().Be("Z");
        dto.TotalAmount.Should().Be(4m);
    }

    /// <summary>
    /// Nhiệm vụ: PUT Id không tồn tại → 404.
    /// </summary>
    [Fact]
    public async Task IT11_Put_NotFound_Returns404()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var put = await _client.PutAsJsonAsync($"/api/orders/{Guid.NewGuid()}", new UpdateOrderRequest { CustomerName = "Z", Email = "z@test.com", Quantity = 1, UnitPrice = 1m });

        // Assert
        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Nhiệm vụ: PUT thiếu Email → 400.
    /// </summary>
    [Fact]
    public async Task IT12_Put_MissingEmail_Returns400()
    {
        // Arrange — Có đơn để gọi PUT, nhưng body thiếu email.
        await ResetOrdersTableAsync();
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        // Act
        var put = await _client.PutAsJsonAsync($"/api/orders/{id}", new { customerName = "X", quantity = 1, unitPrice = 1m });

        // Assert
        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Nhiệm vụ: DELETE đơn tồn tại → 204.
    /// </summary>
    [Fact]
    public async Task IT13_Delete_Exists_Returns204()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        // Act
        var del = await _client.DeleteAsync($"/api/orders/{id}");

        // Assert
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Nhiệm vụ: DELETE Id không tồn tại → 404.
    /// </summary>
    [Fact]
    public async Task IT14_Delete_NotFound_Returns404()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var del = await _client.DeleteAsync($"/api/orders/{Guid.NewGuid()}");

        // Assert
        del.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Nhiệm vụ: Kịch bản CRUD đầy đủ: tạo → đọc → sửa → xóa → đọc lại 404.
    /// </summary>
    [Fact]
    public async Task IT15_FullCrud_Create_Get_Update_Delete_Get404()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act — Chuỗi CRUD trên HTTP.
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 2m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;
        var get1 = await _client.GetAsync($"/api/orders/{id}");
        await _client.PutAsJsonAsync($"/api/orders/{id}", new UpdateOrderRequest { CustomerName = "B", Email = "b@test.com", Quantity = 2, UnitPrice = 2m });
        await _client.DeleteAsync($"/api/orders/{id}");
        var get404 = await _client.GetAsync($"/api/orders/{id}");

        // Assert
        get1.StatusCode.Should().Be(HttpStatusCode.OK);
        get404.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Nhiệm vụ: Chỉ 1 đơn nhưng hỏi trang 2 → Items rỗng, TotalCount vẫn 1.
    /// </summary>
    [Fact]
    public async Task IT16_GetPaged_Page2_EmptyWhenOnlyOneRow()
    {
        // Arrange
        await ResetOrdersTableAsync();
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });

        // Act
        var r = await _client.GetAsync("/api/orders?page=2&pageSize=10");
        var page = await r.Content.ReadFromJsonAsync<PagedOrdersResponse>();

        // Assert
        page!.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(1);
    }

    /// <summary>
    /// Nhiệm vụ: pageSize &gt; 100 vi phạm Range → 400.
    /// </summary>
    [Fact]
    public async Task IT17_GetPaged_InvalidPageSize_Returns400()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var r = await _client.GetAsync("/api/orders?page=1&pageSize=101");

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Nhiệm vụ: page = 0 vi phạm Range → 400.
    /// </summary>
    [Fact]
    public async Task IT18_GetPaged_InvalidPage_Returns400()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var r = await _client.GetAsync("/api/orders?page=0&pageSize=10");

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Nhiệm vụ: TotalAmount lưu trong DB khớp Quantity × UnitPrice sau POST.
    /// </summary>
    [Fact]
    public async Task IT19_Post_TotalAmount_PersistedInDb()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 3, UnitPrice = 4m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        // Assert — Đọc DB trực tiếp.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == id);
        row.TotalAmount.Should().Be(12m);
    }

    /// <summary>
    /// Nhiệm vụ: Sau PUT, các cột trong DB đúng như nghiệp vụ (quantity, email, total).
    /// </summary>
    [Fact]
    public async Task IT20_Put_UpdatesRowInDatabase()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        // Act
        await _client.PutAsJsonAsync($"/api/orders/{id}", new UpdateOrderRequest { CustomerName = "X", Email = "x@test.com", Quantity = 5, UnitPrice = 2m });

        // Assert
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == id);
        row.Quantity.Should().Be(5);
        row.TotalAmount.Should().Be(10m);
        row.Email.Should().Be("x@test.com");
    }

    /// <summary>
    /// Nhiệm vụ: Sau DELETE, bảng Orders không còn dòng.
    /// </summary>
    [Fact]
    public async Task IT21_Delete_RemovesRowFromDatabase()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        // Act
        await _client.DeleteAsync($"/api/orders/{id}");

        // Assert
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Nhiệm vụ: pageSize=1 chỉ trả 1 item nhưng TotalCount = 2 khi có 2 đơn.
    /// </summary>
    [Fact]
    public async Task IT22_GetPaged_PageSize1_ReturnsSingleItem()
    {
        // Arrange
        await ResetOrdersTableAsync();
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "B", Email = "b@test.com", Quantity = 1, UnitPrice = 1m });

        // Act
        var r = await _client.GetAsync("/api/orders?page=1&pageSize=1");
        var page = await r.Content.ReadFromJsonAsync<PagedOrdersResponse>();

        // Assert
        page!.Items.Should().HaveCount(1);
        page.TotalCount.Should().Be(2);
    }

    /// <summary>
    /// Nhiệm vụ: TotalPages làm tròn lên khi TotalCount không chia hết PageSize.
    /// </summary>
    [Fact]
    public async Task IT23_GetPaged_TotalPages_ReflectsCount()
    {
        // Arrange
        await ResetOrdersTableAsync();
        for (var i = 0; i < 5; i++)
            await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = $"U{i}", Email = $"u{i}@test.com", Quantity = 1, UnitPrice = 1m });

        // Act
        var r = await _client.GetAsync("/api/orders?page=1&pageSize=2");
        var page = await r.Content.ReadFromJsonAsync<PagedOrdersResponse>();

        // Assert
        page!.TotalCount.Should().Be(5);
        page.TotalPages.Should().Be(3);
    }

    /// <summary>
    /// Nhiệm vụ: Response JSON sau POST có email lowercase.
    /// </summary>
    [Fact]
    public async Task IT24_Post_ResponseJson_HasLowerEmail()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "MixCase@Test.com", Quantity = 1, UnitPrice = 1m });
        var dto = await post.Content.ReadFromJsonAsync<OrderResponse>();

        // Assert
        dto!.Email.Should().Be("mixcase@test.com");
    }

    /// <summary>
    /// Nhiệm vụ: GET sau POST trả cùng TotalAmount (đọc lại nhất quán).
    /// </summary>
    [Fact]
    public async Task IT25_GetById_ResponseMatchesPostBodyTotals()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 7, UnitPrice = 3m });
        var created = await post.Content.ReadFromJsonAsync<OrderResponse>();
        var get = await _client.GetAsync($"/api/orders/{created!.Id}");
        var got = await get.Content.ReadFromJsonAsync<OrderResponse>();

        // Assert
        got!.TotalAmount.Should().Be(21m);
    }

    /// <summary>
    /// Nhiệm vụ: CreatedAt không đổi sau PUT (chỉ cập nhật field nghiệp vụ).
    /// </summary>
    [Fact]
    public async Task IT26_Put_PreservesCreatedAt_OrderingStillWorks()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        // Act — Đọc CreatedAt trước PUT, sau PUT đọc lại.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var before = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == id);
            await _client.PutAsJsonAsync($"/api/orders/{id}", new UpdateOrderRequest { CustomerName = "B", Email = "b@test.com", Quantity = 1, UnitPrice = 1m });
            var after = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == id);

            // Assert
            after.CreatedAt.Should().Be(before.CreatedAt);
        }
    }

    /// <summary>
    /// Nhiệm vụ: GET /all sắp mới nhất trước (đơn tạo sau nằm đầu list) — có thể cần delay nhỏ để phân biệt thời gian.
    /// </summary>
    [Fact]
    public async Task IT27_GetAll_OrderedByCreatedAtDesc_LastCreatedFirst()
    {
        // Arrange
        await ResetOrdersTableAsync();
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "First", Email = "f@test.com", Quantity = 1, UnitPrice = 1m });
        await Task.Delay(20);
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "Second", Email = "s@test.com", Quantity = 1, UnitPrice = 1m });

        // Act
        var r = await _client.GetAsync("/api/orders/all");
        var list = await r.Content.ReadFromJsonAsync<List<OrderResponse>>();

        // Assert
        list![0].CustomerName.Should().Be("Second");
    }

    /// <summary>
    /// Nhiệm vụ: Trang phân trang đầu tiên đặt đơn mới nhất lên đầu.
    /// </summary>
    [Fact]
    public async Task IT28_GetPaged_OrderMatchesDescCreatedAt()
    {
        // Arrange
        await ResetOrdersTableAsync();
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "Old", Email = "o@test.com", Quantity = 1, UnitPrice = 1m });
        await Task.Delay(20);
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "New", Email = "n@test.com", Quantity = 1, UnitPrice = 1m });

        // Act
        var page = await (await _client.GetAsync("/api/orders?page=1&pageSize=10")).Content.ReadFromJsonAsync<PagedOrdersResponse>();

        // Assert
        page!.Items[0].CustomerName.Should().Be("New");
    }

    /// <summary>
    /// Nhiệm vụ: Thiếu CustomerName → 400.
    /// </summary>
    [Fact]
    public async Task IT29_Post_MissingCustomerName_Returns400()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var r = await _client.PostAsJsonAsync("/api/orders", new { email = "a@test.com", quantity = 1, unitPrice = 1m });

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Nhiệm vụ: Sau khi xóa hết đơn, GET phân trang báo TotalCount = 0.
    /// </summary>
    [Fact]
    public async Task IT30_Delete_ThenGetPaged_ShowsZeroTotal()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        // Act
        await _client.DeleteAsync($"/api/orders/{id}");
        var page = await (await _client.GetAsync("/api/orders")).Content.ReadFromJsonAsync<PagedOrdersResponse>();

        // Assert
        page!.TotalCount.Should().Be(0);
    }

    /// <summary>
    /// Nhiệm vụ: pageSize=100 (max) vẫn được chấp nhận → 200.
    /// </summary>
    [Fact]
    public async Task IT31_GetPaged_MaxPageSize_100_Accepted()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var r = await _client.GetAsync("/api/orders?page=1&pageSize=100");

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Nhiệm vụ: Đơn vừa tạo xuất hiện trong GET /all.
    /// </summary>
    [Fact]
    public async Task IT32_Create_Then_ListAll_ContainsId()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;
        var all = await (await _client.GetAsync("/api/orders/all")).Content.ReadFromJsonAsync<List<OrderResponse>>();

        // Assert
        all!.Any(o => o.Id == id).Should().BeTrue();
    }

    /// <summary>
    /// Nhiệm vụ: PUT với Quantity = 0 → 400.
    /// </summary>
    [Fact]
    public async Task IT33_Put_InvalidQuantity_Returns400()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        // Act
        var put = await _client.PutAsJsonAsync($"/api/orders/{id}", new UpdateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 0, UnitPrice = 1m });

        // Assert
        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Nhiệm vụ: Nhiều lần GET cùng Id trả CreatedAt giống nhau (idempotent đọc).
    /// </summary>
    [Fact]
    public async Task IT34_CreatedAt_Stable_AfterMultipleReads()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;
        var g1 = await (await _client.GetAsync($"/api/orders/{id}")).Content.ReadFromJsonAsync<OrderResponse>();
        var g2 = await (await _client.GetAsync($"/api/orders/{id}")).Content.ReadFromJsonAsync<OrderResponse>();

        // Assert
        g1!.CreatedAt.Should().Be(g2!.CreatedAt);
    }

    /// <summary>
    /// Nhiệm vụ: Trang cuối có thể ít phần tử hơn pageSize (ví dụ 5 đơn, size 2 → trang 3 còn 1).
    /// </summary>
    [Fact]
    public async Task IT35_GetPaged_LastPage_MayHaveFewerItems()
    {
        // Arrange
        await ResetOrdersTableAsync();
        for (var i = 0; i < 5; i++)
            await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = $"U{i}", Email = $"u{i}@t.com", Quantity = 1, UnitPrice = 1m });

        // Act
        var page3 = await (await _client.GetAsync("/api/orders?page=3&pageSize=2")).Content.ReadFromJsonAsync<PagedOrdersResponse>();

        // Assert
        page3!.Items.Should().HaveCount(1);
    }

    /// <summary>
    /// Nhiệm vụ: Email không đúng định dạng → 400.
    /// </summary>
    [Fact]
    public async Task IT36_Post_InvalidEmailFormat_Returns400()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var r = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "not-an-email", Quantity = 1, UnitPrice = 1m });

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Nhiệm vụ: Hai POST gần như đồng thời đều persist (không mất dữ liệu).
    /// </summary>
    [Fact]
    public async Task IT37_ConcurrentStyle_TwoPosts_BothPersist()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var t1 = _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        var t2 = _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "B", Email = "b@test.com", Quantity = 1, UnitPrice = 1m });
        await Task.WhenAll(t1, t2);

        // Assert
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Orders.CountAsync()).Should().Be(2);
    }

    /// <summary>
    /// Nhiệm vụ: DB rỗng thì GET /all trả mảng rỗng.
    /// </summary>
    [Fact]
    public async Task IT38_GetAll_EmptyDb_ReturnsEmptyArray()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var r = await _client.GetAsync("/api/orders/all");
        var list = await r.Content.ReadFromJsonAsync<List<OrderResponse>>();

        // Assert
        list.Should().BeEmpty();
    }

    /// <summary>
    /// Nhiệm vụ: DELETE lặp lại Id không tồn tại luôn 404 (không thành idempotent 204).
    /// </summary>
    [Fact]
    public async Task IT39_Delete_Idempotent_NotApplicable_Returns404_EachTime()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var id = Guid.NewGuid();

        // Act
        var d1 = await _client.DeleteAsync($"/api/orders/{id}");
        var d2 = await _client.DeleteAsync($"/api/orders/{id}");

        // Assert
        d1.StatusCode.Should().Be(HttpStatusCode.NotFound);
        d2.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Nhiệm vụ: Dữ liệu PUT response và GET sau đó khớp (total, email).
    /// </summary>
    [Fact]
    public async Task IT40_Put_ResponseAndGet_ById_Agree()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;
        var put = await _client.PutAsJsonAsync($"/api/orders/{id}", new UpdateOrderRequest { CustomerName = "ZZ", Email = "zz@test.com", Quantity = 3, UnitPrice = 3m });
        var fromPut = await put.Content.ReadFromJsonAsync<OrderResponse>();
        var fromGet = await (await _client.GetAsync($"/api/orders/{id}")).Content.ReadFromJsonAsync<OrderResponse>();

        // Assert
        fromPut!.TotalAmount.Should().Be(fromGet!.TotalAmount);
        fromPut.Email.Should().Be(fromGet.Email);
    }
}
