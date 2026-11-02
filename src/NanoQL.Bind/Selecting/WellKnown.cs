// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Bind.Reading;

namespace NanoQL.Bind.Selecting;

/// <summary>The core types the rules name, read from the inputs' metadata, and the working-type tests.</summary>
internal sealed class WellKnown
{
    /// <summary>The working-type mark's full name.</summary>
    public const string WorkingTypeAttribute = "NanoQL.WorkingTypeAttribute";

    /// <summary>The experimental marker's full name (NQL0001).</summary>
    public const string ExperimentalAttribute = "System.Diagnostics.CodeAnalysis.ExperimentalAttribute";

    /// <summary>The deprecation marker's full name.</summary>
    public const string ObsoleteAttribute = "System.ObsoleteAttribute";

    private readonly Dictionary<Type, bool> working = [];
    private readonly Dictionary<Type, Type?> deriving = [];

    /// <summary>Initializes a new instance of the <see cref="WellKnown"/> class.</summary>
    /// <param name="set">The inputs.</param>
    public WellKnown(MetadataSet set)
    {
        Set = set;
        Date = set.CoreType("NanoQL.Time.Date");
        Period = set.CoreType("NanoQL.Time.Period");
        Matrix = set.CoreType("NanoQL.Numerics.Matrix");
        Vector = set.CoreType("NanoQL.Numerics.Vector");
        Distribution = set.CoreType("NanoQL.Experimental.Distribution");
        EvaluationContext = set.CoreType("NanoQL.EvaluationContext");
        InstrumentResults = set.CoreType("NanoQL.InstrumentResults");
        Rng = set.CoreType("NanoQL.Numerics.IRng");
        Rsg = set.CoreType("NanoQL.Numerics.IRsg");
        ConventionProvider = set.CoreType("NanoQL.ConventionProvider");
        ConventionSet = set.Assemblies.Select(a => a.Assembly.GetType("NanoQL.Extensions.Conventions.ConventionSet")).FirstOrDefault(t => t != null);
    }

    /// <summary>Gets the inputs.</summary>
    public MetadataSet Set { get; }

    /// <summary>Gets <c>NanoQL.Time.Date</c>.</summary>
    public Type Date { get; }

    /// <summary>Gets <c>NanoQL.Time.Period</c>.</summary>
    public Type Period { get; }

    /// <summary>Gets <c>NanoQL.Numerics.Matrix</c>.</summary>
    public Type Matrix { get; }

    /// <summary>Gets <c>NanoQL.Numerics.Vector</c>.</summary>
    public Type Vector { get; }

    /// <summary>Gets the loss models' <c>Distribution</c>.</summary>
    public Type Distribution { get; }

    /// <summary>Gets <c>NanoQL.EvaluationContext</c>.</summary>
    public Type EvaluationContext { get; }

    /// <summary>Gets <c>NanoQL.InstrumentResults</c>, every engine's result's base.</summary>
    public Type InstrumentResults { get; }

    /// <summary>Gets the random-number generators' interface.</summary>
    public Type Rng { get; }

    /// <summary>Gets the sequence generators' interface.</summary>
    public Type Rsg { get; }

    /// <summary>Gets the core's convention provider.</summary>
    public Type ConventionProvider { get; }

    /// <summary>Gets the extensions' convention set, when the inputs hold the extensions.</summary>
    public Type? ConventionSet { get; }

    /// <summary>
    /// Whether a type is a working type: it carries the mark, or a base class or an interface it implements does. The
    /// attribute is inherited by classes only, so interfaces are read too, and a user's own generator is one.
    /// </summary>
    /// <param name="t">The type.</param>
    /// <returns>True for a working type.</returns>
    public bool IsWorking(Type t)
    {
        if (working.TryGetValue(t, out bool w))
        {
            return w;
        }

        Type d = t.IsConstructedGenericType ? t.GetGenericTypeDefinition() : t;
        w = false;
        for (Type? b = d; b != null && !w; b = b.BaseType)
        {
            Type bd = b.IsConstructedGenericType ? b.GetGenericTypeDefinition() : b;
            w = bd.Has(WorkingTypeAttribute);
        }

        w = w || d.GetInterfaces().Any(i => (i.IsConstructedGenericType ? i.GetGenericTypeDefinition() : i).Has(WorkingTypeAttribute));
        working[t] = w;
        return w;
    }

    /// <summary>Whether a type is a generator that crosses as a snapshot: it implements the generators' interfaces.</summary>
    /// <param name="t">The type.</param>
    /// <returns>True for a random-number or sequence generator.</returns>
    public bool IsGenerator(Type t) => Rng.IsAssignableFrom(t) || Rsg.IsAssignableFrom(t);

    /// <summary>
    /// A generator class of the inputs that derives a closed generator construction and has a public constructor, so its
    /// snapshot can stand for the construction (B2R6: <c>MersenneTwisterUniformRsg</c> for
    /// <c>RandomSequenceGenerator&lt;MersenneTwisterUniformRng&gt;</c>); null where none does.
    /// </summary>
    /// <param name="t">The closed construction.</param>
    /// <returns>The first such class by name, or null.</returns>
    public Type? DerivingGenerator(Type t)
    {
        if (!deriving.TryGetValue(t, out Type? d))
        {
            d = Set.Assemblies.SelectMany(a => a.Assembly.GetExportedTypes())
                .Where(c => c != t && c.IsClass && !c.IsAbstract && !c.IsNested && !c.ContainsGenericParameters && t.IsAssignableFrom(c)
                    && IsGenerator(c) && c.GetConstructors().Length > 0)
                .OrderBy(c => c.FullName, StringComparer.Ordinal).FirstOrDefault();
            deriving[t] = d;
        }

        return d;
    }

    /// <summary>A working type's form: <c>matrix</c>, <c>vector</c>, <c>snapshot</c>, <c>distribution</c> or <c>withheld</c>.</summary>
    /// <param name="t">A working type.</param>
    /// <returns>Its form.</returns>
    public string WorkingForm(Type t) =>
        t == Matrix ? "matrix" : t == Vector ? "vector" : t == Distribution ? "distribution" : IsGenerator(t) ? "snapshot" : "withheld";
}
