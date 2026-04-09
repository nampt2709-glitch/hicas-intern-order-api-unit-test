using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderTestingLab.Persistence;

namespace OrderTestingLab.E2ETests;

/// <summary>
/// Host test cho End-to-End: cùng pipeline HTTP thật như integration, nhưng dùng <b>SQLite file trên đĩa</b> (thư mục temp)
/// để mô phỏng gần hơn môi trường chạy API thật (I/O file, không phải :memory:).
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

    /// <summary>Đường dẫn file DB (để gỡ lỗi hoặc kiểm tra E2E thủ công nếu cần).</summary>
    public string DatabaseFilePath => _databaseFilePath;

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
                // Bỏ qua nếu file đang bị khóa ngắn — F.I.R.S.T: không làm fail test vì dọn rác.
            }
        }

        base.Dispose(disposing);
    }
}
