// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// QuantLib's coded presets by the conventions' names, built by the tests themselves, apart from the extensions' own
/// table: each calendar type and market (<c>TARGET</c>, <c>UnitedStates.NYSE</c>), each day counter and convention or
/// flag, and each currency by its code.
/// </summary>
internal static class CodedPresets
{
    /// <summary>Gets the calendar presets, <c>NullCalendar</c> among them.</summary>
    public static IReadOnlyDictionary<string, Func<Calendar>> Calendars { get; } = BuildCalendars();

    /// <summary>Gets the day counter presets.</summary>
    public static IReadOnlyDictionary<string, Func<DayCounter>> DayCounters { get; } = new Dictionary<string, Func<DayCounter>>(StringComparer.Ordinal)
    {
        ["Actual360"] = () => new Actual360(),
        ["Actual360.IncludeLastDay"] = () => new Actual360(true),
        ["Actual364"] = () => new Actual364(),
        ["Actual36525"] = () => new Actual36525(),
        ["Actual36525.IncludeLastDay"] = () => new Actual36525(true),
        ["Actual365Fixed"] = () => new Actual365Fixed(),
        ["Actual365Fixed.Canadian"] = () => new Actual365Fixed(Actual365FixedConvention.Canadian),
        ["Actual365Fixed.NoLeap"] = () => new Actual365Fixed(Actual365FixedConvention.NoLeap),
        ["Actual366"] = () => new Actual366(),
        ["Actual366.IncludeLastDay"] = () => new Actual366(true),
        ["ActualActual.AFB"] = () => new ActualActual(ActualActualConvention.AFB),
        ["ActualActual.Actual365"] = () => new ActualActual(ActualActualConvention.Actual365),
        ["ActualActual.Bond"] = () => new ActualActual(ActualActualConvention.Bond),
        ["ActualActual.Euro"] = () => new ActualActual(ActualActualConvention.Euro),
        ["ActualActual.Historical"] = () => new ActualActual(ActualActualConvention.Historical),
        ["ActualActual.ISDA"] = () => new ActualActual(ActualActualConvention.ISDA),
        ["ActualActual.ISMA"] = () => new ActualActual(ActualActualConvention.ISMA),
        ["Business252"] = () => new Business252(),
        ["OneDayCounter"] = () => new OneDayCounter(),
        ["SimpleDayCounter"] = () => new SimpleDayCounter(),
        ["Thirty360.BondBasis"] = () => new Thirty360(Thirty360Convention.BondBasis),
        ["Thirty360.EurobondBasis"] = () => new Thirty360(Thirty360Convention.EurobondBasis),
        ["Thirty360.European"] = () => new Thirty360(Thirty360Convention.European),
        ["Thirty360.German"] = () => new Thirty360(Thirty360Convention.German),
        ["Thirty360.ISDA"] = () => new Thirty360(Thirty360Convention.ISDA),
        ["Thirty360.ISMA"] = () => new Thirty360(Thirty360Convention.ISMA),
        ["Thirty360.Italian"] = () => new Thirty360(Thirty360Convention.Italian),
        ["Thirty360.NASD"] = () => new Thirty360(Thirty360Convention.NASD),
        ["Thirty360.USA"] = () => new Thirty360(Thirty360Convention.USA),
        ["Thirty365"] = () => new Thirty365(),
    };

    /// <summary>Gets the currency presets, by ISO code.</summary>
    public static IReadOnlyDictionary<string, Func<Currency>> Currencies { get; } = typeof(Currency).Assembly.GetTypes()
        .Where(t => t.IsPublic && t.IsSealed && typeof(Currency).IsAssignableFrom(t))
        .Select(t => (Func<Currency>)(() => (Currency)Activator.CreateInstance(t)!))
        .ToDictionary(f => f().Code, StringComparer.Ordinal);

    /// <summary>Gets the fixed Ibor and overnight presets taking only a forwarding curve, by class name.</summary>
    public static IReadOnlyDictionary<string, Func<IborIndex>> IborIndexes { get; } = Presets<IborIndex>([typeof(YieldTermStructure)]);

    /// <summary>Gets the overnight presets.</summary>
    public static IReadOnlyDictionary<string, Func<IborIndex>> OvernightIndexes { get; } =
        IborIndexes.Where(p => p.Value() is OvernightIndex).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    /// <summary>Gets the Ibor families, each at a tenor.</summary>
    public static IReadOnlyDictionary<string, Func<Period, IborIndex>> IborFamilies { get; } =
        Presets<IborIndex>([typeof(Period), typeof(YieldTermStructure)]).Keys.ToDictionary(
            n => n, n => (Func<Period, IborIndex>)(t => Build<IborIndex>(n, [typeof(Period), typeof(YieldTermStructure)], t, null)), StringComparer.Ordinal);

