using ConsoleApp1.Entities;
using ConsoleApp1.Models;

namespace ConsoleApp1.Mappers;

public static class OrderMapper
{
    // Entity to DTO mapping with expression-bodied syntax
    public static OrderDto ToDto(Order entity) => new()
    {
        OrderId = entity.Id,
        OrderDate = entity.CreatedDate,
        TotalAmount = entity.Total,
        Status = GetOrderStatusString(entity.OrderStatus),
        Items = entity.OrderItems.Select(OrderItemMapper.ToDto).ToList()
    };

    // DTO to entity mapping with expression-bodied syntax
    public static Order ToEntity(OrderDto dto) => new()
    {
        Id = dto.OrderId,
        CreatedDate = dto.OrderDate,
        Total = dto.TotalAmount,
        OrderStatus = ParseOrderStatus(dto.Status),
        OrderItems = dto.Items.Select(OrderItemMapper.ToEntity).ToList()
    };

    // Bulk mapping operations with expression-bodied syntax
    public static IEnumerable<OrderDto> ToDtoList(IEnumerable<Order> entities) =>
        entities.Select(ToDto);

    public static IEnumerable<Order> ToEntityList(IEnumerable<OrderDto> dtos) =>
        dtos.Select(ToEntity);

    // Order status conversion with expression-bodied syntax
    public static string GetOrderStatusString(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Pending",
        OrderStatus.Processing => "Processing",
        OrderStatus.Shipped => "Shipped",
        OrderStatus.Delivered => "Delivered",
        OrderStatus.Cancelled => "Cancelled",
        _ => "Unknown"
    };

    public static OrderStatus ParseOrderStatus(string statusString) => statusString.ToLower() switch
    {
        "pending" => OrderStatus.Pending,
        "processing" => OrderStatus.Processing,
        "shipped" => OrderStatus.Shipped,
        "delivered" => OrderStatus.Delivered,
        "cancelled" => OrderStatus.Cancelled,
        _ => OrderStatus.Pending
    };

    // Advanced filtering and mapping with expression-bodied syntax
    public static IEnumerable<OrderDto> GetRecentOrders(IEnumerable<Order> orders, int days) =>
        orders.Where(o => o.CreatedDate >= DateTime.Now.AddDays(-days))
              .Select(ToDto);

    public static IEnumerable<OrderDto> GetOrdersByStatus(IEnumerable<Order> orders, OrderStatus status) =>
        orders.Where(o => o.OrderStatus == status)
              .Select(ToDto);

    // Calculation helpers with expression-bodied syntax
    public static decimal CalculateTotalValue(OrderDto order) =>
        order.Items.Sum(item => item.TotalPrice);

    public static int GetTotalItemCount(OrderDto order) =>
        order.Items.Sum(item => item.Quantity);

    // Validation with expression-bodied syntax
    public static bool IsValidOrder(OrderDto order) =>
        order.OrderId > 0 &&
        order.OrderDate != default &&
        order.TotalAmount > 0 &&
        !string.IsNullOrWhiteSpace(order.Status) &&
        order.Items.Any() &&
        order.Items.All(OrderItemMapper.IsValidItem);
}