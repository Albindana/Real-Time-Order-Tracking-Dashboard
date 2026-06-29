using Riok.Mapperly.Abstractions;
using RealTimeDashboard.Application.DTOs;
using RealTimeDashboard.Domain.Entities;

namespace RealTimeDashboard.Application.Mapping;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class OrderMapper
{
    public partial OrderSummaryDto ToSummary(Order order);

    [MapProperty(nameof(Order.Items), nameof(OrderDetailDto.Items))]
    public partial OrderDetailDto ToDetail(Order order);

    public partial OrderItemDto ToItemDto(OrderItem item);
}

[Mapper]
public partial class ProductMapper
{
    public partial ProductDto ToDto(Product product);
}
