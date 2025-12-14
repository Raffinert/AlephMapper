using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.0")]
partial class EfCoreMapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetPersonComplex(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, PersonSummaryDto>> GetPersonComplexExpression() => 
        p => new PersonSummaryDto
        {
            Id = p.Id,
            Name = p.Name,
            Email = p.Email,
            Age = (p.BirthInfo != null
                ? (p.BirthInfo.Age) 
                : (int?)null),
            BirthPlace = (p.BirthInfo != null
                ? (p.BirthInfo.BirthPlace) 
                : (string)null) ?? "Unknown",
            BirthAddress = (p.BirthInfo != null
                ? (p.BirthInfo.Address) 
                : (string)null) ?? "Not specified",
            HasBirthInfo = p.BirthInfo != null,
            IsAdult = (p.BirthInfo != null
                ? (p.BirthInfo.Age) 
                : (int?)null) >= 18,
            AddressCount = p.Addresses.Count,
            OrderCount = p.Orders.Count,
            HasActiveAddress = p.Addresses.Any(a => a.IsActive),
            PersonCategory = p.BirthInfo == null
                ? "Unknown Age"
                : p.BirthInfo.Age < 18
                    ? "Minor"
                    : p.BirthInfo.Age < 65
                        ? "Adult"
                        : "Senior",
            Summary = p.BirthInfo != null
                ? p.Name + " (" + p.BirthInfo.Age + " years old) from " + (p.BirthInfo.BirthPlace ?? "Unknown") 
                : p.Name + " (unknown age) from Unknown"
        };

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetPersonName(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, string>> GetPersonNameExpression() => 
        person => person.Name;

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetPersonEmail(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, string>> GetPersonEmailExpression() => 
        person => person.Email;

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetPersonAge(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, int?>> GetPersonAgeExpression() => 
        person => (person.BirthInfo != null
            ? (person.BirthInfo.Age) 
            : (int?)null);

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="IsOlderThan30(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, bool>> IsOlderThan30Expression() => 
        person => (person.BirthInfo != null
            ? (person.BirthInfo.Age) 
            : (int?)null) > 30;

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetBirthPlace(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, string>> GetBirthPlaceExpression() => 
        person => (person.BirthInfo != null
            ? (person.BirthInfo.BirthPlace) 
            : (string)null) ?? "Unknown";

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetBirthAddress(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, string>> GetBirthAddressExpression() => 
        person => (person.BirthInfo != null
            ? (person.BirthInfo.Address) 
            : (string)null) ?? "Not specified";

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="HasBirthInfo(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, bool>> HasBirthInfoExpression() => 
        person => person.BirthInfo != null;

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="IsAdult(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, bool>> IsAdultExpression() => 
        person => (person.BirthInfo != null
            ? (person.BirthInfo.Age) 
            : (int?)null) >= 18;

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="BornInUkraine(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, bool>> BornInUkraineExpression() => 
        person => (person.BirthInfo != null
            ? (person.BirthInfo.Address) 
            : (string)null) != null && person.BirthInfo.Address.Contains("Ukraine");

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetAddressCount(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, int>> GetAddressCountExpression() => 
        person => person.Addresses.Count;

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetOrderCount(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, int>> GetOrderCountExpression() => 
        person => person.Orders.Count;

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="HasActiveAddress(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, bool>> HasActiveAddressExpression() => 
        person => person.Addresses.Any(a => a.IsActive);

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="HasCompletedOrders(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, bool>> HasCompletedOrdersExpression() => 
        person => person.Orders.Any(o => o.IsCompleted);

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetFirstActiveAddressCity(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, string>> GetFirstActiveAddressCityExpression() => 
        person => (person.Addresses.FirstOrDefault(a => a.IsActive) != null
            ? (person.Addresses.FirstOrDefault(a => a.IsActive).City) 
            : (string)null) ?? "No active address";

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetTotalOrderAmount(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, decimal>> GetTotalOrderAmountExpression() => 
        person => person.Orders.Where(o => o.IsCompleted).Sum(o => o.Amount);

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetLatestOrderNumber(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, string>> GetLatestOrderNumberExpression() => 
        person => (person.Orders.OrderByDescending(o => o.OrderDate).FirstOrDefault() != null
            ? (person.Orders.OrderByDescending(o => o.OrderDate).FirstOrDefault().OrderNumber) 
            : (string)null) ?? "No orders";

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetPersonSummary(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, string>> GetPersonSummaryExpression() => 
        person => person.BirthInfo != null
            ? person.Name + " (" + person.BirthInfo.Age + " years old) from " + (person.BirthInfo.BirthPlace ?? "Unknown") 
            : person.Name + " (unknown age) from Unknown";

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="LivesInSamePlaceAsBorn(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, bool>> LivesInSamePlaceAsBornExpression() => 
        person => (person.BirthInfo != null
            ? (person.BirthInfo.BirthPlace) 
            : (string)null) != null && person.Addresses.Any(a => a.IsActive && a.City == person.BirthInfo.BirthPlace);

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="GetPersonCategory(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, string>> GetPersonCategoryExpression() => 
        person => person.BirthInfo == null
            ? "Unknown Age"
            : person.BirthInfo.Age < 18
                ? "Minor"
                : person.BirthInfo.Age < 65
                    ? "Adult"
                    : "Senior";

    /// <summary>
    /// This is an auto-generated expression companion for <see cref="IsVipCustomer(Person)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are rewritten as explicit null checks for better compatibility.
    /// </para>
    /// </remarks>
    public static Expression<Func<Person, bool>> IsVipCustomerExpression() => 
        person => person.Orders.Where(o => o.IsCompleted).Sum(o => o.Amount) >= 1000m;
}
