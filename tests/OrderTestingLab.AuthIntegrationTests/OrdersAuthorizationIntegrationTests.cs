using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderTestingLab.Dtos;
using OrderTestingLab.Data;
using OrderTestingLab.Testing.Common;

namespace OrderTestingLab.AuthIntegrationTests;

/// <summary>
/// Mục đích: Kiểm tra JWT + phân quyền role (User/Admin) trên pipeline HTTP thật.
/// Tầng: Integration (WebApplicationFactory, không mock middleware auth).
/// F.I.R.S.T: Independent (factory + DB reset từng test), Self-validating (401/403/204 rõ ràng).
/// Vì sao tách file: tập trung kịch bản bảo mật, không trộn với test CRUD/validation thuần.
/// </summary>
public class OrdersAuthorizationIntegrationTests : IClassFixture<OrdersTestWebApplicationFactory>
{
    private readonly OrdersTestWebApplicationFactory _factory;

    public OrdersAuthorizationIntegrationTests(OrdersTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task ResetOrdersTableAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Orders");
    }

    /// <summary>
    /// Mục tiêu: Không gửi Bearer → pipeline trả 401 (anonymous bị chặn).
    /// 3A: Arrange factory; Act GET không header Authorization; Assert 401.
    /// F.I.R.S.T: Fast (một request), Timely (đúng hợp đồng OAuth/JWT).
    /// </summary>
    [Fact]
    public async Task GetPaged_WhenNoJwt_ShouldReturn401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Mục tiêu: User có JWT → được GET danh sách (200).
    /// 3A: Arrange client role User; Act GET; Assert 200.
    /// F.I.R.S.T: Self-validating (status code).
    /// </summary>
    [Fact]
    public async Task GetPaged_WhenUserRole_ShouldReturn200Ok()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var client = _factory.CreateClientWithRoles("User");

        // Act
        var response = await client.GetAsync("/api/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Mục tiêu: User tạo đơn thành công (201) — endpoint không yêu cầu Admin.
    /// 3A: Arrange User + body hợp lệ; Act POST; Assert 201.
    /// F.I.R.S.T: Independent (reset bảng).
    /// </summary>
    [Fact]
    public async Task Post_WhenUserRole_ShouldReturn201Created()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var client = _factory.CreateClientWithRoles("User");

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "AuthUser",
            Email = "authuser@test.com",
            Quantity = 1,
            UnitPrice = 1m
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Mục tiêu: User gọi DELETE (chỉ Admin) → 403 Forbidden.
    /// 3A: Arrange tạo đơn bằng User; Act User DELETE; Assert 403.
    /// F.I.R.S.T: Repeatable (kịch bản cố định).
    /// </summary>
    [Fact]
    public async Task Delete_WhenUserRole_ShouldReturn403Forbidden()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var user = _factory.CreateClientWithRoles("User");
        var post = await user.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "Del",
            Email = "del@test.com",
            Quantity = 1,
            UnitPrice = 1m
        });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

        // Act
        var del = await user.DeleteAsync($"/api/orders/{id}");

        // Assert
        del.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Mục tiêu: Admin gọi DELETE → 204 (được phép xóa).
    /// 3A: Arrange POST User (tạo bản ghi), Act Admin DELETE; Assert 204.
    /// F.I.R.S.T: Self-validating.
    /// </summary>
    [Fact]
    public async Task Delete_WhenAdminRole_ShouldReturn204NoContent()
    {
        // Arrange
        await ResetOrdersTableAsync();
        var user = _factory.CreateClientWithRoles("User");
        var post = await user.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "Adm",
            Email = "adm@test.com",
            Quantity = 1,
            UnitPrice = 1m
        });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;
        var admin = _factory.CreateClientWithRoles("Admin");

        // Act
        var del = await admin.DeleteAsync($"/api/orders/{id}");

        // Assert
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Mục tiêu: Bearer không phải JWT hợp lệ → 401.
    /// </summary>
    [Fact]
    public async Task GetPaged_WhenBearerMalformed_ShouldReturn401Unauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.valid.jwt.token");

        var response = await client.GetAsync("/api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Mục tiêu: POST không JWT → 401 (không tới validation body).
    /// </summary>
    [Fact]
    public async Task Post_WhenAnonymous_ShouldReturn401Unauthorized()
    {
        await ResetOrdersTableAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "A",
            Email = "a@test.com",
            Quantity = 1,
            UnitPrice = 1m
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Mục tiêu: PUT không JWT → 401.
    /// </summary>
    [Fact]
    public async Task Put_WhenAnonymous_ShouldReturn401Unauthorized()
    {
        await ResetOrdersTableAsync();
        var user = _factory.CreateClientWithRoles("User");
        var post = await user.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "A",
            Email = "a@test.com",
            Quantity = 1,
            UnitPrice = 1m
        });
        var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;
        var anonymous = _factory.CreateClient();

        var response = await anonymous.PutAsJsonAsync($"/api/orders/{id}", new UpdateOrderRequest
        {
            CustomerName = "B",
            Email = "b@test.com",
            Quantity = 1,
            UnitPrice = 1m
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Mục tiêu: Role Admin vẫn dùng được GET (cùng policy User,Admin).
    /// </summary>
    [Fact]
    public async Task GetAll_WhenAdminRole_ShouldReturn200Ok()
    {
        await ResetOrdersTableAsync();
        var admin = _factory.CreateClientWithRoles("Admin");

        var response = await admin.GetAsync("/api/orders/all");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
