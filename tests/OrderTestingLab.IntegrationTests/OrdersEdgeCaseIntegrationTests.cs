using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderTestingLab.Data;
using OrderTestingLab.Dtos;
using OrderTestingLab.Testing.Common;

namespace OrderTestingLab.IntegrationTests;

/// <summary>
/// Mục đích: Bổ sung kịch bản biên / lỗi nhập liệu chưa gom ở file integration chính (thiếu field, âm, whitespace).
/// Tầng: Integration — HTTP + SQLite in-memory + JWT.
/// F.I.R.S.T: Independent (reset bảng), Self-validating (status + nội dung lỗi khi cần).
/// </summary>
public class OrdersEdgeCaseIntegrationTests : IClassFixture<OrdersTestWebApplicationFactory>
{
    private readonly OrdersTestWebApplicationFactory _factory;
    private readonly HttpClient _userClient;

    public OrdersEdgeCaseIntegrationTests(OrdersTestWebApplicationFactory factory)
    {
        _factory = factory;
        _userClient = factory.CreateClientWithRoles("User");
    }

    private async Task ResetOrdersTableAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Orders");
    }

    /// <summary>
    /// Mục tiêu: Thiếu Quantity trong body → 400 và thông báo liên quan Quantity.
    /// 3A: Arrange DB sạch; Act POST thiếu quantity; Assert 400 + text.
    /// F.I.R.S.T: Timely (validation pipeline).
    /// </summary>
    [Fact]
    public async Task Post_WhenMissingQuantity_ShouldReturn400_WithQuantityInErrorPayload()
    {
        await ResetOrdersTableAsync();

        var response = await _userClient.PostAsJsonAsync("/api/orders", new
        {
            customerName = "X",
            email = "x@test.com",
            unitPrice = 1m
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("Quantity", "API nên báo lỗi field Quantity.");
    }

    /// <summary>
    /// Mục tiêu: Thiếu UnitPrice → 400.
    /// </summary>
    [Fact]
    public async Task Post_WhenMissingUnitPrice_ShouldReturn400()
    {
        await ResetOrdersTableAsync();

        var response = await _userClient.PostAsJsonAsync("/api/orders", new
        {
            customerName = "X",
            email = "x@test.com",
            quantity = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Mục tiêu: PUT với UnitPrice âm → 400 (Range trên DTO).
    /// </summary>
    [Fact]
    public async Task Put_WhenUnitPriceNegative_ShouldReturn400()
    {
        await ResetOrdersTableAsync();
        var post = await _userClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "A",
            Email = "a@test.com",
            Quantity = 1,
            UnitPrice = 1m
        });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        var put = await _userClient.PutAsJsonAsync($"/api/orders/{id}", new UpdateOrderRequest
        {
            CustomerName = "A",
            Email = "a@test.com",
            Quantity = 1,
            UnitPrice = -1m
        });

        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Mục tiêu: POST với Quantity âm → 400.
    /// </summary>
    [Fact]
    public async Task Post_WhenQuantityNegative_ShouldReturn400()
    {
        await ResetOrdersTableAsync();

        var response = await _userClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "A",
            Email = "a@test.com",
            Quantity = -1,
            UnitPrice = 1m
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Mục tiêu: Chỉ khoảng trắng cho CustomerName — model binding/validation trả 400 (không lưu chuỗi rỗng qua API).
    /// </summary>
    [Fact]
    public async Task Post_WhenCustomerNameOnlyWhitespace_ShouldReturn400()
    {
        await ResetOrdersTableAsync();

        var response = await _userClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "   ",
            Email = "onlyspace@test.com",
            Quantity = 1,
            UnitPrice = 1m
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Mục tiêu: Route GET với Id không phải GUID hợp lệ → 404 (routing), không 500.
    /// </summary>
    [Fact]
    public async Task GetById_WhenIdNotGuid_ShouldReturn404()
    {
        await ResetOrdersTableAsync();

        var response = await _userClient.GetAsync("/api/orders/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
