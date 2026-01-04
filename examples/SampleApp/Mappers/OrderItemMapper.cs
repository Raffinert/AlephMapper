using SampleApp.Entities;
using SampleApp.Models;

namespace SampleApp.Mappers;

public static class OrderItemMapper
{
    // Entity to DTO mapping with expression-bodied syntax
    public static OrderItemDto ToDto(OrderItem entity) => new()
    {
        ProductId = entity.ProductId,
        ProductName = entity.Name,
        Quantity = entity.Qty,
        UnitPrice = entity.Price,
        TotalPrice = entity.LineTotal
    };

    // DTO to entity mapping with expression-bodied syntax
    public static OrderItem ToEntity(OrderItemDto dto) => new()
    {
        ProductId = dto.ProductId,
        Name = dto.ProductName,
        Qty = dto.Quantity,
        Price = dto.UnitPrice,
        LineTotal = OrderItemMapper.CalculateLineTotal(dto.Quantity, dto.UnitPrice)
    };

    // Bulk mapping operations with expression-bodied syntax
    public static IEnumerable<OrderItemDto> ToDtoList(IEnumerable<OrderItem> entities) =>
        entities.Select(ToDto);

    public static IEnumerable<OrderItem> ToEntityList(IEnumerable<OrderItemDto> dtos) =>
        dtos.Select(ToEntity);

    // Calculation helpers with expression-bodied syntax
    public static decimal CalculateLineTotal(int quantity, decimal unitPrice) =>
        quantity * unitPrice;

    public static decimal CalculateDiscount(OrderItemDto item, decimal discountPercentage) =>
        item.TotalPrice * (discountPercentage / 100);

    public static decimal CalculateDiscountedPrice(OrderItemDto item, decimal discountPercentage) =>
        item.TotalPrice - CalculateDiscount(item, discountPercentage);

    // Validation with expression-bodied syntax
    public static bool IsValidItem(OrderItemDto item) =>
        item.ProductId > 0 &&
        !string.IsNullOrWhiteSpace(item.ProductName) &&
        item.Quantity > 0 &&
        item.UnitPrice > 0 &&
        item.TotalPrice > 0 &&
        Math.Abs(item.TotalPrice - (item.Quantity * item.UnitPrice)) < 0.01m;

    // Formatting helpers with expression-bodied syntax
    public static string FormatItemDescription(OrderItemDto item) =>
        $"{item.ProductName} (Qty: {item.Quantity}, Unit: {item.UnitPrice:C}, Total: {item.TotalPrice:C})";

    public static string GetItemSummary(OrderItemDto item) =>
        $"{item.Quantity}x {item.ProductName} @ {item.UnitPrice:C} each = {item.TotalPrice:C}";

    // Advanced filtering with expression-bodied syntax
    public static IEnumerable<OrderItemDto> GetHighValueItems(IEnumerable<OrderItemDto> items, decimal threshold) =>
        items.Where(item => item.TotalPrice >= threshold);

    public static IEnumerable<OrderItemDto> GetItemsByProduct(IEnumerable<OrderItemDto> items, int productId) =>
        items.Where(item => item.ProductId == productId);

    // Aggregation helpers with expression-bodied syntax
    public static decimal GetAverageItemValue(IEnumerable<OrderItemDto> items) =>
        items.Any() ? items.Average(item => item.TotalPrice) : 0;

    public static int GetTotalQuantity(IEnumerable<OrderItemDto> items) =>
        items.Sum(item => item.Quantity);
}