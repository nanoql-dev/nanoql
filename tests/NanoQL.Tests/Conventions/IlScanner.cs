// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Reflection.Emit;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Reads method bodies by reflection: each <c>newobj</c>, <c>call</c>, <c>callvirt</c>, <c>ldftn</c> and
/// <c>ldvirtftn</c> with the member it names, and the member a compiler-generated body belongs to.
/// </summary>
public static class IlScanner
{
    private static readonly OpCode[] OneByte = new OpCode[0x100];
    private static readonly OpCode[] TwoByte = new OpCode[0x100];

    static IlScanner()
    {
        foreach (FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (f.GetValue(null) is OpCode op)
            {
                ushort v = (ushort)op.Value;
                if (v < 0x100)
                {
                    OneByte[v] = op;
                }
                else
                {
                    TwoByte[v & 0xff] = op;
                }
            }
        }
    }

    /// <summary>The members a method's body calls, constructs or loads as a function pointer.</summary>
    /// <param name="method">The method or constructor.</param>
    /// <returns>Each instruction's opcode and the member it names, in order.</returns>
    public static List<(OpCode OpCode, MethodBase Member)> References(MethodBase method)
    {
        var result = new List<(OpCode, MethodBase)>();
        byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            return result;
        }

        Type[]? typeArgs = method.DeclaringType is { IsGenericType: true } t ? t.GetGenericArguments() : null;
        Type[]? methodArgs = method is MethodInfo { IsGenericMethod: true } m ? m.GetGenericArguments() : null;
        for (int i = 0; i < il.Length;)
        {
            byte b = il[i++];
            OpCode op = b == 0xfe ? TwoByte[il[i++]] : OneByte[b];
            if (op.OperandType == OperandType.InlineMethod)
            {
                MethodBase? target = method.Module.ResolveMethod(BitConverter.ToInt32(il, i), typeArgs, methodArgs);
                if (target is not null)
                {
                    result.Add((op, target));
                }
            }

            i += op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, i)),
                _ => 4,
            };
        }

        return result;
    }

    /// <summary>
    /// The member a body belongs to, as <c>Namespace.Type.Member</c>: a lambda's, local function's or state machine's
    /// by the compiler's names, on its outermost declared type, a <c>file</c> type by its own name, generic arity
    /// apart.
    /// </summary>
    /// <param name="method">The method or constructor.</param>
    /// <returns>The member's name.</returns>
    public static string Owner(MethodBase method)
    {
        string member = method.Name.StartsWith('<') ? method.Name[1..method.Name.IndexOf('>')] : method.Name;
        Type type = method.DeclaringType!;
        while (type.Name.StartsWith('<') && type.DeclaringType is not null)
        {
            if (type.Name.Contains("d__", StringComparison.Ordinal) && !type.Name.Contains("DisplayClass", StringComparison.Ordinal))
            {
                member = type.Name[1..type.Name.IndexOf('>')];
            }

            type = type.DeclaringType;
        }

        return $"{TypeName(type)}.{member}";
    }

    /// <summary>A type's name as <see cref="Owner"/> writes it: namespace and nesting, a file type unmangled.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The name.</returns>
    public static string TypeName(Type type)
    {
        string name = type.Name;
        int file = name.IndexOf("__", StringComparison.Ordinal);
        if (name.StartsWith('<') && file > 0)
        {
            name = name[(file + 2)..];
        }

        int arity = name.IndexOf('`', StringComparison.Ordinal);
        name = arity > 0 ? name[..arity] : name;
        return type.DeclaringType is { } outer ? $"{TypeName(outer)}+{name}" : $"{type.Namespace}.{name}";
    }
}
