using AlephMapper;
using System.Collections.Generic;
using System.Linq;

namespace AlephMapper.Tests.MethodGroupToEntityList;

public class PersonDto
{
    public List<PhoneDto> PhoneNumbers { get; set; } = new();
}

public class PhoneDto
{
    public string Number { get; set; } = string.Empty;
}

public class Person
{
    public List<PhoneNumber> ContactNumbers { get; set; } = new();
}

public class PhoneNumber
{
    public string Number { get; set; } = string.Empty;
}

public static class PhoneMapper
{
    public static PhoneNumber ToEntity(PhoneDto dto) => new()
    {
        Number = dto.Number
    };
}

public static partial class PersonMapper
{
    [Expressive]
    public static Person ToEntity(PersonDto dto) => new()
    {
        ContactNumbers = dto.PhoneNumbers.Select(PhoneMapper.ToEntity).ToList()
    };
}
