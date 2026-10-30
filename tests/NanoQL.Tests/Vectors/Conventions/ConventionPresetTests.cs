// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using NanoQL.Extensions.Conventions;
using NanoQL.Tests.Extensions;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The shipped presets against QuantLib's coded ones: each calendar, day counter and currency record of
/// <see cref="ConventionSet.QuantLib"/> resolves to an object equal to the coded preset of its name, under the
/// record's QuantLib name, with the coded preset's holidays, counts or data; each of QuantLib's aliases to the preset it
/// shares an implementation with; and each FpML alias to its target, its code verbatim in its scheme's code list as the
/// alias generator's table records it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public partial class ConventionPresetTests
{
    private static readonly ConventionSnapshot Shipped = ConventionSet.QuantLib.Layers[0];
    private static readonly ConventionSet Set = ConventionSet.QuantLib;

    // Date pairs a day count reads differently: month ends, February, leap days, a year and more.
    private static readonly (Date Start, Date End)[] Periods =
    [
        (new(15, Month.January, 2025), new(15, Month.July, 2025)),
        (new(31, Month.January, 2024), new(29, Month.February, 2024)),
        (new(28, Month.February, 2023), new(31, Month.March, 2023)),
        (new(30, Month.November, 2023), new(31, Month.May, 2024)),
        (new(1, Month.January, 2020), new(1, Month.January, 2025)),
        (new(31, Month.December, 2027), new(29, Month.February, 2028)),
    ];

    public static TheoryData<string> CalendarPresets() => Names<CalendarPresetRecord>();

    public static TheoryData<string> DayCounterPresets() => Names<DayCounterPresetRecord>();

    public static TheoryData<string> CurrencyPresets() => Names<CurrencyPresetRecord>();

    public static TheoryData<string> Aliases() => Names<AliasRecord>();

    [Fact]
    public void TheShippedRecordsAreQuantLibsPresetsAndFpmlsExactCodes()
    {
        Assert.Equal(CodedPresets.Calendars.Keys.Order(StringComparer.Ordinal), Shipped.Records
            .Where(r => r is CalendarPresetRecord || (r is AliasRecord && CodedPresets.Calendars.ContainsKey(r.Name)))
            .Select(r => r.Name).Order(StringComparer.Ordinal));
        Assert.Equal(CodedPresets.DayCounters.Keys.Order(StringComparer.Ordinal), Shipped.Records
            .Where(r => r is DayCounterPresetRecord || (r is AliasRecord && CodedPresets.DayCounters.ContainsKey(r.Name)))
            .Select(r => r.Name).Order(StringComparer.Ordinal));
        Assert.Equal(CodedPresets.Currencies.Keys.Order(StringComparer.Ordinal), Shipped.Records.OfType<CurrencyPresetRecord>().Select(r => r.Name).Order(StringComparer.Ordinal));
        Assert.Equal(111, CodedPresets.Currencies.Count);
        Assert.Equal(FpmlTable.Value.Aliases.Select(a => a.Code).Order(StringComparer.Ordinal), Shipped.Records
            .Select((r, i) => (r, Source: Shipped.Sources[i])).Where(x => x.Source == "aliases.xml").Select(x => x.r.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("day-count-fraction", "30E/360.ISDA")]
    [InlineData("BusinessDayConventionEnum", "NEAREST")]
    public void ACodeWithNoExactMappingHasNoAlias(string scheme, string code)
    {
        // CVR15 as ruled: 30E/360.ISDA keeps the last day of February at the termination date, which no day counter
        // preset knows, and FpML's NEAREST isn't QuantLib's Nearest; each is in its scheme's list and isn't shipped
        Assert.Contains(code, FpmlTable.Value.Schemes[scheme]);
        Assert.DoesNotContain(FpmlTable.Value.Aliases, a => a.Code == code);
        Assert.DoesNotContain(Shipped.Records, r => r.Name == code);
    }

    [Theory]
    [MemberData(nameof(CalendarPresets))]
    public void ACalendarPresetResolvesToItsCodedPreset(string name)
    {
        CalendarPresetRecord record = Record<CalendarPresetRecord>(name);
        Calendar coded = CodedPresets.Calendars[name]();
        Calendar resolved = Set.Calendar(name);
        Date from = new(1, Month.January, 2020), to = new(31, Month.December, 2030);

        Assert.IsType(coded.GetType(), resolved);
        Assert.Equal(coded, resolved);
        Assert.Equal(record.QuantLibName, resolved.Name);
        Assert.Empty(resolved.AddedHolidays);
        Assert.Empty(resolved.RemovedHolidays);
        Assert.Equal(coded.HolidayList(from, to, true), resolved.HolidayList(from, to, true));
    }

    [Theory]
    [MemberData(nameof(DayCounterPresets))]
    public void ADayCounterPresetResolvesToItsCodedPreset(string name)
    {
        DayCounterPresetRecord record = Record<DayCounterPresetRecord>(name);
        DayCounter coded = CodedPresets.DayCounters[name]();
        DayCounter resolved = Set.DayCounter(name);

        Assert.IsType(coded.GetType(), resolved);
        Assert.Equal(coded, resolved);
        Assert.Equal(record.QuantLibName, resolved.Name);
        foreach ((Date start, Date end) in Periods)
        {
            // a year fraction's outcome, its refusal included (the Canadian convention needs a reference period)
            Assert.Equal(coded.DayCount(start, end), resolved.DayCount(start, end));
            Assert.Equal(Outcome(() => coded.YearFraction(start, end)), Outcome(() => resolved.YearFraction(start, end)));
            Assert.Equal(Outcome(() => coded.YearFraction(start, end, start, end)), Outcome(() => resolved.YearFraction(start, end, start, end)));
        }
    }

    [Theory]
    [MemberData(nameof(CurrencyPresets))]
    public void ACurrencyPresetResolvesToItsCodedPreset(string code)
    {
        CurrencyPresetRecord record = Record<CurrencyPresetRecord>(code);
        Currency coded = CodedPresets.Currencies[code]();
        Currency resolved = Set.Currency(code);

        Assert.IsType(coded.GetType(), resolved);
        Assert.Equal(coded, resolved);
        Assert.Equal(record.QuantLibName, resolved.Name);
        Assert.Equal(
            (coded.NumericCode, coded.Symbol, coded.FractionSymbol, coded.FractionsPerUnit, CurrencyRounding.Of(coded.Rounding), coded.TriangulationCurrency),
            (resolved.NumericCode, resolved.Symbol, resolved.FractionSymbol, resolved.FractionsPerUnit, CurrencyRounding.Of(resolved.Rounding), resolved.TriangulationCurrency));
        Assert.Equal(coded.Rounding.Evaluate(1234.56789), resolved.Rounding.Evaluate(1234.56789));
    }

    [Theory]
    [MemberData(nameof(Aliases))]
    public void AnAliasResolvesToItsTarget(string name)
    {
        AliasRecord alias = Record<AliasRecord>(name);
        string target = alias.Base!;
        if (CodedPresets.Calendars.ContainsKey(target))
        {
            Assert.Equal(Set.Calendar(target), Set.Calendar(name));
        }
        else if (CodedPresets.DayCounters.ContainsKey(target))
        {
            Assert.Equal(Set.DayCounter(target), Set.DayCounter(name));
        }
        else
        {
            // a business-day convention's member, which C3's and C4's records read
            Assert.True(Enum.TryParse(target, false, out BusinessDayConvention c) && c.ToString() == target, $"{name}: {target}");
        }

        // QuantLib's own aliases name presets sharing one implementation; FpML's each name its code
        if (FpmlTable.Value.Aliases.Where(a => a.Code == name).ToArray() is [var fpml])
        {
            Assert.Equal(fpml.Name, target);
        }
        else if (CodedPresets.Calendars.TryGetValue(name, out Func<Calendar>? calendar))
        {
            Assert.Equal(CodedPresets.Calendars[target](), calendar());
        }
        else
        {
            Assert.Equal(CodedPresets.DayCounters[target](), CodedPresets.DayCounters[name]());
        }
    }

    [Theory]
    [MemberData(nameof(Aliases))]
    public void AnFpmlAliasCodeIsVerbatimInItsSchemesCodeList(string name)
    {
        if (FpmlTable.Value.Aliases.Where(a => a.Code == name).ToArray() is not [var fpml])
        {
            Assert.True(CodedPresets.Calendars.ContainsKey(name) || CodedPresets.DayCounters.ContainsKey(name), name);
            return;
        }

        Assert.Contains(name, FpmlTable.Value.Schemes[fpml.Scheme]);
    }

    private static string Outcome(Func<double> f)
    {
        try
        {
            return f().ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (ArgumentException e)
        {
            return e.Message;
        }
    }

    private static TheoryData<string> Names<T>()
        where T : ConventionRecord => [.. Shipped.Records.OfType<T>().Select(r => r.Name)];

    private static T Record<T>(string name)
        where T : ConventionRecord => Shipped.Records.OfType<T>().Single(r => r.Name == name);

    // The alias generator's table: each scheme's code list, and each alias's code, scheme and nanoql name.
    private static readonly Lazy<(IReadOnlyDictionary<string, string[]> Schemes, (string Code, string Scheme, string Name)[] Aliases)> FpmlTable = new(() =>
    {
        string text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "conventions", "fpml-aliases.toml"));
        var schemes = SchemeBlock().Matches(text).ToDictionary(
            m => m.Groups["scheme"].Value,
            m => QuotedString().Matches(m.Groups["codes"].Value).Select(c => c.Groups[1].Value).ToArray(),
            StringComparer.Ordinal);
        (string, string, string)[] aliases = [.. AliasBlock().Matches(text).Select(m => (m.Groups["code"].Value, m.Groups["scheme"].Value, m.Groups["name"].Value))];
        return (schemes, aliases);
    });

    [GeneratedRegex(@"^\[schemes\.(?<scheme>[^\]]+)\][^\[]*?^codes = \[(?<codes>[^\]]*)\]", RegexOptions.Multiline)]
    private static partial Regex SchemeBlock();

    [GeneratedRegex("\"([^\"]*)\"")]
    private static partial Regex QuotedString();

    [GeneratedRegex(@"^\[\[alias\]\]\s+code = ""(?<code>[^""]*)""\s+scheme = ""(?<scheme>[^""]*)""\s+name = ""(?<name>[^""]*)""", RegexOptions.Multiline)]
    private static partial Regex AliasBlock();
}
