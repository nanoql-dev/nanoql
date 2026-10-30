// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Reflection.Emit;
using NanoQL.Currencies;
using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// The guards against a bypass (CVD34) on C2's kinds: the no-bypass walk over every kind built in the marker set, and
/// its own check; and two scans of the built assemblies, in place of a text search, which a target-typed <c>new()</c>
/// would pass: the extensions build a preset only through their factory table, and use no reflection to build one
/// (CVD30); nothing in the extensions sets <see cref="ConventionProvider.Default"/>; and only
/// <see cref="DefaultProviderCollection"/>'s tests set it.
/// </summary>
public class ConventionGuardTests
{
    private static readonly ConventionSet Marked = ConventionWalk.Set;

    [Fact]
    public void TheMarkerIsABusinessDayOnEveryCodedPresetAndAHolidayOnEveryOneTheSetGives()
    {
        foreach ((string name, Func<Calendar> preset) in CodedPresets.Calendars.Where(p => p.Key != "NullCalendar"))
        {
            Assert.True(preset().IsBusinessDay(ConventionWalk.Marker), name);
            Assert.True(Marked.Calendar(name).IsHoliday(ConventionWalk.Marker), name);
        }
    }

    [Fact]
    public void TheWalkNamesWhatTheSetDidntGiveAndAllowsTheTechnicalCalendar()
    {
        var mixed = new JointCalendar(Marked.Calendar("TARGET"), new UnitedKingdom());
        object[] held = [mixed, new Business252(), new EURCurrency(), new NullCalendar(), Marked.Currency("USD")];
        IReadOnlyList<string> found = ConventionWalk.Unmarked(held);

        // the joint calendar takes its marked member's holiday, so only its unmarked member is named
        Assert.Equal(
            "Object[][0].calendars[1]: UK settlement | Object[][1].calendar: Brazil | Object[][2]: EUR",
            string.Join(" | ", found.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void EveryKindBuiltInTheMarkerSetReachesOnlyTheSetsCalendarsAndCurrencies()
    {
        ConventionSet set = Marked.With(ConventionsText.Records($"""
            <Calendar name="Desk" base="UnitedKingdom.Settlement"><AddedHoliday>2026-10-16</AddedHoliday></Calendar>
            <JointCalendar name="Both"><Rule>JoinBusinessDays</Rule><Member>TARGET</Member><Member>Desk</Member></JointCalendar>
            <JointCalendar name="Again" base="Both"><Rule>JoinHolidays</Rule></JointCalendar>
            <BespokeCalendar name="Island"><Weekend>Friday</Weekend><Holiday>{ConventionWalk.Marker.Year:D4}-{(int)ConventionWalk.Marker.Month:D2}-{ConventionWalk.Marker.DayOfMonth:D2}</Holiday></BespokeCalendar>
            <DayCounterPreset name="Business252" base="Business252"><Calendar>Desk</Calendar></DayCounterPreset>
            <DayCounter name="Island252" base="Business252"><Calendar>Island</Calendar></DayCounter>
            <CurrencyPreset name="EUR" base="EUR"><TriangulationCurrency>USD</TriangulationCurrency></CurrencyPreset>
            """));
        var built = new List<object>();
        built.AddRange(CodedPresets.Calendars.Keys.Select(set.Calendar));
        string[] records = ["Desk", "Both", "Again", "Island", "EUTA", "USGS"];
        built.AddRange(records.Select(set.Calendar));
        built.AddRange(CodedPresets.DayCounters.Keys.Concat(["Island252", "BUS/252"]).Select(set.DayCounter));
        built.AddRange(CodedPresets.Currencies.Keys.Select(set.Currency));
        using (ConventionProvider.Use(set))
        {
            built.AddRange(CodedPresets.Calendars.Values.Select(f => (object)f()));
            built.AddRange(CodedPresets.DayCounters.Values.Select(f => (object)f()));
            built.AddRange(CodedPresets.Currencies.Values.Select(f => (object)f()));
        }

        Assert.Empty(ConventionWalk.Unmarked(built));
        Assert.Contains(built, b => b is DEMCurrency { TriangulationCurrency.TriangulationCurrency.Code: "USD" });
    }

    [Fact]
    public void EveryIndexKindBuiltInTheMarkerSetReachesOnlyTheSetsCalendarsCurrenciesAndDayCounters()
    {
        // each index, and a swap index's underlying swap, the instrument it builds
        ConventionSet set = ConventionWalk.IndexSet;
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        var built = new List<object>();
        built.AddRange(CodedPresets.IborIndexes.Keys.Select(n => set.IborIndex(n)));
        foreach (string family in CodedPresets.IborFamilies.Keys)
        {
            built.AddRange(new[] { new Period(1, TimeUnit.Months), new Period(6, TimeUnit.Months), new Period(1, TimeUnit.Years) }
                .Select(t => set.IborIndex(family, t)));
        }

        foreach (string swap in CodedPresets.SwapIndexes.Keys)
        {
            foreach (SwapIndex index in new[] { set.SwapIndex(swap, new Period(1, TimeUnit.Years)), set.SwapIndex(swap, new Period(10, TimeUnit.Years)) })
            {
                built.Add(index);
                built.Add(index.UnderlyingSwap(index.FixingCalendar.Adjust(new Date(16, Month.October, 2026)), context));
            }
        }

        built.AddRange(CodedPresets.ZeroInflationIndexes.Keys.Select(n => set.ZeroInflationIndex(n)));
        built.AddRange(CodedPresets.YoYInflationIndexes.Keys.Select(n => set.YoYInflationIndex(n)));
        using (ConventionProvider.Use(set))
        {
            built.AddRange(CodedPresets.DailyTenorLibors.Values.Select(f => (object)f(2)));
            built.Add(new BMAIndex());
            built.Add(new AUCPI(Frequency.Monthly, false));
            built.AddRange(CodedPresets.Regions.Values.Select(f => (object)f()));
        }

        Assert.Empty(ConventionWalk.Unmarked(built, dayCounters: true));
        Assert.NotEmpty(ConventionWalk.Unmarked(new object[] { new Euribor6M() }, dayCounters: true));
    }

    [Fact]
    public void TheExtensionsBuildAPresetOnlyThroughTheirFactoryTableAndNeverByReflection()
    {
        // the records build from their data a joint calendar, a bespoke one and Business252 on a calendar, and a user's
        // own region, indexes and unit of measure as their base types
        ConstructorInfo[] fromData =
        [
            typeof(JointCalendar).GetConstructor([typeof(IEnumerable<Calendar>), typeof(JointCalendarRule)])!,
            typeof(BespokeCalendar).GetConstructor([typeof(string)])!,
            typeof(Business252).GetConstructor([typeof(Calendar)])!,
            .. new[]
            {
                typeof(CustomRegion), typeof(IborIndex), typeof(OvernightIndex), typeof(Libor), typeof(CustomIborIndex), typeof(SwapIndex),
                typeof(OvernightIndexedSwapIndex), typeof(ZeroInflationIndex), typeof(YoYInflationIndex),
                typeof(NanoQL.Experimental.CommodityIndex), typeof(NanoQL.Experimental.UnitOfMeasure),
            }.SelectMany(t => t.GetConstructors()),
        ];
        MethodBase[] reflection =
        [
            .. typeof(Activator).GetMethods().Where(m => m.Name == nameof(Activator.CreateInstance)),
            .. typeof(Type).GetMethods().Where(m => m.Name == nameof(Type.GetType)),
            .. typeof(Assembly).GetMethods().Where(m => m.Name == nameof(Assembly.CreateInstance)),
        ];
        var failures = new List<string>();
        foreach (MethodBase m in Bodies(typeof(ConventionSet).Assembly))
        {
            string owner = IlScanner.Owner(m);
            foreach ((OpCode op, MethodBase member) in IlScanner.References(m))
            {
                bool preset = op == OpCodes.Newobj && member is ConstructorInfo c && IsConvention(c.DeclaringType!) && !fromData.Contains(c);
                if ((preset && !owner.StartsWith("NanoQL.Extensions.Conventions.PresetFactories.", StringComparison.Ordinal))
                    || reflection.Contains(member is MethodInfo { IsGenericMethod: true } g ? g.GetGenericMethodDefinition() : member))
                {
                    failures.Add($"{owner}: {member.DeclaringType!.Name}.{member.Name}");
                }
            }
        }

        Assert.Empty(failures);
        Assert.Contains(Bodies(typeof(ConventionSet).Assembly), m => IlScanner.Owner(m).StartsWith("NanoQL.Extensions.Conventions.PresetFactories.", StringComparison.Ordinal)
            && IlScanner.References(m).Any(r => r.OpCode == OpCodes.Newobj && r.Member.DeclaringType == typeof(TARGET)));

        static bool IsConvention(Type t) => t.Assembly == typeof(Calendar).Assembly
            && (typeof(Calendar).IsAssignableFrom(t) || typeof(DayCounter).IsAssignableFrom(t) || typeof(Currency).IsAssignableFrom(t)
                || typeof(FinancialIndex).IsAssignableFrom(t) || typeof(Region).IsAssignableFrom(t)
                || typeof(NanoQL.Experimental.UnitOfMeasure).IsAssignableFrom(t));
    }

    [Fact]
    public void NothingInTheExtensionsSetsTheDefault()
    {
        // CVD34: no code in src/ sets the default, the extensions, which build sets, among it
        MethodInfo setter = typeof(ConventionProvider).GetProperty(nameof(ConventionProvider.Default))!.SetMethod!;

        Assert.Empty(Bodies(typeof(ConventionSet).Assembly).Where(m => IlScanner.References(m).Any(r => r.Member == setter)).Select(IlScanner.Owner));
    }

    [Fact]
    public void OnlyTheDefaultCollectionsTestsSetTheDefault()
    {
        MethodInfo setter = typeof(ConventionProvider).GetProperty(nameof(ConventionProvider.Default))!.SetMethod!;
        string[] setters = [.. Bodies(typeof(ConventionGuardTests).Assembly)
            .Where(m => IlScanner.References(m).Any(r => r.Member == setter))
            .Select(Outermost).Distinct()
            .Where(t => t.GetCustomAttribute<CollectionAttribute>()?.Name != DefaultProviderCollection.Name)
            .Select(t => t.FullName!)];

        Assert.Empty(setters);
        Assert.Contains(Bodies(typeof(ConventionGuardTests).Assembly), m => Outermost(m) == typeof(ConventionProviderDefaultTests) && IlScanner.References(m).Any(r => r.Member == setter));

        static Type Outermost(MethodBase m)
        {
            Type t = m.DeclaringType!;
            while (t.DeclaringType is not null)
            {
                t = t.DeclaringType;
            }

            return t;
        }
    }

    private static IEnumerable<MethodBase> Bodies(Assembly assembly)
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        return assembly.GetTypes().SelectMany(t => t.GetMethods(All).Cast<MethodBase>().Concat(t.GetConstructors(All)))
            .Where(m => m.GetMethodBody() is not null);
    }
}
