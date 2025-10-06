﻿[![Stand With Ukraine](https://raw.githubusercontent.com/vshymanskyy/StandWithUkraine/main/banner2-direct.svg)](https://stand-with-ukraine.pp.ua)

## Terms of use<sup>[?](https://github.com/Tyrrrz/.github/blob/master/docs/why-so-political.md)</sup>

By using this project or its source code, for any purpose and in any shape or form, you grant your **implicit agreement** to all the following statements:

- You **condemn Russia and its military aggression against Ukraine**
- You **recognize that Russia is an occupant that unlawfully invaded a sovereign state**
- You **support Ukraine's territorial integrity, including its claims over temporarily occupied territories of Crimea and Donbas**
- You **reject false narratives perpetuated by Russian state propaganda**

To learn more about the war and how you can help, [click here](https://stand-with-ukraine.pp.ua). Glory to Ukraine! 🇺🇦

# AlephMapper

[![NuGet](https://img.shields.io/nuget/v/AlephMapper.svg)](https://www.nuget.org/packages/AlephMapper)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Build Status](https://img.shields.io/github/actions/workflow/status/Raffinert/AlephMapper/build.yml?branch=main)](https://github.com/Raffinert/AlephMapper/actions)

AlephMapper is a **Source Generator** that automatically creates projectable companion methods from your mapping logic. It enables you to write mapping methods once and use them both for in-memory objects and as expression trees for database queries (Entity Framework Core projections).

## 🚀 Features

- **Expression Tree Generation** - Automatically converts method bodies to expression trees
- **Null-Conditional Operator Support** - Configurable handling of `?.` operators
- - **Updateable Methods** - Generate update methods that modify existing instances (now is in a very early development stage)

## 📦 Installation

```bash
dotnet add package AlephMapper
```

## 🏃‍♂️ Quick Start

### 1. Mark your mapping class or method with `[Expressive]. Please ensure that your class is static and partial.`

```csharp
[Expressive]
public static partial class PersonMapper
{
    public static PersonDto MapToPerson(Employee employee) => new PersonDto
    {
        Id = employee.EmployeeId,
        FullName = GetFullName(employee),
        Email = employee.ContactInfo.Email,
        Age = CalculateAge(employee.BirthDate),
        Department = employee.Department?.Name ?? "Unknown"
    };

    public static string GetFullName(Employee employee) => 
        $"{employee.FirstName} {employee.LastName}";

    public static int CalculateAge(DateTime birthDate) => 
        DateTime.Now.Year - birthDate.Year;
}
```

### 2. Use generated expression methods in Entity Framework queries

```csharp
// The source generator creates PersonMapper.MapToPersonExpression() automatically
var personDtos = await dbContext.Employees
    .Select(PersonMapper.MapToPersonExpression())
    .ToListAsync();
```

### 3. Use the original methods for in-memory operations

```csharp
var employee = GetEmployee();
var personDto = PersonMapper.MapToPerson(employee);
var fullName = PersonMapper.GetFullName(employee);
```

## 🔧 Advanced Features

### Null-Conditional Operator Handling

AlephMapper provides flexible handling of null-conditional operators (`?.`):

```csharp
[Expressive(NullConditionalRewrite = NullConditionalRewrite.Rewrite)]
public static partial class SafeMapper
{
    // This method uses null-conditional operators
    public static PersonSummary GetSummary(Person person) => new PersonSummary
    {
        Name = person.Name,
        Age = person.BirthInfo?.Age,
        City = person.BirthInfo?.Address?.City,
        HasAddress = person.BirthInfo?.Address != null
    };
}
```

**Rewrite Options:**
- `None` - Don't allow null-conditional operators (throws compile error)
- `Ignore` - Remove null-conditional operators (may cause NullReferenceException)
- `Rewrite` - Convert to explicit null checks: `person.BirthInfo?.Age` becomes `person.BirthInfo != null ? person.BirthInfo.Age : null`

### Updateable Methods

Generate update methods that modify existing instances instead of creating new ones:

```csharp
[Expressive]
public static partial class PersonMapper
{
    [Updateable]
    public static PersonDto UpdatePerson(Employee employee) => new PersonDto
    {
        Id = employee.EmployeeId,
        FullName = GetFullName(employee),
        Email = employee.ContactInfo.Email
    };
}

// Usage:
var existingDto = new PersonDto();
var employee = GetEmployee();

// Generated method signature: UpdatePerson(Employee source, PersonDto target)
PersonMapper.UpdatePerson(employee, existingDto);
// existingDto is now updated with employee data
```

## 🔍 How It Works

AlephMapper uses **Roslyn Source Generators** to analyze your mapping methods at compile time and generates corresponding expression tree methods.

For each method marked with `[Expressive]`:

1. **`MapToPersonDto(Employee employee)`** → generates **`MapToPersonDtoExpression()`** returning `Expression<Func<Employee, PersonDto>>`
2. **Method calls are inlined** - Calls to other methods in the same class are automatically inlined into the expression tree
3. **Null-conditional operators are handled** according to your specified rewrite policy
4. **Update methods** generate overloads that accept a target instance to modify

## ⚠️ Limitations

- Methods must be **static** and be member of a **partial static class**
- Supported only simple lambda-bodied or return methods
- Does not have circilar method call detection and protection. Be careful.
- Update methods are WIP and do not support inlining yet

## 🔄 Migration from Other Mappers

### From AutoMapper

```csharp
// AutoMapper
CreateMap<Employee, PersonDto>()
    .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FirstName + " " + src.LastName));

// AlephMapper
[Expressive]
public static partial class PersonMapper
{
    public static PersonDto MapToPerson(Employee employee) => new PersonDto
    {
        FullName = employee.FirstName + " " + employee.LastName,
        // ... other mappings
    };
}
```

### From Manual Expression Trees

```csharp
// Manual Expression Trees
Expression<Func<Employee, PersonDto>> expression = e => new PersonDto
{
    FullName = e.FirstName + " " + e.LastName
};

// AlephMapper - Same result, but with method reusability
[Expressive]
public static partial class PersonMapper
{
    public static PersonDto MapToPerson(Employee employee) => new PersonDto
    {
        FullName = GetFullName(employee) // This gets inlined in the expression
    };
    
    public static string GetFullName(Employee employee) => 
        employee.FirstName + " " + employee.LastName;
}
```

## 🤝 Contributing

We welcome contributions!

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Run the test suite
6. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Inspired by [EntityFrameworkCore.Projectables](https://github.com/koenbeuk/EntityFrameworkCore.Projectables) and [Expressionify](https://github.com/ClaveConsulting/Expressionify)
- Thanks to all [contributors](https://github.com/Raffinert/AlephMapper/graphs/contributors)

## 🔗 Related Projects

- [EntityFrameworkCore.Projectables](https://github.com/koenbeuk/EntityFrameworkCore.Projectables) - Similar concept with different approach
- [Expressionify](https://github.com/ClaveConsulting/Expressionify) - Similar concept with different approach
- [AutoMapper](https://automapper.org/) - Popular object-to-object mapper
- [Mapster](https://github.com/MapsterMapper/Mapster) - Fast object mapper

---

**Star ⭐ this repository if AlephMapper helps you build better applications!**