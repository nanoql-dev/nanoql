// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Selecting;

/// <summary>
/// Bound engines (docs/bindings.md, "Bound engines"). An engine is a class whose public static <c>Calculate</c>
/// methods take an instrument, the context and inputs, and return results. Each set of inputs gets a
/// <c>Bind</c> function, every input optional, and a <c>BindOn</c> that takes the instrument too. A member that takes an
/// engine as a delegate gets, per engine input set, the calculation for the most derived instrument type its own is,
/// each argument the member supplies placed by its role: an object into the one parameter of its type; a value, an
/// object two parameters take, and a calibration helper's trial model by the roles table's names.
/// </summary>
internal sealed class EngineBinder
{
    private readonly WellKnown known;
    private readonly RolesTable roles;
    private readonly List<Type> concrete;

    /// <summary>Initializes a new instance of the <see cref="EngineBinder"/> class.</summary>
    /// <param name="known">The core types.</param>
    /// <param name="roles">The roles table.</param>
    public EngineBinder(WellKnown known, RolesTable roles)
    {
        this.known = known;
        this.roles = roles;
        concrete = [.. known.Set.Assemblies.SelectMany(a => a.Assembly.GetExportedTypes()).Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)];
        Sets = FindSets();
    }

    /// <summary>Gets every engine input set, by engine name, then index.</summary>
    public List<EngineInputSet> Sets { get; }

    /// <summary>Gets the engine parameters found, by adapter index.</summary>
    public List<EngineParameter> Parameters { get; } = [];

    /// <summary>The engine shape a delegate parameter has, or null when no engine fits it.</summary>
    /// <param name="closed">The parameter's delegate type, a generic method's arguments closed.</param>
    /// <param name="definition">The parameter's type in the generic method's definition, or null.</param>
    /// <returns>The shape, with its adapter index, or null.</returns>
    public EngineParameter? Shape(Type closed, Type? definition)
    {
        MethodInfo? invoke = closed.GetMethod("Invoke");
        if (invoke == null || !known.InstrumentResults.IsAssignableFrom(invoke.ReturnType))
        {
            return null;
        }

        ParameterInfo[] ps = invoke.GetParameters();
        ParameterInfo[]? defs = definition?.GetMethod("Invoke")?.GetParameters();
        EngineParameter? shape = null;
        if (ps.Length == 1 && ps[0].ParameterType == known.EvaluationContext)
        {
            shape = new EngineParameter(closed, null, [], [], invoke.ReturnType, Parameters.Count);
        }
        else if (ps.Length >= 2 && ps[1].ParameterType == known.EvaluationContext && ps[0].ParameterType != known.EvaluationContext)
        {
            if (defs != null && defs[0].ParameterType.IsGenericParameter)
            {
                return null;
            }

            Type[] supplied = [.. ps.Skip(2).Select(p => p.ParameterType)];
            bool[] cast = [.. Enumerable.Range(0, supplied.Length).Select(i => defs != null && defs[i + 2].ParameterType.IsGenericParameter)];
            shape = new EngineParameter(closed, ps[0].ParameterType, supplied, cast, invoke.ReturnType, Parameters.Count);
        }

        if (shape != null)
        {
            Parameters.Add(shape);
        }

        return shape;
    }

    /// <summary>
    /// Every pair of a member's engine parameter and an engine input set whose calculations are for the member's
    /// instrument: formed, refused at the call, or forming no pair, with the reason.
    /// </summary>
    /// <param name="member">The member's function.</param>
    /// <param name="shape">Its engine parameter.</param>
    /// <returns>The pairs.</returns>
    public IEnumerable<EnginePair> Pairs(CFunction member, EngineParameter shape)
    {
        string name = MemberName(member);
        foreach (EngineInputSet set in Sets)
        {
            if (shape.Instrument == null)
            {
                // The composite shape: the instrument comes with the engine (BindOn), each calculation of the set for it.
                MethodInfo[] fit = [.. set.Calculations.Where(c => shape.Result.IsAssignableFrom(c.ReturnType))];
                if (fit.Length > 0)
                {
                    yield return new EnginePair(member, shape, set, fit[0], [], [], null, false);
                }

                continue;
            }

            MethodInfo[] candidates = [.. set.Calculations.Where(c => c.GetParameters()[0].ParameterType.IsAssignableFrom(shape.Instrument) && shape.Result.IsAssignableFrom(c.ReturnType))];
            if (candidates.Length == 0)
            {
                continue;
            }

            // The calculation for the most derived instrument type the member's is.
            MethodInfo[] best = [.. candidates.Where(c => candidates.All(o => o.GetParameters()[0].ParameterType.IsAssignableFrom(c.GetParameters()[0].ParameterType)))];
            if (best.Length != 1)
            {
                throw new BindException($"{name}: two calculations of {set.Engine.FullName} tie for its instrument {shape.Instrument.Name}: {string.Join("; ", candidates.Select(c => c.MemberKey()))}");
            }

            MethodInfo calc = best[0];
            (int[]? targets, string[] how, string? reason, bool refused) = Place(calc, shape, name);
            yield return new EnginePair(member, shape, set, calc, targets ?? [], how, reason, refused);
        }
    }

    /// <summary>The member's name as the roles table writes it: its declaring type's full name, a dot, its name.</summary>
    /// <param name="member">The member's function.</param>
    /// <returns>The name.</returns>
    public static string MemberName(CFunction member) =>
        (member.Holder.IsConstructedGenericType ? member.Holder.GetGenericTypeDefinition() : member.Holder).FullName + "." + member.Member!.Name;

    /// <summary>The Bind and BindOn functions of each input set whose inputs all cross, unnamed.</summary>
    /// <param name="classifier">The kinds.</param>
    /// <param name="typeName">The engines' C names.</param>
    /// <param name="prefix">An engine's prefix.</param>
    /// <param name="skip">Receives an input set that can't be bound, and why.</param>
    /// <returns>The functions.</returns>
    public IEnumerable<CFunction> BindFunctions(KindClassifier classifier, Func<Type, string> typeName, Func<Type, string> prefix, Action<EngineInputSet, string> skip)
    {
        foreach (EngineInputSet set in Sets)
        {
            var inputs = new List<CParam>();
            string? why = null;
            foreach (ParameterInfo p in set.Inputs)
            {
                CKind? k = classifier.Classify(p.ParameterType, p, false, null, out why);
                if (k == null)
                {
                    why = $"input {p.Name}: {why}";
                    break;
                }

                inputs.Add(new CParam(p.Name!, Open(k), true, p.HasDefaultValue ? p.RawDefaultValue : null));
            }

            if (why != null)
            {
                skip(set, why);
                continue;
            }

            Type instrument = CommonBase(set.Calculations.Select(c => c.GetParameters()[0].ParameterType));
            bool experimental = set.Engine.Has(WellKnown.ExperimentalAttribute) || set.Calculations.All(c => c.Has(WellKnown.ExperimentalAttribute));
            foreach (bool on in new[] { false, true })
            {
                var f = new CFunction
                {
                    Prefix = prefix(set.Engine),
                    TypeName = typeName(set.Engine),
                    BaseName = on ? "BindOn" : "Bind",
                    Kind = on ? "bindon" : "bind",
                    Type = set.Engine,
                    Holder = set.Engine,
                    Static = true,
                    Experimental = experimental,
                    InputSet = set,
                    Key = set.Id + (on ? "::BindOn" : "::Bind"),
                    Return = new CKind("handle", known.InstrumentResults),
                };
                if (on)
                {
                    f.Params.Add(new CParam("instrument", new CKind("handle", instrument), false, null));
                }

                f.Params.AddRange(inputs);
                f.SignatureNames = [.. (on ? new[] { "Instrument" } : []).Concat(set.Inputs.Select(p => OverloadNamer.SigName(p.ParameterType)))];
                yield return f;
            }
        }
    }

    // An input every Bind leaves open by default: an object NULL, a value through a pointer, NULL for open.
    private static CKind Open(CKind k) => k.Name switch
    {
        "double" or "int32" or "int64" or "bool" or "period" or "enum" or "complex" => k with { Name = k.Name + "?" },
        "boxed" => k with { Name = "boxed?" },
        _ => k,
    };

    private static Type CommonBase(IEnumerable<Type> types)
    {
        Type[] all = [.. types];
        for (Type? b = all[0]; b != null; b = b.BaseType)
        {
            if (all.All(b.IsAssignableFrom))
            {
                return b;
            }
        }

        return all[0];
    }

    private List<EngineInputSet> FindSets()
    {
        var sets = new List<EngineInputSet>();
        foreach (Type engine in known.Set.Assemblies.SelectMany(a => a.Assembly.GetExportedTypes())
                     .Where(t => t.IsClass && !t.IsGenericTypeDefinition && !t.IsNested)
                     .OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var calcs = engine.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == "Calculate" && !m.IsGenericMethodDefinition)
                .Where(m => m.GetParameters() is { Length: >= 2 } ps && ps[1].ParameterType == known.EvaluationContext && ps[0].ParameterType != known.EvaluationContext
                    && !ps[0].ParameterType.IsValueType && known.InstrumentResults.IsAssignableFrom(m.ReturnType))
                .OrderBy(m => m.MetadataToken)
                .ToList();
            foreach (IGrouping<string, MethodInfo> g in calcs.GroupBy(c => string.Join(",", c.GetParameters().Skip(2).Select(p => p.Name + ":" + p.ParameterType.KeyName()))))
            {
                MethodInfo first = g.First();
                sets.Add(new EngineInputSet(engine, sets.Count(s => s.Engine == engine), [.. first.GetParameters().Skip(2)], [.. g]));
            }
        }

        return sets;
    }

    private static bool IsValue(Type t) => (t.NullableOf() ?? t).IsValueType;

    // A parameter takes the argument when the argument is of its type; a cast argument also when some public class is
    // both (a calibrated model that is a Gaussian1dModel).
    private bool Fits(Type param, Type arg, bool cast)
    {
        Type u = param.NullableOf() ?? param;
        if (u.IsAssignableFrom(arg))
        {
            return true;
        }

        return cast && !u.IsValueType && u.FullName != "System.Object" && concrete.Any(m => arg.IsAssignableFrom(m) && u.IsAssignableFrom(m));
    }

    private (int[]? Targets, string[] How, string? Reason, bool Refused) Place(MethodInfo calc, EngineParameter shape, string member)
    {
        ParameterInfo[] rest = [.. calc.GetParameters().Skip(2)];
        int[] targets = new int[shape.Supplied.Count];
        string[] how = new string[shape.Supplied.Count];
        for (int i = 0; i < shape.Supplied.Count; i++)
        {
            Type a = shape.Supplied[i];
            int[] fit = [.. Enumerable.Range(0, rest.Length).Where(j => Fits(rest[j].ParameterType, a, shape.Cast[i]))];
            string[]? names = roles.For(member, i);
            bool byName = IsValue(a) || fit.Length > 1 || (shape.Cast[i] && names != null);
            if (fit.Length == 0)
            {
                return (null, how, $"no parameter takes the member's {TypeNamer.Simple(a)}", false);
            }

            if (!byName)
            {
                targets[i] = fit[0];
                how[i] = $"{TypeNamer.Simple(a)} by type into {rest[fit[0]].Name}";
                continue;
            }

            if (names == null)
            {
                throw new BindException(IsValue(a)
                    ? $"{member}: the {TypeNamer.Simple(a)} it supplies to {calc.DeclaringType!.FullName} has no entry in the roles table (argument {i})"
                    : $"{member}: two parameters of {calc.DeclaringType!.FullName} take the {TypeNamer.Simple(a)} it supplies ({string.Join(", ", fit.Select(j => rest[j].Name))}) and the roles table names neither (argument {i})");
            }

            int[] named = [.. fit.Where(j => names.Contains(rest[j].Name))];
            if (named.Length != 1)
            {
                return (null, how, $"no parameter '{string.Join("' or '", names)}' for the member's {TypeNamer.Simple(a)}", true);
            }

            targets[i] = named[0];
            how[i] = $"{TypeNamer.Simple(a)} by name into {rest[named[0]].Name}" + (shape.Cast[i] ? ", cast" : string.Empty);
        }

        return (targets, how, null, false);
    }
}
