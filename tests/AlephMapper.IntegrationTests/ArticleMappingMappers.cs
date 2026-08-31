namespace AlephMapper.IntegrationTests;

public static partial class ArticleOrderMapper
{
    [Projectable]
    public static ArticleOrderDto Map(ArticleOrder order) =>
        new(
            order.Id,
            order.Number,
            DisplayName(order.Customer),
            CalculateTotal(order));

    public static string DisplayName(ArticleCustomer customer) =>
        customer.FirstName + " " + customer.LastName;

    public static decimal CalculateTotal(ArticleOrder order) =>
        order.Lines.Sum(line => line.Price * line.Quantity);
}
