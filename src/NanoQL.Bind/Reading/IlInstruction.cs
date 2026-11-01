// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Reflection.Emit;

namespace NanoQL.Bind.Reading;

/// <summary>One decoded IL instruction: its opcode, its operand (a token, an integer or a double) and its offset.</summary>
/// <param name="Op">The opcode's value.</param>
/// <param name="Operand">A 32-bit operand: a token, an integer constant, a branch offset or a local's index.</param>
/// <param name="Real">An 8-byte floating-point operand.</param>
/// <param name="Offset">The instruction's offset in the body.</param>
internal readonly record struct IlInstruction(short Op, int Operand, double Real, int Offset)
{
    private static readonly Dictionary<short, OperandType> Operands = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => o.Value, o => o.OperandType);

    /// <summary>Decodes a method body's IL.</summary>
    /// <param name="il">The body's bytes.</param>
    /// <returns>Its instructions, in order.</returns>
    public static IEnumerable<IlInstruction> Decode(byte[] il)
    {
        int i = 0;
        while (i < il.Length)
        {
            int at = i;
            short v = il[i++];
            if (v == 0xFE)
            {
                v = (short)(0xFE00 | il[i++]);
            }

            int operand = 0;
            double real = 0;
            switch (Operands[v])
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    operand = v == OpCodes.Ldc_I4_S.Value ? (sbyte)il[i] : il[i];
                    i += 1;
                    break;
                case OperandType.InlineVar:
                    operand = BitConverter.ToInt16(il, i);
                    i += 2;
                    break;
                case OperandType.ShortInlineR:
                    real = BitConverter.ToSingle(il, i);
                    i += 4;
                    break;
                case OperandType.InlineR:
                    real = BitConverter.ToDouble(il, i);
                    i += 8;
                    break;
                case OperandType.InlineI8:
                    i += 8;
                    break;
                case OperandType.InlineSwitch:
                    i += 4 + (4 * BitConverter.ToInt32(il, i));
                    break;
                default:
                    operand = BitConverter.ToInt32(il, i);
                    i += 4;
                    break;
            }

            yield return new IlInstruction(v, operand, real, at);
        }
    }
}
