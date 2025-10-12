# Type-Aware Null Handling for UpdateableExpressionProcessor

## Problem Solved

The original `UpdateableExpressionProcessor` was generating incorrect null comparison logic for value types. It would generate code like:

```csharp
if (dest.Age == null)  // ERROR: Cannot compare value type 'int' to null
    dest.Age = new int();
```

This would cause compilation errors because value types cannot be null in C#.

## Solution Implemented

### 1. Type Information Annotation System (`TypeAnnotationHelpers.cs`)

- **`PropertyTypeInfo`**: Represents type information for a property path
- **`UpdateableTypeContext`**: Contains type information for all properties in a mapping
- Tracks whether properties can be null based on their actual types:
  - Value types: `CanBeNull = false`
  - Nullable value types (`int?`, `DateTime?`): `CanBeNull = true` 
  - Reference types: `CanBeNull = true`

### 2. Type Annotation Collector (`TypeAnnotationCollector.cs`)

- **`TypeAnnotationCollector`**: Walks the syntax tree and collects type information
- Uses `SemanticModel` to resolve actual types from syntax nodes
- Handles error cases gracefully to prevent source generator failures
- Recursively processes nested object creations and conditional expressions

### 3. Enhanced UpdateableExpressionProcessor (`EmitHelpers.cs`)

- Modified to accept `UpdateableTypeContext` parameter
- Uses type information to generate correct null handling logic:
  - **Value types**: Direct assignment without null checks
  - **Reference/Nullable types**: Proper null checks and else clauses

### 4. Source Generator Integration (`AlephSourceGenerator.cs`)

- Passes `SemanticModel` to `EmitHelpers.TryBuildUpdateAssignmentsWithInlining`
- Enables type collection during code generation

## Generated Code Examples

### Before Fix (Incorrect)
```csharp
if (dest.Age == null)  // COMPILATION ERROR
    dest.Age = new int();
```

### After Fix (Correct)
```csharp
// For value types - no null check needed
dest.Age = source.Age;

// For reference types - proper null handling
if (source.Person != null)
{
    if (dest.Person == null)
        dest.Person = new Person();
    // ... update properties
}
else
{
    dest.Person = null;
}
```

## Test Coverage

- **Value Types**: `int`, `bool`, `DateTime`, `decimal` - no null checks generated
- **Reference Types**: `string`, objects - proper null checks generated  
- **Nullable Value Types**: `int?`, `DateTime?` - treated as nullable
- **Mixed Scenarios**: Complex objects with both value and reference type properties
- **Backward Compatibility**: All existing tests pass

## Key Benefits

1. **Correct Code Generation**: No more compilation errors for value types
2. **Type Safety**: Proper null handling based on actual types
3. **Performance**: Avoids unnecessary null checks for value types
4. **Maintainability**: Clean, readable generated code
5. **Backward Compatibility**: Existing functionality unchanged

The solution is robust, handles edge cases gracefully, and maintains full backward compatibility while fixing the core issue with value type null handling.