using AutoMapper;
using OrderTestingLab.Dtos;
using OrderTestingLab.Entities;

namespace OrderTestingLab.Mapping;

/// <summary>
/// Cấu hình AutoMapper: <see cref="Order"/> → <see cref="OrderResponse"/> (không mất field nghiệp vụ).
/// </summary>
public class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        CreateMap<Order, OrderResponse>();
    }
}
