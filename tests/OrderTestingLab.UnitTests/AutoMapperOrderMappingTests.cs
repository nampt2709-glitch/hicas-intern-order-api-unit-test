using AutoMapper;
using FluentAssertions;
using OrderTestingLab.Dtos;
using OrderTestingLab.Entities;
using OrderTestingLab.Mapping;

namespace OrderTestingLab.UnitTests;

/// <summary>
/// Mục đích: Kiểm tra cấu hình AutoMapper (Entity → DTO) không làm mất/sai dữ liệu.
/// Tầng: Unit (không HTTP, không DB).
/// F.I.R.S.T: Fast, Independent, Repeatable, Self-validating, Timely (bắt regress map).
/// Vì sao ở Unit: chỉ cần profile + entity giả lập, không cần pipeline ASP.NET.
/// </summary>
public class AutoMapperOrderMappingTests
{
    private static IMapper CreateSut()
    {
        var cfg = new MapperConfiguration(c => c.AddProfile<OrderMappingProfile>());
        cfg.AssertConfigurationIsValid();
        return cfg.CreateMapper();
    }

    /// <summary>
    /// Mục tiêu: AssertConfigurationIsValid không ném — toàn bộ map được khai báo hợp lệ.
    /// 3A: Arrange (profile), Act (AssertConfigurationIsValid), Assert (không exception).
    /// F.I.R.S.T: Self-validating (cấu hình tự kiểm).
    /// </summary>
    [Fact]
    public void OrderMappingProfile_WhenConfigurationBuilt_ShouldPassAssertConfigurationIsValid()
    {
        // Arrange
        var cfg = new MapperConfiguration(c => c.AddProfile<OrderMappingProfile>());

        // Act
        var act = () => cfg.AssertConfigurationIsValid();

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// Mục tiêu: Map Order → OrderResponse giữ Id, CustomerName, Email, Quantity, UnitPrice, TotalAmount, CreatedAt.
    /// 3A: Arrange entity đầy đủ; Act Map; Assert từng property.
    /// F.I.R.S.T: Fast, Repeatable (deterministic entity).
    /// </summary>
    [Fact]
    public void Map_WhenOrderHasAllFields_ShouldMapToOrderResponseWithoutLoss()
    {
        // Arrange
        var mapper = CreateSut();
        var id = Guid.NewGuid();
        var created = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var order = new Order
        {
            Id = id,
            CustomerName = "Test Customer",
            Email = "user@example.com",
            Quantity = 5,
            UnitPrice = 12.34m,
            TotalAmount = 61.70m,
            CreatedAt = created
        };

        // Act
        var dto = mapper.Map<OrderResponse>(order);

        // Assert
        dto.Id.Should().Be(id);
        dto.CustomerName.Should().Be("Test Customer");
        dto.Email.Should().Be("user@example.com");
        dto.Quantity.Should().Be(5);
        dto.UnitPrice.Should().Be(12.34m);
        dto.TotalAmount.Should().Be(61.70m);
        dto.CreatedAt.Should().Be(created);
    }
}
