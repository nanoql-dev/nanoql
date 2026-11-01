// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Reflection.Emit;

namespace NanoQL.Bind.Reading;

/// <summary>
/// Reads the extensions' coded presets from IL, never by running them: the preset factories' static constructor pairs
/// each name (an <c>ldstr</c>) with a lambda (an <c>ldftn</c>) and stores the dictionary (an <c>stsfld</c>); each
/// lambda's IL holds the preset's constructor calls (<c>newobj</c>), whose arguments a small stack walk reads as
/// constants or the lambda's parameters; a local the walk stored is read back, and one cleared by <c>initobj</c> is its
/// type's default (an optional <c>Date?</c> left out), null where the parameter takes null.
/// </summary>
internal static class PresetReader
{
    /// <summary>The preset factories' type, in the extensions.</summary>
    public const string FactoriesType = "NanoQL.Extensions.Conventions.PresetFactories";

    /// <summary>Reads every input's presets.</summary>
    /// <param name="set">The inputs.</param>
    /// <returns>The presets, in the static constructor's order.</returns>
    public static IReadOnlyList<PresetFactory> Read(MetadataSet set)
    {
        var presets = new List<PresetFactory>();
        foreach ((_, Assembly assembly) in set.Assemblies)
        {
            Type? factories = assembly.GetType(FactoriesType);
            if (factories?.TypeInitializer is not { } cctor)
            {
                continue;
            }

            using var tokens = new TokenResolver(set, assembly);
            string? name = null;
            var pending = new List<(string Name, MethodInfo Lambda)>();
            foreach (IlInstruction ins in IlInstruction.Decode(tokens.Body(cctor)))
            {
                if (ins.Op == OpCodes.Ldstr.Value)
                {
                    name = tokens.String(ins.Operand);
                }
                else if (ins.Op == OpCodes.Ldftn.Value && name != null)
                {
                    pending.Add((name, (MethodInfo)tokens.Method(ins.Operand).Member!));
                    name = null;
                }
                else if (ins.Op == OpCodes.Stsfld.Value)
                {
                    string field = tokens.FieldName(ins.Operand);
                    foreach ((string n, MethodInfo lambda) in pending)
                    {
                        IReadOnlyList<PresetConstruction> calls = Constructions(set, tokens, lambda);
                        if (calls.Count > 0)
                        {
                            presets.Add(new PresetFactory(field, n, lambda, calls));
                        }
                    }

                    pending.Clear();
                }
            }
        }

        return presets;
    }

