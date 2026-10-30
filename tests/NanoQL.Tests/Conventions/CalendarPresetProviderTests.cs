// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Every calendar preset reads its holiday edits through <see cref="ConventionProvider"/>, by its name, once its rules
/// are set: none with nothing set, and a provider's edits applied as <see cref="Calendar.AddHoliday"/> and
/// <see cref="Calendar.RemoveHoliday"/> apply them. Each preset's QuantLib values with nothing set are checked by its
/// calendar's vector file (G3, unchanged).
/// </summary>
public class CalendarPresetProviderTests
{
    // The calendar presets, by name: each market of each calendar type, or the type itself, NullCalendar apart.
    private static readonly IReadOnlyDictionary<string, Func<Calendar>> Presets = BuildPresets();

    public static TheoryData<string> PresetNames() => [.. Presets.Keys];

    [Fact]
    public void ThePresetsAreTheSeventyEightMarketCalendars()
    {
        Assert.Equal(78, Presets.Count);
        Assert.Equal(51, Presets.Values.Select(f => f().GetType()).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void WithNothingSetAPresetHasNoEdits(string preset)
    {
        Calendar calendar = Presets[preset]();
        Calendar quantLibs;
        using (ConventionProvider.Use(ConventionProvider.QuantLib))
        {
            quantLibs = Presets[preset]();
        }

        Assert.Empty(calendar.AddedHolidays);
        Assert.Empty(calendar.RemovedHolidays);
        Assert.Equal(quantLibs, calendar);
    }

    [Theory]
    [MemberData(nameof(PresetNames))]
    public void APresetCarriesItsProvidersEditsAsAddHolidayAndRemoveHolidayApplyThem(string preset)
    {
        Calendar plain = Presets[preset]();
        Date added = FirstOf(plain, d => plain.IsBusinessDay(d));
        Date removed = FirstOf(plain, d => plain.IsHoliday(d));
        Date businessDayRemoved = FirstOf(plain, d => plain.IsBusinessDay(d) && d > added);
        Date holidayAdded = FirstOf(plain, d => plain.IsHoliday(d) && d > removed);

        // Removing a business day and adding a holiday change nothing, as they don't for AddHoliday and RemoveHoliday.
        var provider = new TestConventions("p")
        {
            Edits = new Dictionary<string, CalendarEdits>
            {
                [ReadName(preset)] = new([added, holidayAdded], [removed, businessDayRemoved]),
            },
        };
        Calendar edited;
        using (ConventionProvider.Use(provider))
        {
            edited = Presets[preset]();
        }

        Calendar expected = plain.AddHoliday(added).AddHoliday(holidayAdded).RemoveHoliday(removed)
            .RemoveHoliday(businessDayRemoved);
        Assert.Equal(expected, edited);
        Assert.Equal([added], edited.AddedHolidays);
        Assert.Equal([removed], edited.RemovedHolidays);
        Assert.True(edited.IsHoliday(added));
        Assert.True(edited.IsBusinessDay(removed));
        Assert.Equal(["calendar:" + ReadName(preset)], provider.Resolved);
    }

    [Fact]
    public void AnEditOfAnotherPresetDoesntReachACalendar()
    {
        Date holiday = new(13, Month.March, 2026);
        var provider = TestConventions.AddingHoliday("p", "UnitedStates.NYSE", holiday);
        using (ConventionProvider.Use(provider))
        {
            Assert.Empty(new UnitedStates(UnitedStatesMarket.Settlement).AddedHolidays);
            Assert.Empty(new TARGET().AddedHolidays);
            Assert.Equal([holiday], new UnitedStates(UnitedStatesMarket.NYSE).AddedHolidays);
        }
    }

    [Fact]
    public void AnIndexPresetsCalendarCarriesItsProvidersEdits()
    {
        // Euribor's own TARGET is built in its constructor, under the provider read then.
        Date holiday = new(12, Month.March, 2026);
        var provider = TestConventions.AddingHoliday("p", "TARGET", holiday);
        Indexes.IborIndex euribor;
        using (ConventionProvider.Use(provider))
        {
            euribor = new Indexes.Euribor(new Period(6, TimeUnit.Months));
        }

        Assert.Equal([holiday], euribor.FixingCalendar.AddedHolidays);
        Assert.False(euribor.IsValidFixingDate(holiday));
        Assert.True(new Indexes.Euribor(new Period(6, TimeUnit.Months)).IsValidFixingDate(holiday));
    }

    [Fact]
    public void QuantLibsSharedImplementationsShareTheirEdits()
    {
        // BEJ, JSX and IDX share QuantLib's one implementation, so each reads BEJ's edits, and an edit of JSX's name
        // reaches none of them.
        Date holiday = new(12, Month.March, 2026);
        var bej = TestConventions.AddingHoliday("bej", "Indonesia.BEJ", holiday);
        var jsx = TestConventions.AddingHoliday("jsx", "Indonesia.JSX", holiday);
        IndonesiaMarket[] markets = [IndonesiaMarket.BEJ, IndonesiaMarket.JSX, IndonesiaMarket.IDX];
        foreach (IndonesiaMarket market in markets)
        {
            using (ConventionProvider.Use(bej))
            {
                Assert.Equal([holiday], new Indonesia(market).AddedHolidays);
            }

            using (ConventionProvider.Use(jsx))
            {
                Assert.Empty(new Indonesia(market).AddedHolidays);
            }
        }

        Assert.All(bej.Resolved, r => Assert.Equal("calendar:Indonesia.BEJ", r));
        Assert.Equal(3, bej.Resolved.Count);
    }

    [Fact]
    public void NullCalendarIsATechnicalPlaceholderAndReadsNoProvider()
    {
        var provider = TestConventions.AddingHoliday("p", "NullCalendar", new Date(12, Month.March, 2026));
        using (ConventionProvider.Use(provider))
        {
            var calendar = new NullCalendar();
            Assert.Empty(calendar.AddedHolidays);
        }

        Assert.Empty(provider.Resolved);
    }

    // The name a preset reads its edits by: its own, or for an alias the shared implementation's first member's.
    private static string ReadName(string preset) => preset.StartsWith("Indonesia.", StringComparison.Ordinal)
        ? "Indonesia.BEJ"
        : preset;

    // The first date of 2026 the predicate holds for, from 12 January (past every New Year holiday).
    private static Date FirstOf(Calendar calendar, Func<Date, bool> predicate)
    {
        for (Date d = new(12, Month.January, 2026); ; ++d)
        {
            if (predicate(d))
            {
                return d;
            }
        }
    }

    private static Dictionary<string, Func<Calendar>> BuildPresets()
    {
        var presets = new Dictionary<string, Func<Calendar>>(StringComparer.Ordinal);
        IEnumerable<Type> types = typeof(Calendar).Assembly.GetTypes()
            .Where(t => t.IsPublic && t.IsSealed && typeof(Calendar).IsAssignableFrom(t))
            .Where(t => t != typeof(NullCalendar) && t != typeof(JointCalendar) && t != typeof(BespokeCalendar))
            .OrderBy(t => t.Name, StringComparer.Ordinal);
        foreach (Type type in types)
        {
            ConstructorInfo ctor = type.GetConstructors().Single();
            ParameterInfo[] parameters = ctor.GetParameters();
            if (parameters.Length == 0)
            {
                presets.Add(type.Name, () => (Calendar)ctor.Invoke(null));
                continue;
            }

            Type market = Assert.Single(parameters).ParameterType;
            foreach (object value in Enum.GetValues(market))
            {
                presets.Add($"{type.Name}.{value}", () => (Calendar)ctor.Invoke([value]));
            }
        }

        return presets;
    }
}
