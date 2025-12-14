# AlephMapper Comprehensive Tests

This test project provides comprehensive coverage of the AlephMapper source generator capabilities, demonstrating all major features and scenarios.

## Test Coverage

### 1. Expressive Mapping Tests (`SimpleIntegrationTests`)

#### Basic Functionality
- **Simple Property Expressions**: Tests basic property mapping with string interpolation
- **Null Conditional Expressions**: Tests null-conditional operators (`?.`) with different policies
- **Boolean Expressions**: Tests boolean property mapping and logic
- **Collection Count**: Tests collection count operations
- **Complex DTO Expression**: Tests complex object-to-DTO mapping with method inlining

#### Null Conditional Policies
- **Rewrite Policy**: Converts `?.` operators to explicit null checks
- **Ignore Policy**: Removes `?.` operators (may cause NullReferenceException)
- **None Policy**: Prohibits `?.` operators (requires explicit null checks)

#### Expression Tree Validation
- Validates that generated expressions have correct structure
- Verifies method inlining occurs properly
- Checks for proper string concatenation handling

### 2. Updatable Mapping Tests (`SimpleUpdateTests`)

#### Basic Update Operations
- **Simple Update**: Tests basic property updates on target objects
- **Null Handling**: Tests proper handling of null navigation properties
- **Department Update**: Tests updating simple nested objects

#### Advanced Update Scenarios
- **Reference Preservation**: Verifies that existing target objects are updated, not replaced
- **Nested Object Updates**: Tests complex nested object update scenarios
- **Partial Null Chains**: Tests handling of partially null navigation chains

## Test Architecture

### Models
The test project uses a comprehensive domain model representing an employee management system:

- **Employee**: Core entity with personal information
- **Department**: Organizational unit
- **EmployeeProfile**: Extended employee information
- **ContactInfo**: Emergency contact details
- **EmployeeAddress**: Employee addresses
- **Project**: Work projects
- **EmployeeProject**: Many-to-many relationship
- **Timesheet**: Time tracking

### Database Integration
- Uses SQLite in-memory database for fast test execution
- Entity Framework Core with proper relationships
- Comprehensive seed data for realistic testing scenarios

### Mappers Tested

#### `SimpleEmployeeMapper` (Expressive with Rewrite Policy)
- Basic property mapping
- Null conditional operators with rewrite
- Simple DTO creation
- Method inlining demonstration

#### `SimpleIgnoreMapper` (Expressive with Ignore Policy)
- Same functionality as above but with ignore policy
- Demonstrates different null handling behavior

#### `SimpleUpdateMapper` (Updatable)
- Simple property updates
- Nested object updates
- Demonstrates reference preservation

## Key Features Demonstrated

### 1. Expression Tree Generation
All Expressive mappers automatically generate corresponding expression tree methods:
- `GetFullName(Employee)` ? `GetFullNameExpression()`
- `MapToSimpleDto(Employee)` ? `MapToSimpleDtoExpression()`

### 2. Method Inlining
When using Expressive mappers, method calls to other methods in the same class are automatically inlined:
```csharp
public static EmployeeSimpleDto MapToSimpleDto(Employee employee) => new EmployeeSimpleDto
{
    // ...
    DepartmentName = GetDepartmentName(employee) // This gets inlined
};
```

### 3. Null Conditional Rewriting
With `NullConditionalRewrite.Rewrite`, this code:
```csharp
employee.Department?.Name ?? "No Department"
```

Gets converted to:
```csharp
(employee.Department != null ? employee.Department.Name : null) ?? "No Department"
```

### 4. Update Method Generation
Updatable mappers generate overloads that update existing target instances:
```csharp
[Updatable]
public static EmployeeSimpleDto UpdateSimpleDto(Employee employee) => new EmployeeSimpleDto { ... }
```

Generates:
```csharp
public static EmployeeSimpleDto UpdateSimpleDto(Employee source, EmployeeSimpleDto target) { ... }
```

## Usage Examples

### EF Core Projections
```csharp
var employeeDtos = await context.Employees
    .Include(e => e.Department)
    .Select(SimpleEmployeeMapper.MapToSimpleDtoExpression())
    .ToListAsync();
```

### In-Memory Mapping
```csharp
var employee = GetEmployee();
var dto = SimpleEmployeeMapper.MapToSimpleDto(employee);
```

### Update Operations
```csharp
var existingDto = new EmployeeSimpleDto();
var employee = GetEmployee();
SimpleUpdateMapper.UpdateSimpleDto(employee, existingDto);
// existingDto is now updated with employee data
```

## Test Data

The tests use a realistic dataset with:
- 6 employees with varying levels of data completeness
- 5 departments (4 active, 1 inactive)
- Complex relationships including self-referencing (manager/subordinate)
- Addresses, projects, and timesheets for integration testing

## Benefits

This comprehensive test suite demonstrates:

1. **Real-world applicability** - Uses realistic domain models and scenarios
2. **EF Core integration** - Shows how generated expressions work with database queries
3. **Error handling** - Tests null scenarios and edge cases
4. **Performance** - Uses in-memory database for fast test execution
5. **Maintainability** - Clear separation of test types and scenarios

The tests serve both as validation of the AlephMapper functionality and as documentation/examples for users of the library.