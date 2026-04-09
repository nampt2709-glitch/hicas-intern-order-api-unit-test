using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderTestingLab.Data;
using OrderTestingLab.Testing.Common;

namespace OrderTestingLab.TestcontainersTests;

/// <summary>
/// WebApplicationFactory trỏ DbContext tới SQL Server (connection từ Testcontainers).
/// </summary>
public sealed class SqlServerOrdersWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public SqlServerOrdersWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public HttpClient CreateClientWithRoles(params string[] roles)
    {
        var client = CreateClient();
        var token = JwtTestTokenFactory.CreateToken(roles);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(AppDbContext)).ToList();
            foreach (var d in descriptors)
                services.Remove(d);

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(_connectionString));
        });
    }
}
