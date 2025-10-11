namespace AlephMapper.Tests
{
    internal class TestModel1
    {
        public string Name { get; set; }
        public string SurName { get; set; }

        public Address1 Address { get; set; }
    }

    internal class Address1
    {
        public AddressLine Line1 { get; set; }
        public AddressLine? Line2 { get; set; }
    }

    internal class AddressLine
    {
        public string Street { get; set; }
        public string HouseNumber { get; set; }

    }

    internal class TestModel1Dto
    {
        public string Name { get; set; }
        public string SurName { get; set; }

        public Address1Dto Address { get; set; }
    }

    internal class Address1Dto
    {
        public AddressLineDto? Line1 { get; set; }
        public AddressLineDto? Line2 { get; set; }
    }

    internal class AddressLineDto
    {
        public string Street { get; set; }
        public string HouseNumber { get; set; }

    }

    [Expressive]
    internal static partial class TestModel1Mapper
    {
        [Updateable]
        public static TestModel1Dto MapToTestModel1Dto(TestModel1 source)
        => new TestModel1Dto
        {
            Name = source.Name,
            SurName = source.SurName,
            Address = source.Address != null ? Address1Mapper.MapToDto(source.Address) : null
        };
    }

    [Expressive]
    internal static partial class Address1Mapper
    {
        public static Address1Dto MapToDto(Address1 sourceAddress)
        => new Address1Dto
        {
            Line1 = AddressLineMapper.MapToDto(sourceAddress.Line1),
            Line2 = AddressLineMapper.MapToDto(sourceAddress.Line2)
        };
    }

    [Expressive]
    internal static partial class AddressLineMapper
    {
        public static AddressLineDto? MapToDto(AddressLine? sourceAddressLine1)
        => sourceAddressLine1 == null ? null : new AddressLineDto
        {
            Street = sourceAddressLine1.Street,
            HouseNumber = sourceAddressLine1.HouseNumber
        };
    }
}
