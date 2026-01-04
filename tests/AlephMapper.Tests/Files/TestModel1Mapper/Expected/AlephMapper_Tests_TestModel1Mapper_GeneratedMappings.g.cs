using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Linq.Expressions;


namespace AlephMapper.Tests;

[GeneratedCode("AlephMapper", "0.5.1")]
partial class TestModel1Mapper
{
    /// <summary>
    /// This is an auto-generated expression companion for <see cref="MapToTestModel1Dto(TestModel1)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null handling strategy: Null-conditional operators are ignored and treated as regular member access.
    /// </para>
    /// </remarks>
    public static Expression<Func<TestModel1, TestModel1Dto>> MapToTestModel1DtoExpression() => 
        source => new TestModel1Dto
        {
            Name = source.Name,
            SurName = source.SurName,
            Address = source.Address != null
                ? new Address1Dto
                {
                    Line1 = source.Address.Line1 == null
                        ? null 
                        : new AddressLineDto
                        {
                            Street = source.Address.Line1.Street,
                            HouseNumber = source.Address.Line1.HouseNumber
                        },
                    Line2 = source.Address.Line2 == null
                        ? null 
                        : new AddressLineDto
                        {
                            Street = source.Address.Line2.Street,
                            HouseNumber = source.Address.Line2.HouseNumber
                        }
                }
                : null
        };

    /// <summary>
    /// Updates an existing instance of <see cref="TestModel1Dto"/> with values from the source object.
    /// </summary>
    /// <param name="source">The source object to map values from. If null, no updates are performed.</param>
    /// <param name="dest">The destination object to update. If null, no updates are performed.</param>
    /// <returns>The updated destination object for method chaining, or the original destination if either parameter is null.</returns>
    public static TestModel1Dto MapToTestModel1Dto(TestModel1 source, TestModel1Dto dest)
    {
        if (source == null) return dest;
        if (dest == null)
            dest = new TestModel1Dto();
        dest.Name = source.Name;
        dest.SurName = source.SurName;
        if (source.Address != null)
        {
            if (dest.Address == null)
                dest.Address = new Address1Dto();
            if (source.Address.Line1== null)
            {
                dest.Address.Line1 = null;
            }
            else
            {
                if (dest.Address.Line1 == null)
                    dest.Address.Line1 = new AddressLineDto();
                dest.Address.Line1.Street = source.Address.Line1.Street;
                dest.Address.Line1.HouseNumber = source.Address.Line1.HouseNumber;
            }
            if (source.Address.Line2== null)
            {
                dest.Address.Line2 = null;
            }
            else
            {
                if (dest.Address.Line2 == null)
                    dest.Address.Line2 = new AddressLineDto();
                dest.Address.Line2.Street = source.Address.Line2.Street;
                dest.Address.Line2.HouseNumber = source.Address.Line2.HouseNumber;
            }
        }
        else
        {
            dest.Address = null;
        }
        return dest;
    }
}
