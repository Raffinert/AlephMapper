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

AlephMapper is a **Source Generator** that automatically creates projectable and/or updatable companion methods from your mapping logic. It enables you to write mapping methods once and use them both for in-memory objects and as expression trees for database queries (Entity Framework Core projections).

## 🚀 Features

- **Expression Tree Generation** - Automatically converts method bodies to expression trees
- **Null-Conditional Operator Support** - Configurable handling of `?.` operators
- **Updatable Methods** - Generate update methods that modify existing instances

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

### Updatable Methods

From a single mapping, AlephMapper can emit an **update-in-place** overload that writes into an existing instance (instead of creating a new one). This is especially suitable for **EF Core entity updates with change tracking**: you keep the **same tracked instance**, so EF can detect modified properties and produce the correct `UPDATE`.

```csharp
[Updatable]
public static Person MapToPerson(PersonUpdateDto dto) => new Person
{
    FirstName = dto.FirstName,
    LastName  = dto.LastName,
    Email     = dto.Email
    // ...
};

// Generated usage with EF Core change tracking:
var person = await db.People.FindAsync(id);           // tracked entity
PersonMapper.MapToPerson(source: dto, target: person); // mutate in-place
await db.SaveChangesAsync();                           // EF sees changes on the same instance
```

## 🔍 How It Works

For each method marked with `[Expressive]`:

1. **`MapToPersonDto(Employee employee)`** → generates **`MapToPersonDtoExpression()`** returning `Expression<Func<Employee, PersonDto>>`
2. **Method calls are inlined** - Calls to other methods in the same class are automatically inlined into the expression tree
3. **Null-conditional operators are handled** according to your specified rewrite policy

For each method marked with `[Updatable]`:

1. **`MapToPersonDto(Employee employee)`** → generates **`MapToPersonDto(Employee employee, PersonDto target)`** returning `PersonDto`
2. **Method calls are inlined** - Calls to other methods in the same class are automatically inlined into the method

## ⚠️ Limitations

- Methods must be **static** and be member of a **partial static class**.
- Supported only simple lambda-bodied or return methods.

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
- [Facet](https://github.com/Tim-Maes/Facet)

---

**Star ⭐ this repository if AlephMapper helps you build better applications!**
