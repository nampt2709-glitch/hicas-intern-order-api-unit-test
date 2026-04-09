using System.Net;
using System.Net.Http.Json;
using Docker.DotNet;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderTestingLab.Dtos;
using OrderTestingLab.Data;
using Testcontainers.MsSql;
using Xunit;

namespace OrderTestingLab.TestcontainersTests;

/// <summary>
/// Mục đích: Chạy SQL Server thật trong Docker (Testcontainers), schema qua EnsureCreated của API, gọi HTTP + đọc DB.
/// Tầng: Integration đặc biệt (container) — bổ sung “DB engine thật”, không thay SQLite in-memory.
/// F.I.R.S.T: Independent (container trong một test), Timely (lỗi chỉ SQL Server).
/// Lý do chọn SQL Server thay vì Redis: Order lưu quan hệ; Redis không thay EF Orders.
/// </summary>
public class OrdersSqlServerContainerTests
{
    private static bool IsDockerEngineAvailable()
    {
        try
        {
            using var cfg = new DockerClientConfiguration();
            using var client = cfg.CreateClient();
            client.System.PingAsync().GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunWithSqlServerAsync(Func<SqlServerOrdersWebApplicationFactory, Task> test)
    {
        Skip.IfNot(IsDockerEngineAvailable(), "Docker engine không sẵn sàng — bật Docker để chạy Testcontainers.");

        await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync();

        using var factory = new SqlServerOrdersWebApplicationFactory(container.GetConnectionString());
        await test(factory);
    }

    /// <summary>
    /// Mục tiêu: POST hợp lệ → dòng tồn tại trong SQL Server với TotalAmount đúng.
    /// </summary>
    [SkippableFact]
    public async Task Post_WhenUsingSqlServerContainer_ShouldPersistOrderWithCorrectTotals()
    {
        await RunWithSqlServerAsync(async factory =>
        {
            var client = factory.CreateClientWithRoles("User");
            var request = new CreateOrderRequest
            {
                CustomerName = "Container",
                Email = "container@test.com",
                Quantity = 3,
                UnitPrice = 4m
            };

            var response = await client.PostAsJsonAsync("/api/orders", request);
            var body = await response.Content.ReadFromJsonAsync<OrderResponse>();

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            body.Should().NotBeNull();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == body!.Id);
            row.TotalAmount.Should().Be(12m);
            row.Email.Should().Be("container@test.com");
        });
    }

    /// <summary>
    /// Mục tiêu: GET theo Id sau POST trên SQL Server — pipeline + đọc DB thống nhất.
    /// </summary>
    [SkippableFact]
    public async Task GetById_WhenUsingSqlServerContainer_ShouldMatchPostedOrder()
    {
        await RunWithSqlServerAsync(async factory =>
        {
            var client = factory.CreateClientWithRoles("User");
            var post = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
            {
                CustomerName = "G",
                Email = "getbyid@test.com",
                Quantity = 2,
                UnitPrice = 5m
            });
            var created = await post.Content.ReadFromJsonAsync<OrderResponse>();

            var get = await client.GetAsync($"/api/orders/{created!.Id}");
            var dto = await get.Content.ReadFromJsonAsync<OrderResponse>();

            get.StatusCode.Should().Be(HttpStatusCode.OK);
            dto!.TotalAmount.Should().Be(10m);
            dto.Email.Should().Be("getbyid@test.com");
        });
    }

    /// <summary>
    /// Mục tiêu: Admin DELETE trên SQL Server — bản ghi biến mất khỏi DB.
    /// </summary>
    [SkippableFact]
    public async Task Delete_WhenAdminOnSqlServerContainer_ShouldRemoveRowFromDatabase()
    {
        await RunWithSqlServerAsync(async factory =>
        {
            var user = factory.CreateClientWithRoles("User");
            var post = await user.PostAsJsonAsync("/api/orders", new CreateOrderRequest
            {
                CustomerName = "Del",
                Email = "delcontainer@test.com",
                Quantity = 1,
                UnitPrice = 1m
            });
            var id = (await post.Content.ReadFromJsonAsync<OrderResponse>())!.Id;

            var admin = factory.CreateClientWithRoles("Admin");
            var del = await admin.DeleteAsync($"/api/orders/{id}");

            del.StatusCode.Should().Be(HttpStatusCode.NoContent);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Orders.AsNoTracking().CountAsync(o => o.Id == id)).Should().Be(0);
        });
    }
}
