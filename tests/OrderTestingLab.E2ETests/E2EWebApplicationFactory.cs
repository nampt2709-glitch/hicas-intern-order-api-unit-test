using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderTestingLab.Data;
using OrderTestingLab.Testing.Common;

namespace OrderTestingLab.E2ETests;

/// <summary>
/// Host test cho End-to-End: pipeline HTTP thật, SQLite file trên đĩa, JWT cấu hình giống integration test.
/// </summary>
public class E2EWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _databaseFilePath;

    public E2EWebApplicationFactory()
    {
        var root = Path.Combine(Path.GetTempPath(), "OrderTestingLab.E2E");
        Directory.CreateDirectory(root);
        _databaseFilePath = Path.Combine(root, $"e2e_{Guid.NewGuid():N}.db");
    }

    /// <summary>Đường dẫn file DB (gỡ lỗi thủ công nếu cần).</summary>
    public string DatabaseFilePath => _databaseFilePath;

    /// <summary>HttpClient kèm JWT theo role (User/Admin).</summary>
    public HttpClient CreateClientWithRoles(params string[] roles)
    {
        var client = CreateClient();
        var token = JwtTestTokenFactory.CreateToken(roles);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("E2E");

        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(AppDbContext)).ToList();
            foreach (var d in descriptors)
                services.Remove(d);

            var connectionString = $"Data Source={_databaseFilePath}";
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                if (File.Exists(_databaseFilePath))
                    File.Delete(_databaseFilePath);
            }
            catch
            {
                // Bỏ qua nếu file đang bị khóa ngắn.
            }
        }

        base.Dispose(disposing);
    }
}
