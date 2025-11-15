using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace AlephMapper.Tests;

public class NullableValueTypeUpdatableTests
{
    [Test]
    public async Task Updatable_Method_Should_Not_PreCreate_Nullable_Value_Types()
    {
        // The generated Updatable overload should exist with (source, destination) parameters
        var updatableMethod = typeof(NullableValueTypeMapper)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "MapToDestination" && m.GetParameters().Length == 2);

        // Inspect IL to make sure we aren't instantiating Nullable<T> via new / initobj
        var containsNullableInstantiation = IlInspectionHelper.ContainsNullableInstantiation(updatableMethod);

        await Assert.That(containsNullableInstantiation)
            .IsFalse();
    }
}

internal static class IlInspectionHelper
{
    private static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
    private static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];

    static IlInspectionHelper()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(OpCode)) continue;

            var op = (OpCode)field.GetValue(null)!;
            var value = (ushort)op.Value;

            if (value < 0x100)
            {
                OneByteOpCodes[value] = op;
            }
            else if ((value & 0xff00) == 0xfe00)
            {
                TwoByteOpCodes[value & 0xff] = op;
            }
        }
    }

    public static bool ContainsNullableInstantiation(MethodInfo method)
    {
        var body = method.GetMethodBody();
        if (body?.GetILAsByteArray() is not { Length: > 0 } il)
        {
            return false;
        }

        var module = method.Module;
        var cursor = 0;

        while (cursor < il.Length)
        {
            var opCode = ReadOpCode(il, ref cursor);

            if (opCode == OpCodes.Newobj && opCode.OperandType == OperandType.InlineMethod)
            {
                var ctorToken = ReadInt32(il, ref cursor);
                var ctor = module.ResolveMethod(ctorToken);

                if (IsNullableConstructor(ctor))
                {
                    return true;
                }

                continue;
            }

            if (opCode == OpCodes.Initobj && opCode.OperandType == OperandType.InlineType)
            {
                var typeToken = ReadInt32(il, ref cursor);
                var operandType = module.ResolveType(typeToken);

                if (IsNullableType(operandType))
                {
                    return true;
                }

                continue;
            }

            SkipOperand(opCode, il, ref cursor);
        }

        return false;
    }

    private static bool IsNullableConstructor(MethodBase methodBase)
        => methodBase.Name == ".ctor"
           && methodBase.DeclaringType?.IsGenericType == true
           && methodBase.DeclaringType.GetGenericTypeDefinition() == typeof(Nullable<>);

    private static bool IsNullableType(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);

    private static OpCode ReadOpCode(byte[] il, ref int cursor)
    {
        var op = il[cursor++];
        if (op != 0xFE)
        {
            return OneByteOpCodes[op];
        }

        var second = il[cursor++];
        return TwoByteOpCodes[second];
    }

    private static int ReadInt32(byte[] il, ref int cursor)
    {
        var value = BitConverter.ToInt32(il, cursor);
        cursor += 4;
        return value;
    }

    private static void SkipOperand(OpCode opCode, byte[] il, ref int cursor)
    {
        switch (opCode.OperandType)
        {
            case OperandType.InlineNone:
                return;
            case OperandType.ShortInlineBrTarget:
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
                cursor += 1;
                return;
            case OperandType.InlineVar:
                cursor += 2;
                return;
            case OperandType.InlineI:
            case OperandType.InlineField:
            case OperandType.InlineMethod:
            case OperandType.InlineSig:
            case OperandType.InlineString:
            case OperandType.InlineTok:
            case OperandType.InlineType:
            case OperandType.InlineBrTarget:
                cursor += 4;
                return;
            case OperandType.ShortInlineR:
                cursor += 4;
                return;
            case OperandType.InlineI8:
            case OperandType.InlineR:
                cursor += 8;
                return;
            case OperandType.InlineSwitch:
                var count = BitConverter.ToInt32(il, cursor);
                cursor += 4 + (count * 4);
                return;
            default:
                throw new NotSupportedException($"Unsupported operand type '{opCode.OperandType}'.");
        }
    }
}