    // The constructor calls of the inputs' own types in a lambda's body, each argument read from a stack walk: a
    // constant, a parameter, or computed (anything else).
    private static List<PresetConstruction> Constructions(MetadataSet set, TokenResolver tokens, MethodInfo lambda)
    {
        // The walk's own entries, never in a construction: a local's address (its index in Constant), and a cleared
        // local's value, its type's default.
        const int address = -2;
        var cleared = new PresetArgument(null, false, -3);
        var calls = new List<PresetConstruction>();
        var stack = new List<PresetArgument>();
        var locals = new Dictionary<int, PresetArgument>();
        int first = lambda.IsStatic ? 0 : 1;
        PresetArgument Pop()
        {
            if (stack.Count == 0)
            {
                return PresetArgument.Computed;
            }

            PresetArgument top = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            return top;
        }

        void Arg(int index) => stack.Add(index >= first ? new PresetArgument(null, false, index - first) : PresetArgument.Computed);
        void Const(object? value) => stack.Add(new PresetArgument(value, true, -1));
        void Load(int index) => stack.Add(locals.GetValueOrDefault(index, PresetArgument.Computed));
        void Store(int index) => locals[index] = Pop();

        foreach (IlInstruction ins in IlInstruction.Decode(tokens.Body(lambda)))
        {
            short op = ins.Op;
            if (op >= OpCodes.Ldarg_0.Value && op <= OpCodes.Ldarg_3.Value)
            {
                Arg(op - OpCodes.Ldarg_0.Value);
            }
            else if (op == OpCodes.Ldarg_S.Value || op == OpCodes.Ldarg.Value)
            {
                Arg(ins.Operand);
            }
            else if (op >= OpCodes.Ldc_I4_M1.Value && op <= OpCodes.Ldc_I4_8.Value)
            {
                Const(op - OpCodes.Ldc_I4_0.Value);
            }
            else if (op == OpCodes.Ldc_I4_S.Value || op == OpCodes.Ldc_I4.Value)
            {
                Const(ins.Operand);
            }
            else if (op == OpCodes.Ldc_R8.Value || op == OpCodes.Ldc_R4.Value)
            {
                Const(ins.Real);
            }
            else if (op == OpCodes.Ldstr.Value)
            {
                Const(tokens.String(ins.Operand));
            }
            else if (op == OpCodes.Ldnull.Value)
            {
                Const(null);
            }
            else if (op >= OpCodes.Ldloc_0.Value && op <= OpCodes.Ldloc_3.Value)
            {
                Load(op - OpCodes.Ldloc_0.Value);
            }
            else if (op == OpCodes.Ldloc_S.Value || op == OpCodes.Ldloc.Value)
            {
                Load(ins.Operand);
            }
            else if (op >= OpCodes.Stloc_0.Value && op <= OpCodes.Stloc_3.Value)
            {
                Store(op - OpCodes.Stloc_0.Value);
            }
            else if (op == OpCodes.Stloc_S.Value || op == OpCodes.Stloc.Value)
            {
                Store(ins.Operand);
            }
            else if (op == OpCodes.Ldloca_S.Value || op == OpCodes.Ldloca.Value)
            {
                stack.Add(new PresetArgument(ins.Operand, false, address));
            }
            else if (op == OpCodes.Initobj.Value)
            {
                if (Pop() is { Parameter: address, Constant: int local })
                {
                    locals[local] = cleared;
                }
            }
            else if (op == OpCodes.Newobj.Value)
            {
                (MethodBase? member, int count, _, _) = tokens.Method(ins.Operand);
                var args = new PresetArgument[count];
                for (int k = args.Length - 1; k >= 0; k--)
                {
                    args[k] = Pop();
                }

                if (member is ConstructorInfo ctor && set.Ours(ctor.DeclaringType!))
                {
                    ParameterInfo[] ps = ctor.GetParameters();
                    for (int k = 0; k < args.Length; k++)
                    {
                        Type t = ps[k].ParameterType;
                        bool takesNull = !t.IsValueType || (t.IsGenericType && t.GetGenericTypeDefinition().FullName == "System.Nullable`1");
                        args[k] = args[k] == cleared && takesNull ? new PresetArgument(null, true, -1)
                            : args[k].Parameter < -1 ? PresetArgument.Computed
                            : args[k];
                    }

                    calls.Add(new PresetConstruction(ctor, args));
                }

                stack.Add(PresetArgument.Computed);
            }
            else if (op == OpCodes.Call.Value || op == OpCodes.Callvirt.Value)
            {
                (_, int count, bool instance, bool isVoid) = tokens.Method(ins.Operand);
                for (int k = count + (instance ? 1 : 0); k > 0; k--)
                {
                    Pop();
                }

                if (!isVoid)
                {
                    stack.Add(PresetArgument.Computed);
                }
            }
            else if (op == OpCodes.Dup.Value)
            {
                stack.Add(stack.Count > 0 ? stack[^1] : PresetArgument.Computed);
            }
            else if (op == OpCodes.Pop.Value || op == OpCodes.Ret.Value || op == OpCodes.Brtrue.Value || op == OpCodes.Brtrue_S.Value
                     || op == OpCodes.Brfalse.Value || op == OpCodes.Brfalse_S.Value)
            {
                Pop();
            }
            else if (op == OpCodes.Br.Value || op == OpCodes.Br_S.Value || op == OpCodes.Nop.Value)
            {
            }
            else
            {
                // Anything else computes: its value can't be read as a constant or a parameter.
                stack.Add(PresetArgument.Computed);
            }
        }

        return calls;
    }
}
