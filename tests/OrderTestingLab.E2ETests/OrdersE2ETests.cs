using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderTestingLab.Dtos;
using OrderTestingLab.Persistence;

namespace OrderTestingLab.E2ETests;

/// <summary>
/// <para><b>End-to-End (E2E)</b> trong phạm vi API: kiểm tra <b>luồng nghiệp vụ dài</b> qua nhiều request HTTP liên tiếp,
/// xuyên suốt ứng dụng đã deploy trong host test (middleware, routing, validation, controller, service, EF Core).</para>
/// <para>Khác <c>IntegrationTests</c>: integration dùng SQLite <b>:memory:</b>; E2E ở đây dùng SQLite <b>file trên đĩa</b> (<see cref="E2EWebApplicationFactory"/>)
/// để gần với vận hành thật (ghi/đọc file .db).</para>
/// <para>E2E với trình duyệt (Playwright/Selenium) là lớp khác — có thể bổ sung sau nếu có UI.</para>
/// </summary>
/// <remarks>
/// <b>F.I.R.S.T</b>: Fast (so với UI); Independent (reset bảng mỗi test); Repeatable; Self-validating (Assert); Timely (bảo vệ luồng người dùng).
/// Mỗi test: <b>Arrange – Act – Assert</b> + summary nhiệm vụ.
/// </remarks>
public class OrdersE2ETests : IClassFixture<E2EWebApplicationFactory>
{
    private readonly E2EWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OrdersE2ETests(E2EWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>Arrange — Dọn dữ liệu để mỗi kịch bản E2E độc lập (F.I.R.S.T: Independent).</summary>
    private async Task ResetOrdersTableAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Orders");
    }

