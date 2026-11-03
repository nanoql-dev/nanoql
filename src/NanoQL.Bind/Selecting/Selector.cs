// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Bind.Model;
using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Selecting;

/// <summary>
/// Selects the C surface of a set of assemblies (docs/bindings.md): every public top-level type and its public declared
/// members, a named class's closed generic base's members too, each parameter and result mapped to its kind; what
/// doesn't cross is listed with its reason. The working types take their forms (<c>Matrix</c> and <c>Vector</c> as
/// arrays, generators as snapshots drawn in batches, a loss distribution as its values) and the rest stay inside; the
/// coded presets' constructors are withheld for the convention set's lookups by name; members that take an engine
/// take a bound one.
/// </summary>
internal sealed class Selector
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private readonly MetadataSet set;
    private readonly WellKnown known;
    private readonly KindClassifier classifier;
    private readonly EngineBinder binder;
    private readonly ShippedSurface shipped;
    private readonly ExceptionDocs docs;
    private readonly TypeNamer typeNames;
    private readonly Surface surface = new();
    private readonly HashSet<Type> presetTypes;
    private readonly IReadOnlyList<PresetFactory> presets;
    private readonly Dictionary<Type, bool> inheritsExperimental = [];

    private Selector(MetadataSet set, RolesTable roles, ShippedSurface shipped, ExceptionDocs docs)
    {
        this.set = set;
        this.shipped = shipped;
        this.docs = docs;
        known = new WellKnown(set);
        classifier = new KindClassifier(known);
        binder = new EngineBinder(known, roles);
        typeNames = new TypeNamer(set, shipped);
        presets = PresetReader.Read(set);
        presetTypes = [.. presets.Select(p => p.PresetType)];
        surface.Roles.AddRange(roles.Entries.Select(e => (e.Key.Member, e.Key.Argument, e.Value)));
    }

    /// <summary>Selects the surface.</summary>
    /// <param name="set">The inputs.</param>
    /// <param name="roles">The roles table.</param>
    /// <param name="shipped">The last release's surface.</param>
    /// <param name="docs">The documented exceptions.</param>
    /// <returns>The surface.</returns>
    public static Surface Select(MetadataSet set, RolesTable roles, ShippedSurface shipped, ExceptionDocs docs) =>
        new Selector(set, roles, shipped, docs).Run();

    private Surface Run()
    {
        surface.Assemblies.AddRange(set.Assemblies.Select(a => (a.Prefix, a.Assembly.GetName().Name!)));
        var functions = new List<CFunction>();
        foreach ((string prefix, Assembly assembly) in set.Assemblies)
        {
            foreach (Type t in assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                if (t.IsNested)
                {
                    Skip(t.FullName!, t.FullName!, "nested type (compiler-generated or nested)");
                    continue;
                }

                if (t.IsEnum)
                {
                    surface.Enums.Add(new EnumRow(
                        t, prefix, typeNames.Of(t),
                        [.. t.GetFields(BindingFlags.Public | BindingFlags.Static).OrderBy(f => f.MetadataToken).Select(f => (f.Name, Convert.ToInt64(f.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture)))],
                        IsExperimental(t), t.Has("System.FlagsAttribute")));
                    continue;
                }

                if (t.IsDelegate())
                {
                    continue;
                }

                if (t.IsGenericTypeDefinition)
                {
                    Skip(t.FullName!, t.FullName!, "open generic type: all members");
                    continue;
                }

                if (t.IsTrait())
                {
                    Skip(t.FullName!, t.FullName!, "trait interface (static abstract members): all members");
                    continue;
                }

                surface.Types.Add(Row(t, prefix));
                functions.AddRange(TypeFunctions(t, prefix));
            }
        }

        functions.AddRange(binder.BindFunctions(classifier, typeNames.Of, set.PrefixOf, (s, why) => Skip(s.Id, s.Engine.FullName + ".Bind", why)));
        AddConstructions(functions);

        // A generic base the first walk passed may cross as a closed construction: each type's base is the nearest that
        // crosses.
        var crossing = new HashSet<Type>(surface.Types.Select(r => r.Type));
        for (int i = 0; i < surface.Types.Count; i++)
        {
            surface.Types[i] = surface.Types[i] with { Base = BaseOf(surface.Types[i].Type, crossing) };
        }

        Name(functions);
        foreach (CFunction f in surface.Functions)
        {
            foreach (CParam p in f.Params.Where(p => p.Kind.Engine != null))
            {
                surface.EnginePairs.AddRange(binder.Pairs(f, p.Kind.Engine!));
            }
        }

        surface.EngineSets.AddRange(binder.Sets);
        surface.EngineParameters.AddRange(binder.Parameters);
        LinkPresets();
        surface.Skips.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        return surface;
    }

    // The closed generic types crossing functions take or return, and the closed generic bases of crossing types that
    // declare a static factory of their own: each crosses as a type of its own, its members too, until no function or
    // type names a new one.
    private void AddConstructions(List<CFunction> functions)
    {
        var seen = new HashSet<Type>(surface.Types.Select(r => r.Type));
        int from = 0;
        while (from < functions.Count)
        {
            int to = functions.Count;
            Type[] found =
            [
                .. functions.Skip(from).Take(to - from).SelectMany(f => f.Params.Select(p => p.Kind).Concat(f.Return is { } r ? [r] : []))
                    .SelectMany(Objects)
                    .Concat(from == 0 ? surface.Types.Select(r => r.Type).SelectMany(FactoryBases) : [])
                    .Where(t => t.IsConstructedGenericType && !t.ContainsGenericParameters && set.Ours(t) && !t.IsInterface && !t.IsDelegate() && !t.IsTrait() && seen.Add(t))
                    .OrderBy(t => t.FullName, StringComparer.Ordinal),
            ];
            from = to;
            foreach (Type t in found.Concat(found.SelectMany(FactoryBases).Where(b => set.Ours(b) && seen.Add(b))).ToList())
            {
                string prefix = set.PrefixOf(t);
                surface.Types.Add(Row(t, prefix));
                functions.AddRange(TypeFunctions(t, prefix));
            }
        }
    }

    // A type's closed generic bases that declare a public static returning themselves: a factory, which crosses under
    // its declaring type alone, so that type crosses too.
    private static IEnumerable<Type> FactoryBases(Type t)
    {
        for (Type? b = t.BaseType; b != null && b.IsConstructedGenericType && !b.ContainsGenericParameters; b = b.BaseType)
        {
            if (b.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Any(m => m.ReturnType == b))
            {
                yield return b;
            }
        }
    }

    // The types of the objects a kind carries: an object itself, a collection's elements, a map's values, a tuple's items.
    private static IEnumerable<Type> Objects(CKind k)
    {
        if (k.Name is "handle" or "boxed" or "boxed?")
        {
            yield return k.Type.NullableOf() ?? k.Type;
        }

        foreach (CKind inner in new[] { k.Element, k.Value }.OfType<CKind>().Concat(k.Items))
        {
            foreach (Type t in Objects(inner))
            {
                yield return t;
            }
        }
    }

    // The nearest base a type crosses with: a public class of ours that isn't generic, or a closed construction that
    // crosses.
    private string? BaseOf(Type t, HashSet<Type> crossing)
    {
        Type? b = t.BaseType;
        while (b != null && set.Ours(b) && !crossing.Contains(b) && (b.IsGenericType || !b.IsPublic))
        {
            b = b.BaseType;
        }

        return b != null && set.Ours(b) ? b.FullName : null;
    }

    private TypeRow Row(Type t, string prefix)
    {
        string shape = t.IsInterface ? "interface" : t.IsValueType ? "struct" : t.IsAbstract && t.IsSealed ? "static" : t.IsAbstract ? "abstract" : t.IsSealed ? "sealed" : "class";
        string form = t == known.Date || t == known.Period ? "value"
            : known.IsWorking(t) ? known.WorkingForm(t)
            : t.IsValueType ? "boxed"
            : t.IsAbstract && t.IsSealed ? "static"
            : "handle";
        return new TypeRow(
            t, prefix, typeNames.Of(t), BaseOf(t, []),
            [.. t.GetInterfaces().Where(i => set.Ours(i) && !i.IsGenericType && !i.IsTrait()).Select(i => i.FullName!).Order(StringComparer.Ordinal)],
            shape, IsExperimental(t), form);
    }

    private IEnumerable<CFunction> TypeFunctions(Type t, string prefix)
    {
        string tc = typeNames.Of(t);
        if (known.IsWorking(t))
        {
            string form = known.WorkingForm(t);
            bool generator = form == "snapshot" && !t.IsAbstract && !t.IsInterface;
            foreach (MemberInfo m in t.GetMembers(Declared).Where(m => m is MethodBase or PropertyInfo or FieldInfo))
            {
                if (generator && m is ConstructorInfo)
                {
                    continue;
                }

                if (m is MethodBase { IsSpecialName: true } and not ConstructorInfo)
                {
                    continue;
                }

                Skip(m.MemberKey(), Display(t, m), form switch
                {
                    "matrix" or "vector" => $"{t.Name} crosses as an array by rows; its own members don't cross",
                    "distribution" => "a loss distribution crosses as its values; its own members don't cross",
                    "snapshot" => "a generator crosses as a snapshot drawn in batches (Draws); its other members don't cross",
                    _ => "member of a working type (a stepping part, kept inside nanoql)",
                });
            }

            if (!generator)
            {
                yield break;
            }

            foreach (ConstructorInfo c in t.GetConstructors().OrderBy(c => c.MetadataToken))
            {
                if (Build(t, prefix, tc, c, "new", "new", c.GetParameters(), t, true, t) is { } f)
                {
                    f.Return = new CKind("snapshot", t);
                    yield return f;
                }
            }

            yield return Draws(t, prefix, tc);
            yield break;
        }

        // A named class, a closed generic type's name, surfaces the members its closed generic bases declare.
        var sources = new List<Type> { t };
        if (!t.IsInterface)
        {
            for (Type? b = t.BaseType; b != null && set.Ours(b) && b.IsGenericType && !b.ContainsGenericParameters; b = b.BaseType)
            {
                sources.Add(b);
            }
        }

        foreach (Type src in sources)
        {
            foreach (MemberInfo m in src.GetMembers(Declared).OrderBy(m => m.MetadataToken))
            {
                // A base's static stays under its declaring type, unless this is a named class and the base is the
                // closing it names: a copy would build another type than its holder's name says.
                if (src != t && IsStatic(m) && !(src == t.BaseType && IsNamedClass(t)))
                {
                    continue;
                }

                foreach (CFunction f in MemberFunctions(t, prefix, tc, src, m))
                {
                    yield return f;
                }
            }
        }

        // A struct's implicit parameterless constructor, default(T), which metadata doesn't hold.
        if (t.IsValueType && !t.IsEnum && t != known.Date && t != known.Period && !presetTypes.Contains(t) && t.GetConstructor(Type.EmptyTypes) == null)
        {
            yield return new CFunction
            {
                Prefix = prefix, TypeName = tc, BaseName = "new", Kind = "new", Type = t, Holder = t, Static = true,
                Key = t.KeyName() + "::.ctor()", Experimental = IsExperimental(t), Deprecated = Deprecation(t), Return = new CKind("boxed", t),
            };
        }
    }

    // A static member: a static method or operator, a property whose accessors are static, or a static or constant field.
    private static bool IsStatic(MemberInfo m) => m switch
    {
        MethodInfo mi => mi.IsStatic,
        PropertyInfo pi => (pi.GetMethod ?? pi.SetMethod)?.IsStatic == true,
        FieldInfo fi => fi.IsStatic,
        _ => false,
    };

    /// <summary>
    /// Whether a type is a named class: a sealed, non-generic class whose direct base is a closed generic construction,
    /// declaring no public instance member but its constructors and those returning itself (a generator's
    /// <c>Clone</c>), as a QuantLib typedef's or a QuantLib-SWIG <c>%template</c>'s name for its closing does.
    /// </summary>
    /// <param name="t">The type.</param>
    /// <returns>Whether it is one.</returns>
    internal static bool IsNamedClass(Type t) =>
        t.IsClass && t.IsSealed && !t.IsGenericType && t.BaseType is { IsGenericType: true, ContainsGenericParameters: false }
        && !t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(m => m is PropertyInfo or FieldInfo || (m is MethodInfo { IsSpecialName: false } mi && mi.ReturnType != t));

    private IEnumerable<CFunction> MemberFunctions(Type t, string prefix, string tc, Type src, MemberInfo m)
    {
        switch (m)
        {
            case ConstructorInfo c when src == t:
                if (t.IsAbstract)
                {
                    yield break;
                }

                if (presetTypes.Contains(t))
                {
                    Skip(c.MemberKey(), Display(t, c), "coded preset constructor: by name through the convention set");
                    yield break;
                }

                List<PropertyInfo> required = c.Has("System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute")
                    ? []
                    : [.. t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.Has("System.Runtime.CompilerServices.RequiredMemberAttribute")).OrderBy(p => p.MetadataToken)];
                ParameterInfo[] ps = [.. c.GetParameters(), .. required.Select(r => r.SetMethod!.GetParameters()[0])];
                if (Build(t, prefix, tc, c, "new", "new", ps, t, true, t, required) is { } ctor)
                {
                    ctor.Return = t == known.Date ? new CKind("date", t) : t == known.Period ? new CKind("period", t) : t.IsValueType ? new CKind("boxed", t) : new CKind("handle", t);
                    yield return ctor;
                }

                yield break;
            case MethodInfo mi when mi.IsSpecialName && mi.Name.StartsWith("op_", StringComparison.Ordinal):
                if (!mi.IsStatic || mi.Name.EndsWith("Assignment", StringComparison.Ordinal))
                {
                    Skip(mi.MemberKey(), Display(t, mi), "compound-assignment or instance operator (mutates)");
                }
                else if (Build(t, prefix, tc, mi, "operator", mi.Name, mi.GetParameters(), mi.ReturnType, true, src) is { } op)
                {
                    yield return op;
                }

                yield break;
            case MethodInfo mi when mi.IsSpecialName && mi.Name.StartsWith("get_", StringComparison.Ordinal) && mi.GetParameters().Length > 0:
                if (Build(t, prefix, tc, mi, "indexer", "get_" + mi.Name[4..], mi.GetParameters(), mi.ReturnType, mi.IsStatic, src) is { } ix)
                {
                    yield return ix;
                }

                yield break;
            case MethodInfo { IsSpecialName: true }:
                yield break;
            case MethodInfo mi:
                if (mi.Name is "Equals" or "GetHashCode" or "ToString" or "<Clone>$" or "Deconstruct" or "PrintMembers" or "CompareTo" && !mi.IsStatic)
                {
                    yield break;
                }

                if (mi.Name.StartsWith('<'))
                {
                    yield break;
                }

                if (t.IsInterface && mi.IsStatic)
                {
                    Skip(mi.MemberKey(), Display(t, mi), "static abstract/virtual interface member (traits)");
                    yield break;
                }

                if (mi.IsGenericMethodDefinition)
                {
                    foreach (CFunction g in Generic(t, prefix, tc, src, mi))
                    {
                        yield return g;
                    }

                    yield break;
                }

                if (Build(t, prefix, tc, mi, mi.IsStatic ? "static" : "method", mi.Name, mi.GetParameters(), mi.ReturnType, mi.IsStatic, src) is { } method)
                {
                    yield return method;
                }

                yield break;
            case PropertyInfo pi:
                if (pi.Name == "EqualityContract" || pi.GetIndexParameters().Length > 0)
                {
                    yield break;
                }

                if (pi.GetMethod is { IsPublic: true } g2)
                {
                    if (t.IsInterface && g2.IsStatic)
                    {
                        Skip(pi.MemberKey(), Display(t, pi), "static abstract/virtual interface member (traits)");
                    }
                    else if (Build(t, prefix, tc, g2, "get", "get_" + pi.Name, [], pi.PropertyType, g2.IsStatic, src) is { } getter)
                    {
                        yield return getter;
                    }
                }

                if (pi.SetMethod is { IsPublic: true } s)
                {
                    bool init = s.ReturnParameter.GetRequiredCustomModifiers().Any(x => x.Name == "IsExternalInit");
                    if (s.IsStatic)
                    {
                        if (Build(t, prefix, tc, s, "set", "set_" + pi.Name, s.GetParameters(), null, true, src) is { } setter)
                        {
                            yield return setter;
                        }
                    }
                    else if (init && t.GetMethod("<Clone>$") != null)
                    {
                        if (Build(t, prefix, tc, pi, "with", "with_" + pi.Name, [s.GetParameters()[0]], t, false, src) is { } with)
                        {
                            yield return with;
                        }
                    }
                    else if (!init)
                    {
                        Skip(pi.MemberKey() + "::set", Display(t, pi), "settable property (mutable state)");
                    }
                }

                yield break;
            case FieldInfo fi:
                if (fi.IsLiteral || fi.IsInitOnly || fi.IsStatic)
                {
                    if (Build(t, prefix, tc, fi, "field", "get_" + fi.Name, [], fi.FieldType, fi.IsStatic, src) is { } field)
                    {
                        yield return field;
                    }
                }
                else
                {
                    Skip(fi.MemberKey(), Display(t, fi), "mutable field");
                }

                yield break;
        }
    }

    // A generic method crosses closed: per named factory where its type argument is a trait's, and at its
    // constraint where its type argument is a calibrated model an engine takes (a helper's trial model).
    private IEnumerable<CFunction> Generic(Type t, string prefix, string tc, Type src, MethodInfo mi)
    {
        Type[] args = mi.GetGenericArguments();
        if (args.Length == 1 && args[0].GetGenericParameterConstraints() is { Length: > 0 } constraints && constraints.Any(c => c.IsTrait()))
        {
            Type[] factories =
            [
                .. set.Assemblies.SelectMany(a => a.Assembly.GetExportedTypes())
                    .Where(f => !f.IsGenericTypeDefinition && !f.IsAbstract && !f.IsInterface && !f.IsNested && constraints.All(c => c.IsAssignableFrom(f)))
                    .Where(f => Satisfies(args[0], f))
                    .OrderBy(f => f.FullName, StringComparer.Ordinal),
            ];
            if (factories.Length == 0)
            {
                Skip(mi.MemberKey(), Display(t, mi), "generic method: no named factory closes it");
                yield break;
            }

            foreach (Type factory in factories)
            {
                MethodInfo closed = mi.MakeGenericMethod(factory);
                if (Build(t, prefix, tc, closed, mi.IsStatic ? "static" : "method", mi.Name, closed.GetParameters(), closed.ReturnType, mi.IsStatic, src, typeArguments: [factory], definition: mi) is { } f)
                {
                    f.SignatureNames.Insert(0, TypeNamer.Simple(factory));
                    yield return f;
                }
            }

            yield break;
        }

        // Closed at its constraint: each type parameter has one class or interface constraint naming no type parameter,
        // and no constructor or value-type constraint, so the constraint itself is a type argument it takes:
        // Calibrate<TModel> where TModel : CalibratedModel takes any calibrated model.
        const GenericParameterAttributes special = GenericParameterAttributes.DefaultConstructorConstraint | GenericParameterAttributes.NotNullableValueTypeConstraint;
        bool closable = args.All(a => a.GetGenericParameterConstraints() is [var c] && !c.IsTrait() && !c.ContainsGenericParameters
            && (c.IsClass || c.IsInterface) && (a.GenericParameterAttributes & special) == 0);
        if (closable)
        {
            Type[] at = [.. args.Select(a => a.GetGenericParameterConstraints()[0])];
            MethodInfo closed = mi.MakeGenericMethod(at);
            if (Build(t, prefix, tc, closed, mi.IsStatic ? "static" : "method", mi.Name, closed.GetParameters(), closed.ReturnType, mi.IsStatic, src, typeArguments: at, definition: mi) is { } f)
            {
                yield return f;
            }

            yield break;
        }

        Skip(mi.MemberKey(), Display(t, mi), "generic method");
    }

    private static bool Satisfies(Type parameter, Type factory)
    {
        GenericParameterAttributes a = parameter.GenericParameterAttributes;
        if ((a & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0 && !factory.IsValueType)
        {
            return false;
        }

        if ((a & GenericParameterAttributes.ReferenceTypeConstraint) != 0 && factory.IsValueType)
        {
            return false;
        }

        return (a & GenericParameterAttributes.DefaultConstructorConstraint) == 0 || factory.IsValueType || factory.GetConstructor(Type.EmptyTypes) != null;
    }

    // One function, or null with the member listed and why, when a parameter or the result doesn't cross.
    private CFunction? Build(
        Type t, string prefix, string tc, MemberInfo m, string kind, string baseName, ParameterInfo[] ps, Type? ret, bool isStatic, Type holder,
        List<PropertyInfo>? required = null, Type[]? typeArguments = null, MethodInfo? definition = null)
    {
        var f = new CFunction
        {
            Prefix = prefix, TypeName = tc, BaseName = baseName, Kind = kind, Type = t, Holder = holder, Member = m, Static = isStatic,
            TypeArguments = typeArguments ?? [],
            Key = (definition ?? m).MemberKey() + (typeArguments is { Length: > 0 } ? "<" + string.Join(",", typeArguments.Select(a => a.KeyName())) + ">" : string.Empty),
            Experimental = IsExperimental(m),

            // a static a named class carries from its closing takes the class's deprecation where it has none
            Deprecated = Deprecation(m) ?? (isStatic && holder != t ? Deprecation(t) : null),

            // read here, while the metadata is open: the back ends run after it is closed
            GeneratedEquality = kind == "operator" && m.Name is "op_Equality" or "op_Inequality" && m.Has("System.Runtime.CompilerServices.CompilerGeneratedAttribute"),
        };
        if (!isStatic && kind != "new")
        {
            CKind self = t == known.Date ? new CKind("date", t) : t == known.Period ? new CKind("period", t) : t.IsValueType ? new CKind("boxed", t) : new CKind("handle", t);
            f.Params.Add(new CParam("self", self, false, null) { IsSelf = true });
        }

        ParameterInfo[] defs = definition?.GetParameters() ?? [];
        int nCtor = ps.Length - (required?.Count ?? 0);
        for (int i = 0; i < ps.Length; i++)
        {
            ParameterInfo p = ps[i];
            Type? defType = i < defs.Length ? defs[i].ParameterType : null;
            CKind? k = classifier.Classify(p.ParameterType, p, false, d => binder.Shape(d, defType), out string? why);
            if (k == null && IsNullByDefault(p))
            {
                // An optional parameter of a type that doesn't cross, null by default, is left at its default:
                // Date.TodaysDate's clock, an optimizer's line search, the caller's functions.
                f.Dropped.Add(i);
                continue;
            }

            if (k == null)
            {
                Skip(f.Key, Display(t, m), why!);
                return null;
            }

            string name = required != null && i >= nCtor ? char.ToLowerInvariant(required[i - nCtor].Name[0]) + required[i - nCtor].Name[1..] : p.Name ?? "arg" + i;
            f.Params.Add(new CParam(name, k, p.HasDefaultValue || p.IsOptional, p.HasDefaultValue ? p.RawDefaultValue : null));
            f.SignatureNames.Add(OverloadNamer.SigName(p.ParameterType));
        }

        if (ret != null && ret.FullName != "System.Void")
        {
            ParameterInfo? rp = (m as MethodInfo)?.ReturnParameter;
            CKind? r = classifier.Classify(ret, rp, true, null, out string? why);
            if (r == null)
            {
                Skip(f.Key, Display(t, m), "return: " + why);
                return null;
            }

            f.Return = r;
        }

        if (!f.Experimental && (f.Params.Any(p => MentionsExperimental(p.Kind.Type)) || (f.Return != null && MentionsExperimental(f.Return.Type))))
        {
            f.Experimental = true;
        }

        f.DocumentedCodes = [.. docs.Of(definition ?? m).Distinct().Select(e => (e, Code(e))).Where(x => x.Item2 > 0)];
        return f;
    }

    // An optional parameter whose default is null: of a class, an interface, a delegate or a nullable value type.
    private static bool IsNullByDefault(ParameterInfo p) =>
        p.HasDefaultValue && p.RawDefaultValue == null && (!p.ParameterType.IsValueType || p.ParameterType.NullableOf() != null);

    // A documented exception the C ABI's table doesn't name maps by the member's own documentation.
    private static int Code(string exception) => exception switch
    {
        "System.IndexOutOfRangeException" => 4,
        _ => 0,
    };

    private CFunction Draws(Type t, string prefix, string tc)
    {
        bool sequence = known.Rsg.IsAssignableFrom(t);
        var f = new CFunction
        {
            Prefix = prefix, TypeName = tc, BaseName = "Draws", Kind = "draws", Type = t, Holder = t, Static = false,
            Key = t.KeyName() + "::Draws", Experimental = IsExperimental(t), Deprecated = Deprecation(t),
            Return = new CKind(sequence ? "sequences" : "draws", t),
            SignatureNames = ["int"],
        };
        f.Params.Add(new CParam("self", new CKind("snapshot", t), false, null) { IsSelf = true });
        f.Params.Add(new CParam("count", new CKind("int32", set.SystemType("System.Int32")), false, null));
        return f;
    }

    private void Name(List<CFunction> functions)
    {
        var used = new HashSet<string>(shipped.FunctionNames, StringComparer.Ordinal);
        foreach (IGrouping<(string, string, string), CFunction> group in functions
                     .GroupBy(f => (f.Prefix, f.TypeName, f.BaseName))
                     .OrderBy(g => g.Key.Item1 + "_" + g.Key.Item2 + "_" + g.Key.Item3, StringComparer.Ordinal))
        {
            surface.Functions.AddRange(OverloadNamer.Name(
                [.. group], shipped, used, (dropped, kept) => Skip(dropped.Key, Display(dropped.Type, dropped.Member) + " (" + string.Join(", ", dropped.SignatureNames) + ")",
                    "overload with the same C signature as another: " + string.Join(", ", kept.SignatureNames))));
        }

        surface.Functions.Sort((a, b) => string.CompareOrdinal(a.CName, b.CName));
        if (surface.Functions.GroupBy(f => f.CName).FirstOrDefault(g => g.Count() > 1) is { } dup)
        {
            throw new BindException($"two functions are named {dup.Key}");
        }
    }

    // Each withheld preset constructor's lookup: the convention set's function taking the preset's name and the
    // lambda's parameters the construction passes, in the lambda's order (a parameter that only picks the branch, a
    // swap index's exogenous discounting, isn't one), and returning a type the preset is (BMAIndex is no IborIndex).
    private void LinkPresets()
    {
        if (known.ConventionSet == null)
        {
            return;
        }

        var lookups = surface.Functions.Where(f => f.Type == known.ConventionSet && f.Kind == "method" && f.Member is MethodInfo).ToList();
        foreach (PresetFactory p in presets)
        {
            ParameterInfo[] lambdaParams = p.Lambda.GetParameters();
            foreach (PresetConstruction c in p.Constructors.Where(c => c.Constructor.DeclaringType == p.PresetType))
            {
                Type[] passed = [.. c.PassedParameters().Select(j => lambdaParams[j].ParameterType)];
                CFunction? lookup = lookups.FirstOrDefault(f =>
                {
                    ParameterInfo[] ps = ((MethodInfo)f.Member!).GetParameters();
                    return ps.Length == passed.Length + 1 && ps[0].ParameterType.FullName == "System.String"
                        && ps.Skip(1).Select(x => x.ParameterType).SequenceEqual(passed)
                        && ((MethodInfo)f.Member!).ReturnType.IsAssignableFrom(p.PresetType);
                });
                surface.Presets.Add(new PresetRow(p, c, lookup));
            }
        }
    }

    private bool IsExperimental(MemberInfo m) =>
        m.Has(WellKnown.ExperimentalAttribute) || (m.DeclaringType != null && IsExperimental(m.DeclaringType)) || (m is Type t && InheritsExperimental(t));

    // A type over an experimental base or interface is experimental too, as a member whose signature names one is: a
    // user's class over one of nanoql's experimental types.
    private bool InheritsExperimental(Type t)
    {
        if (!inheritsExperimental.TryGetValue(t, out bool e))
        {
            static Type Definition(Type x) => x.IsConstructedGenericType ? x.GetGenericTypeDefinition() : x;
            e = false;
            for (Type? b = t.BaseType; b != null && set.Ours(b) && !e; b = b.BaseType)
            {
                e = Definition(b).Has(WellKnown.ExperimentalAttribute);
            }

            e = e || t.GetInterfaces().Any(i => set.Ours(i) && Definition(i).Has(WellKnown.ExperimentalAttribute));
            inheritsExperimental[t] = e;
        }

        return e;
    }

    private static string? Deprecation(MemberInfo m)
    {
        // The compiler marks a constructor of a type with required members obsolete for compilers that predate them,
        // beside its CompilerFeatureRequired: that isn't nanoql's deprecation.
        CustomAttributeData? a = m.Has("System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute") ? null : m.Attribute(WellKnown.ObsoleteAttribute);
        a ??= m.DeclaringType?.Attribute(WellKnown.ObsoleteAttribute);
        return a == null ? null : a.ConstructorArguments.Count > 0 ? (string?)a.ConstructorArguments[0].Value ?? string.Empty : string.Empty;
    }

    private bool MentionsExperimental(Type t)
    {
        if (t.IsByRef || t.IsArray)
        {
            return MentionsExperimental(t.GetElementType()!);
        }

        if (t.IsGenericType && t.GetGenericArguments().Any(MentionsExperimental))
        {
            return true;
        }

        // Experimental by its own attribute, its declaring type's, or an experimental base or interface's.
        return set.Ours(t) && IsExperimental(t.IsConstructedGenericType ? t.GetGenericTypeDefinition() : t);
    }

    private static string Display(Type t, MemberInfo? m) => TypeNamer.Simple(t) + "." + (m is ConstructorInfo ? "new" : m?.Name ?? string.Empty);

    private void Skip(string key, string display, string reason) => surface.Skips.Add(new SkipRow(key, display, reason));
}
