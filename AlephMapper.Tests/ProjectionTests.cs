using System.Linq.Expressions;

namespace AlephMapper.Tests;

public class ProjectionTests
{
    [Test]
    public async Task Should_Generate_MapToDestDtoExpression_Method()
    {
        // Arrange & Act
        var expression = Mapper.MapToDestDtoExpression();
        
        // Assert
        await Assert.That(expression).IsNotNull();
        await Assert.That(expression).IsAssignableTo<Expression<Func<SourceDto, DestDto>>>();
    }

    [Test]
    public async Task Should_Generate_Correct_Expression()
    {
        // Arrange
        var expression = Mapper.MapToDestDtoExpression();
        var compiled = expression.Compile();
        var source = new SourceDto 
        { 
            Name = "John Doe", 
            BirthInfo = new BirthInfo { Age = 30, Address = "123 Main St" },
            Email = "john@example.com" 
        };

        // Act
        var result = compiled(source);

        // Assert
        await Assert.That(result.Name).IsEqualTo("John Doe");
        await Assert.That(result.BirthInfo).IsNotNull();
        await Assert.That(result.BirthInfo.Age).IsEqualTo(30);
        await Assert.That(result.BirthInfo.Address).IsEqualTo("123 Main St");
        await Assert.That(result.ContactInfo).IsEqualTo("john@example.com");
    }

    [Test]
    public async Task Expression_Should_Be_Queryable()
    {
        // Arrange
        var expression = Mapper.MapToDestDtoExpression();
        
        // Act
        var body = expression.Body;
        
        // Assert - Expression should represent object creation with property assignments
        await Assert.That(body).IsTypeOf<MemberInitExpression>();
        var memberInit = (MemberInitExpression)body;
        await Assert.That(memberInit.Type).IsEqualTo(typeof(DestDto));
        
        // Should have bindings for Name, BirthInfo, and ContactInfo (3 bindings)
        await Assert.That(memberInit.Bindings.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Should_Unwrap_Nested_Method_Calls()
    {
        // Arrange
        var expression = Mapper.MapToDestDtoExpression();
        
        // Act
        var body = expression.Body as MemberInitExpression;
        
        // Assert
        await Assert.That(body).IsNotNull();
        
        // Find the BirthInfo binding - it should be inlined, not a method call
        var birthInfoBinding = body.Bindings
            .OfType<MemberAssignment>()
            .FirstOrDefault(b => b.Member.Name == "BirthInfo");
        
        await Assert.That(birthInfoBinding).IsNotNull();
        
        // The expression should be a conditional expression due to null check
        // Note: In C# expressions, this might be FullConditionalExpression
        await Assert.That(birthInfoBinding.Expression is ConditionalExpression || 
                   birthInfoBinding.Expression.GetType().Name.Contains("ConditionalExpression")).IsTrue();
        
        var conditionalExpr = birthInfoBinding.Expression as ConditionalExpression;
        await Assert.That(conditionalExpr).IsNotNull();
        
        // The IfTrue part should be a MemberInitExpression for BirthInfoDto (inlined method call)
        await Assert.That(conditionalExpr.IfTrue).IsTypeOf<MemberInitExpression>();
        
        var birthInfoMemberInit = (MemberInitExpression)conditionalExpr.IfTrue;
        await Assert.That(birthInfoMemberInit.Type).IsEqualTo(typeof(BirthInfoDto));
        
        // Should have Age and Address bindings
        await Assert.That(birthInfoMemberInit.Bindings.Count).IsEqualTo(2);
        
        // Verify that it's not a method call expression
        await Assert.That(conditionalExpr.IfTrue).IsNotTypeOf<MethodCallExpression>();
    }
}