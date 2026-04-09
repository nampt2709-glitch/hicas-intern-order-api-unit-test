using Microsoft.EntityFrameworkCore;
using OrderTestingLab.Interfaces;
using OrderTestingLab.Persistence;
using OrderTestingLab.Repositories;
using OrderTestingLab.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SQLite file khi chạy API thật (file OrderTestingLab.db tại thư mục chạy ứng dụng).
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=OrderTestingLab.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

// Tạo database/schema khi khởi động (phù hợp lab; production nên dùng migrations).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.Run();

/// <summary>
/// Cho phép WebApplicationFactory tham chiếu entry point.
/// </summary>
public partial class Program { }
