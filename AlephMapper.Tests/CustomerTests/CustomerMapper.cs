namespace AlephMapper.Tests.CustomerTests;

internal static partial class CustomerMapper
{
    [Updatable]
    public static CustomerDto MapToCustomerDto(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return new CustomerDto
        {
            Id = customer.Id,
            FullName = $"{customer.FirstName} {customer.LastName}",
            Email = customer.Email,
            Age = DateTime.Now.Year - customer.DateOfBirth.Year,
            HomeAddressFormatted = FormatAddress(customer.HomeAddress),
            BillingAddressFormatted = FormatAddress(customer.BillingAddress),
            TotalOrders = customer.Orders.Count,
            TotalSpent = customer.Orders.Sum(o => o.TotalAmount),
            CustomerTypeText = customer.CustomerType.ToString(),
            IsActive = customer.IsActive,
            MemberSince = new DateTime(2025, 10, 10).ToString("d"),
            LastLogin = customer.LastLoginDate?.ToString("d")
        };
    }

    private static string? FormatAddress(Address? address)
    {
        return address == null ? null : $"{address.Street}, {address.City}, {address.State}, {address.PostalCode}, {address.Country}";
    }
}