    /// <summary>
    /// Nhiệm vụ: Kịch bản đủ bước — tạo → đọc theo Id → danh sách phân trang → cập nhật → xóa → xác nhận không còn tồn tại.
    /// </summary>
    [Fact]
    public async Task E2E01_Journey_Create_GetPaged_Update_Delete_Get404()
    {
        // Arrange — DB sạch.
        await ResetOrdersTableAsync();

        // Act — Chuỗi thao tác như người dùng / client thật.
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "  Alice  ",
            Email = "Alice@Shop.com",
            Quantity = 2,
            UnitPrice = 15m
        });
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await post.Content.ReadFromJsonAsync<OrderResponse>();

        var getOne = await _client.GetAsync($"/api/orders/{created!.Id}");
        var paged = await _client.GetAsync("/api/orders?page=1&pageSize=10");
        await _client.PutAsJsonAsync($"/api/orders/{created.Id}", new UpdateOrderRequest
        {
            CustomerName = "Alice",
            Email = "alice@shop.com",
            Quantity = 1,
            UnitPrice = 20m
        });
        await _client.DeleteAsync($"/api/orders/{created.Id}");
        var getAfterDelete = await _client.GetAsync($"/api/orders/{created.Id}");

        // Assert — Trạng thái cuối và dữ liệu đã chuẩn hóa.
        getOne.StatusCode.Should().Be(HttpStatusCode.OK);
        paged.StatusCode.Should().Be(HttpStatusCode.OK);
        getAfterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        created.Email.Should().Be("alice@shop.com");
    }

    /// <summary>
    /// Nhiệm vụ: Sau khi tạo nhiều đơn, GET /all phải phản ánh đủ số lượng (hành trình “kho đơn hàng”).
    /// </summary>
    [Fact]
    public async Task E2E02_Journey_MultipleCreates_ThenGetAll_CountMatches()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a1@test.com", Quantity = 1, UnitPrice = 1m });
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "B", Email = "b1@test.com", Quantity = 1, UnitPrice = 1m });
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "C", Email = "c1@test.com", Quantity = 1, UnitPrice = 1m });
        var all = await _client.GetAsync("/api/orders/all");

        // Assert
        all.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await all.Content.ReadFromJsonAsync<List<OrderResponse>>();
        list.Should().HaveCount(3);
    }

    /// <summary>
    /// Nhiệm vụ: Điều hướng phân trang (trang 1 và trang 2) khi có đủ bản ghi — E2E trải nghiệm người xem danh sách.
    /// </summary>
    [Fact]
    public async Task E2E03_Journey_Pagination_Page1AndPage2_ConsistentTotal()
    {
        // Arrange
        await ResetOrdersTableAsync();
        for (var i = 0; i < 5; i++)
            await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = $"U{i}", Email = $"u{i}@e2e.com", Quantity = 1, UnitPrice = 1m });

        // Act
        var p1 = await _client.GetAsync("/api/orders?page=1&pageSize=2");
        var p2 = await _client.GetAsync("/api/orders?page=2&pageSize=2");

        // Assert
        var page1 = await p1.Content.ReadFromJsonAsync<PagedOrdersResponse>();
        var page2 = await p2.Content.ReadFromJsonAsync<PagedOrdersResponse>();
        page1!.TotalCount.Should().Be(5);
        page2!.TotalCount.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
    }

    /// <summary>
    /// Nhiệm vụ: Request lỗi rồi request đúng — luồng sửa lỗi nhập liệu (validation) vẫn kết thúc thành công.
    /// </summary>
    [Fact]
    public async Task E2E04_Journey_InvalidThenValid_CreateSucceeds()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var bad = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "X", Email = "x@test.com", Quantity = 0, UnitPrice = 1m });
        var ok = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "X", Email = "x@test.com", Quantity = 1, UnitPrice = 1m });

        // Assert
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ok.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Nhiệm vụ: Xác minh dữ liệu nằm thật trên file SQLite (đọc DbContext) sau khi POST — E2E “đáy” persistence.
    /// </summary>
    [Fact]
    public async Task E2E05_Journey_Post_ThenVerifyRowInFileDatabase()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "Db", Email = "db@test.com", Quantity = 4, UnitPrice = 2.5m });
        var body = await post.Content.ReadFromJsonAsync<OrderResponse>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == body!.Id);

        // Assert
        row.TotalAmount.Should().Be(10m);
        row.Email.Should().Be("db@test.com");
    }

    /// <summary>
    /// Nhiệm vụ: Hai khách khác email — GET /all chứa cả hai (kịch bản nhiều người dùng đơn giản).
    /// </summary>
    [Fact]
    public async Task E2E06_Journey_TwoCustomers_BothAppearInGetAll()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "Nam", Email = "nam@test.com", Quantity = 1, UnitPrice = 10m });
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "Lan", Email = "lan@test.com", Quantity = 2, UnitPrice = 5m });
        var all = await (await _client.GetAsync("/api/orders/all")).Content.ReadFromJsonAsync<List<OrderResponse>>();

        // Assert
        all!.Select(o => o.Email).Should().Contain(new[] { "nam@test.com", "lan@test.com" });
    }

    /// <summary>
    /// Nhiệm vụ: Sửa đơn rồi đọc lại theo Id — phải khớp (PUT → GET).
    /// </summary>
    [Fact]
    public async Task E2E07_Journey_Update_ThenGetById_ReflectsChanges()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "Old", Email = "old@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        // Act
        await _client.PutAsJsonAsync($"/api/orders/{id}", new UpdateOrderRequest { CustomerName = "New", Email = "new@test.com", Quantity = 3, UnitPrice = 2m });
        var get = await _client.GetAsync($"/api/orders/{id}");
        var dto = await get.Content.ReadFromJsonAsync<OrderResponse>();

        // Assert
        dto!.CustomerName.Should().Be("New");
        dto.TotalAmount.Should().Be(6m);
    }

    /// <summary>
    /// Nhiệm vụ: Xóa một đơn rồi xem phân trang — TotalCount giảm (luồng quản trị).
    /// </summary>
    [Fact]
    public async Task E2E08_Journey_DeleteOne_ThenPagedTotalDecrements()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var id1 = (await (await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m })).Content.ReadFromJsonAsync<OrderResponse>())!.Id;
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "B", Email = "b@test.com", Quantity = 1, UnitPrice = 1m });

        // Act
        await _client.DeleteAsync($"/api/orders/{id1}");
        var page = await (await _client.GetAsync("/api/orders?page=1&pageSize=10")).Content.ReadFromJsonAsync<PagedOrdersResponse>();

        // Assert
        page!.TotalCount.Should().Be(1);
    }

    /// <summary>
    /// Nhiệm vụ: Response PUT và GET sau đó phải thống nhất (E2E nhất quán giao diện API).
    /// </summary>
    [Fact]
    public async Task E2E09_Journey_PutResponse_Matches_SubsequentGet()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "A", Email = "a@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        // Act
        var put = await _client.PutAsJsonAsync($"/api/orders/{id}", new UpdateOrderRequest { CustomerName = "Z", Email = "z@test.com", Quantity = 2, UnitPrice = 3m });
        var fromPut = await put.Content.ReadFromJsonAsync<OrderResponse>();
        var fromGet = await (await _client.GetAsync($"/api/orders/{id}")).Content.ReadFromJsonAsync<OrderResponse>();

        // Assert
        fromPut!.Id.Should().Be(fromGet!.Id);
        fromPut.TotalAmount.Should().Be(fromGet.TotalAmount);
    }

    /// <summary>
    /// Nhiệm vụ: Tổng tiền trong JSON khớp Quantity×Giá trên cả POST body và GET (truy vết nghiệp vụ).
    /// </summary>
    [Fact]
    public async Task E2E10_Journey_TotalAmount_Consistent_OnPostAndGet()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "T", Email = "t@test.com", Quantity = 6, UnitPrice = 7.5m });
        var created = await post.Content.ReadFromJsonAsync<OrderResponse>();
        var get = await (await _client.GetAsync($"/api/orders/{created!.Id}")).Content.ReadFromJsonAsync<OrderResponse>();

        // Assert
        var expected = 45m;
        created!.TotalAmount.Should().Be(expected);
        get!.TotalAmount.Should().Be(expected);
    }

    /// <summary>
    /// Nhiệm vụ: CRUD tối giản — tạo, đọc, sửa, đọc lại, xóa (5 bước liên tiếp).
    /// </summary>
    [Fact]
    public async Task E2E11_Journey_MinimalCrud_FiveSteps()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act & Assert từng bước
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "M", Email = "m@test.com", Quantity = 1, UnitPrice = 100m });
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        (await _client.GetAsync($"/api/orders/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.PutAsJsonAsync($"/api/orders/{id}", new UpdateOrderRequest { CustomerName = "M2", Email = "m2@test.com", Quantity = 2, UnitPrice = 50m }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.GetAsync($"/api/orders/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.DeleteAsync($"/api/orders/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Nhiệm vụ: Đơn tạo sau cùng xuất hiện đầu danh sách phân trang (trải nghiệm “mới nhất trước”).
    /// </summary>
    [Fact]
    public async Task E2E12_Journey_NewestOrder_FirstOnPage1()
    {
        // Arrange
        await ResetOrdersTableAsync();
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "First", Email = "f@test.com", Quantity = 1, UnitPrice = 1m });
        await Task.Delay(25);
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "Second", Email = "s@test.com", Quantity = 1, UnitPrice = 1m });

        // Act
        var page = await (await _client.GetAsync("/api/orders?page=1&pageSize=5")).Content.ReadFromJsonAsync<PagedOrdersResponse>();

        // Assert
        page!.Items[0].CustomerName.Should().Be("Second");
    }

    /// <summary>
    /// Nhiệm vụ: Tạo 3 đơn, xóa đơn giữa, GET /all còn 2 và không chứa Id đã xóa.
    /// </summary>
    [Fact]
    public async Task E2E13_Journey_ThreeOrders_DeleteMiddle_RemainingTwo()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        var id1 = (await (await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "1", Email = "1@test.com", Quantity = 1, UnitPrice = 1m })).Content.ReadFromJsonAsync<OrderResponse>())!.Id;
        var id2 = (await (await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "2", Email = "2@test.com", Quantity = 1, UnitPrice = 1m })).Content.ReadFromJsonAsync<OrderResponse>())!.Id;
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "3", Email = "3@test.com", Quantity = 1, UnitPrice = 1m });
        await _client.DeleteAsync($"/api/orders/{id2}");
        var all = await (await _client.GetAsync("/api/orders/all")).Content.ReadFromJsonAsync<List<OrderResponse>>();

        // Assert
        all.Should().NotBeNull();
        var list = all!;
        list.Should().HaveCount(2);
        list.Any(o => o.Id == id2).Should().BeFalse();
        list.Any(o => o.Id == id1).Should().BeTrue();
    }

    /// <summary>
    /// Nhiệm vụ: Sau DELETE, đếm bản ghi trong DB = 0 (E2E xác nhận persistence).
    /// </summary>
    [Fact]
    public async Task E2E14_Journey_DeleteLastOrder_DatabaseEmpty()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var post = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "Only", Email = "only@test.com", Quantity = 1, UnitPrice = 1m });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        // Act
        await _client.DeleteAsync($"/api/orders/{id}");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Orders.CountAsync();

        // Assert
        count.Should().Be(0);
    }

    /// <summary>
    /// Nhiệm vụ: Hai request POST tuần tự (giả lập hai thao tác người dùng) rồi phân trang hiển thị đủ 2 — luồng “thêm nhiều đơn”.
    /// </summary>
    [Fact]
    public async Task E2E15_Journey_SequentialPosts_ThenPagedShowsBoth()
    {
        // Arrange
        await ResetOrdersTableAsync();

        // Act
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "P1", Email = "p1@test.com", Quantity = 1, UnitPrice = 1m });
        await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest { CustomerName = "P2", Email = "p2@test.com", Quantity = 1, UnitPrice = 1m });
        var page = await (await _client.GetAsync("/api/orders?page=1&pageSize=10")).Content.ReadFromJsonAsync<PagedOrdersResponse>();

        // Assert
        page!.TotalCount.Should().Be(2);
        page.Items.Should().HaveCount(2);
    }
}
