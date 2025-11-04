using System;
using AgileObjects.ReadableExpressions;
using AlephMapper.Tests;

namespace TestApp;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Tech Debt Investigation ===");
        
        try
        {
            Console.WriteLine("None Policy Expression:");
            var noneExpr = TechDebtPersonMapperNone.ToDtoExpression();
            Console.WriteLine(noneExpr.ToReadableString());
            Console.WriteLine();
            
            Console.WriteLine("Rewrite Policy Expression:");
            var rewriteExpr = TechDebtPersonMapperRewrite.ToDtoExpression();
            Console.WriteLine(rewriteExpr.ToReadableString());
            Console.WriteLine();
            
            Console.WriteLine("Ignore Policy Expression:");
            var ignoreExpr = TechDebtPersonMapperIgnore.ToDtoExpression();
            Console.WriteLine(ignoreExpr.ToReadableString());
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }
    }
}