    /// <summary>Gets the daily-tenor Libors, each at its settlement days.</summary>
    public static IReadOnlyDictionary<string, Func<int, IborIndex>> DailyTenorLibors { get; } =
        Presets<IborIndex>([typeof(int), typeof(YieldTermStructure)]).Keys.ToDictionary(
            n => n, n => (Func<int, IborIndex>)(d => Build<IborIndex>(n, [typeof(int), typeof(YieldTermStructure)], d, null)), StringComparer.Ordinal);

    /// <summary>Gets the swap index families, each at a tenor.</summary>
    public static IReadOnlyDictionary<string, Func<Period, SwapIndex>> SwapIndexes { get; } =
        Presets<SwapIndex>([typeof(Period), typeof(YieldTermStructure)]).Keys.ToDictionary(
            n => n, n => (Func<Period, SwapIndex>)(t => Build<SwapIndex>(n, [typeof(Period), typeof(YieldTermStructure)], t, null)), StringComparer.Ordinal);

    /// <summary>Gets the zero inflation presets taking only a curve.</summary>
    public static IReadOnlyDictionary<string, Func<ZeroInflationIndex>> ZeroInflationIndexes { get; } =
        Presets<ZeroInflationIndex>([typeof(ZeroInflationTermStructure)]);

    /// <summary>Gets the year-on-year inflation presets taking only a curve.</summary>
    public static IReadOnlyDictionary<string, Func<YoYInflationIndex>> YoYInflationIndexes { get; } =
        Presets<YoYInflationIndex>([typeof(YoYInflationTermStructure)]);

    /// <summary>Gets the regions, but batch 7's generic region, which C5's records take.</summary>
    public static IReadOnlyDictionary<string, Func<Region>> Regions { get; } = typeof(Region).Assembly.GetTypes()
        .Where(t => t.IsPublic && t.IsSealed && typeof(Region).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) is not null)
        .Where(t => t.Name != "GenericRegion")
        .ToDictionary(t => t.Name, t => (Func<Region>)(() => (Region)Activator.CreateInstance(t)!), StringComparer.Ordinal);

    // The public non-abstract index classes whose one public constructor's parameters are the types given (the last
    // a curve, null): the presets of one shape.
    private static Dictionary<string, Func<T>> Presets<T>(Type[] parameters)
        where T : class =>
        typeof(T).Assembly.GetTypes()
            .Where(t => t.IsPublic && !t.IsAbstract && typeof(T).IsAssignableFrom(t) && t.Namespace == "NanoQL.Indexes")
            .Where(t => t.GetConstructors().Any(c => c.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters)))
            .Where(t => t.GetConstructors().All(c => c.GetParameters().Length <= parameters.Length + 1))
            .ToDictionary(t => t.Name, t => (Func<T>)(() => Build<T>(t.Name, parameters, [.. parameters.Select(_ => (object?)null)])), StringComparer.Ordinal);

    private static T Build<T>(string name, Type[] parameters, params object?[] args)
        where T : class
    {
        Type type = typeof(T).Assembly.GetTypes().Single(t => t.Name == name && t.Namespace == "NanoQL.Indexes");
        return (T)type.GetConstructor(parameters)!.Invoke(BindingFlags.DoNotWrapExceptions, null, args, null);
    }

    private static Dictionary<string, Func<Calendar>> BuildCalendars()
    {
        var presets = new Dictionary<string, Func<Calendar>>(StringComparer.Ordinal) { ["NullCalendar"] = () => new NullCalendar() };
        IEnumerable<Type> types = typeof(Calendar).Assembly.GetTypes()
            .Where(t => t.IsPublic && t.IsSealed && typeof(Calendar).IsAssignableFrom(t))
            .Where(t => t != typeof(NullCalendar) && t != typeof(JointCalendar) && t != typeof(BespokeCalendar));
        foreach (Type type in types)
        {
            ConstructorInfo ctor = type.GetConstructors().Single();
            if (ctor.GetParameters() is not [{ } market])
            {
                presets.Add(type.Name, () => (Calendar)ctor.Invoke(null));
                continue;
            }

            foreach (object value in Enum.GetValues(market.ParameterType))
            {
                presets.Add($"{type.Name}.{value}", () => (Calendar)ctor.Invoke([value]));
            }
        }

        return presets;
    }
}
