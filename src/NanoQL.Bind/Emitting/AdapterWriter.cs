// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Emitting;

/// <summary>
/// Writes the engine adapters, one method per engine parameter, that turn a bound engine into the delegate a member
/// takes: the native host's shims and the Excel add-in call the same code, each on its runtime's bound-engine type, so
/// a member's arguments are placed by one derivation (docs/excel.md, "Bound engines").
/// </summary>
/// <param name="surface">The surface whose engine parameters and pairs are written.</param>
/// <param name="boundEngine">The runtime's bound-engine type, fully qualified with <c>global::</c>.</param>
internal sealed class AdapterWriter(Surface surface, string boundEngine)
{
    /// <summary>Writes every engine parameter's adapter, <c>A</c> and its index, as members of the class being written.</summary>
    /// <param name="sb">The class's text.</param>
    public void Write(StringBuilder sb)
    {
        foreach (EngineParameter p in surface.EngineParameters)
        {
            Adapter(sb, p);
        }
    }

    // A bound input's stored type: a span, which can't be boxed, as an array, which converts back to it at the call.
    internal static string Stored(Type t) =>
        t.IsConstructionOf("System.ReadOnlySpan`1") || t.IsConstructionOf("System.Span`1") ? CsNames.Of(t.GetGenericArguments()[0]) + "[]" : CsNames.Of(t);

    // The bound inputs a calculation reads, each taken, or its default where neither bound nor supplied.
    private static List<string> Takes(StringBuilder sb, ParameterInfo[] rest, ISet<int> supplied)
    {
        var names = new List<string>();
        for (int j = 0; j < rest.Length; j++)
        {
            string local = "x" + j + "_";
            names.Add(local);
            if (supplied.Contains(j))
            {
                continue;
            }

            string t = Stored(rest[j].ParameterType);
            string fallback = !rest[j].HasDefaultValue ? string.Empty : t != CsNames.Of(rest[j].ParameterType) ? "null" : CsNames.Literal(rest[j].ParameterType, rest[j].RawDefaultValue);
            // A nullable value neither bound nor supplied is null, C#'s value for it; a required one is refused.
            bool openNull = !rest[j].HasDefaultValue && rest[j].ParameterType.NullableOf() != null;
            sb.AppendLine(rest[j].HasDefaultValue || openNull
                ? $"                var {local} = b.TakeOr<{t}>(\"{rest[j].Name}\", {(openNull ? "null" : fallback)});"
                : $"                var {local} = b.Take<{t}>(\"{rest[j].Name}\");");
        }

        return names;
    }

    private static void Placed(StringBuilder sb, EnginePair pair)
    {
        MethodInfo calc = pair.Calculation!;
        ParameterInfo[] rest = [.. calc.GetParameters().Skip(2)];
        sb.AppendLine("                b.NoInstrument();");
        foreach (int t in pair.Targets)
        {
            sb.AppendLine($"                b.Open(\"{rest[t].Name}\");");
        }

        List<string> locals = Takes(sb, rest, new HashSet<int>(pair.Targets));
        var lambda = new List<string> { "i_", "c_" };
        for (int i = 0; i < pair.Targets.Count; i++)
        {
            lambda.Add("s" + i + "_");
            locals[pair.Targets[i]] = pair.Parameter.Cast[i] ? $"({CsNames.Of(rest[pair.Targets[i]].ParameterType)})s{i}_" : $"s{i}_";
        }

        sb.AppendLine($"                return ({string.Join(", ", lambda)}) => {CsNames.Of(calc.DeclaringType!)}.Calculate({string.Join(", ", new[] { "i_", "c_" }.Concat(locals))});");
    }

    private static void Composite(StringBuilder sb, EnginePair pair)
    {
        MethodInfo[] calcs = [.. pair.Set.Calculations.Where(c => pair.Parameter.Result.IsAssignableFrom(c.ReturnType))];

        // Most derived instrument first, so no arm is subsumed by one before it.
        Array.Sort(calcs, (a, b) => Depth(b.GetParameters()[0].ParameterType).CompareTo(Depth(a.GetParameters()[0].ParameterType)));
        ParameterInfo[] rest = [.. calcs[0].GetParameters().Skip(2)];
        List<string> locals = Takes(sb, rest, new HashSet<int>());
        sb.AppendLine("                var ins_ = b.NeedInstrument();");
        sb.AppendLine("                return ins_ switch").AppendLine("                {");
        foreach (MethodInfo c in calcs)
        {
            sb.AppendLine($"                    {CsNames.Of(c.GetParameters()[0].ParameterType)} t_ => c_ => {CsNames.Of(c.DeclaringType!)}.Calculate({string.Join(", ", new[] { "t_", "c_" }.Concat(locals))}),");
        }

        sb.AppendLine("                    _ => throw b.NoCalculation(member),").AppendLine("                };");
    }

    private static int Depth(Type t)
    {
        int d = 0;
        for (Type? b = t; b != null; b = b.BaseType)
        {
            d++;
        }

        return d + t.GetInterfaces().Length;
    }

    private void Adapter(StringBuilder sb, EngineParameter p)
    {
        string del = CsNames.Of(p.Delegate);
        sb.AppendLine($"    public static {del} A{p.Index}({boundEngine} b, string member)");
        sb.AppendLine("    {").AppendLine("        if (b == null) return null;").AppendLine("        switch (b.Set)").AppendLine("        {");
        foreach (EnginePair pair in surface.EnginePairs.Where(x => x.Parameter == p))
        {
            if (pair.Refused)
            {
                sb.AppendLine($"            case \"{pair.Set.Id}\": throw b.Refuse({System.Text.Json.JsonSerializer.Serialize(pair.Reason)});");
                continue;
            }

            if (!pair.Formed)
            {
                continue;
            }

            sb.AppendLine($"            case \"{pair.Set.Id}\":").AppendLine("            {");
            if (p.Instrument == null)
            {
                Composite(sb, pair);
            }
            else
            {
                Placed(sb, pair);
            }

            sb.AppendLine("            }");
        }

        sb.AppendLine("            default: throw b.NoCalculation(member);").AppendLine("        }").AppendLine("    }");
    }
}
