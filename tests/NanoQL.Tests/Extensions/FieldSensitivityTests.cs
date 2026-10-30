// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Reflection;
using NanoQL.Extensions.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// Field sensitivity (CVD34) on C2's kinds: every field of every kind, built with two values in a setup where it
/// matters, changes what the resolved value does (its holidays, counts or data), or the second value is refused where
/// the field is a preset's identity. The table holds a row for every field the kinds have.
/// </summary>
public class FieldSensitivityTests
{
    private const string TargetAndUk = "<Member>TARGET</Member><Member>UnitedKingdom.Settlement</Member>";

    // kind, field, the layer below, record A, record B, what is looked up (kind:name), and whether B is refused
    private static readonly (string Kind, string Field, string Lower, string A, string B, string Lookup, bool Refused)[] Rows =
    [
        ("CalendarPreset", "QuantLibName", "", "<CalendarPreset name=\"TARGET\"><QuantLibName>TARGET</QuantLibName></CalendarPreset>",
            "<CalendarPreset name=\"TARGET\"><QuantLibName>Target</QuantLibName></CalendarPreset>", "calendar:TARGET", true),
        ("CalendarPreset", "AddedHoliday", "", "<CalendarPreset name=\"TARGET\" base=\"TARGET\" />",
            "<CalendarPreset name=\"TARGET\" base=\"TARGET\"><AddedHoliday>2026-10-16</AddedHoliday></CalendarPreset>", "calendar:TARGET", false),
        ("CalendarPreset", "RemovedHoliday", "", "<CalendarPreset name=\"TARGET\" base=\"TARGET\" />",
            "<CalendarPreset name=\"TARGET\" base=\"TARGET\"><RemovedHoliday>2026-05-01</RemovedHoliday></CalendarPreset>", "calendar:TARGET", false),
        ("CalendarPreset", "@base", "<CalendarPreset name=\"TARGET\" base=\"TARGET\"><AddedHoliday>2026-10-16</AddedHoliday></CalendarPreset>",
            "<CalendarPreset name=\"TARGET\" base=\"TARGET\" />", "<CalendarPreset name=\"TARGET\"><QuantLibName>TARGET</QuantLibName></CalendarPreset>", "calendar:TARGET", false),
        ("Calendar", "@base", "", "<Calendar name=\"X\" base=\"TARGET\" />", "<Calendar name=\"X\" base=\"UnitedKingdom.Settlement\" />", "calendar:X", false),
        ("Calendar", "AddedHoliday", "", "<Calendar name=\"X\" base=\"TARGET\" />",
            "<Calendar name=\"X\" base=\"TARGET\"><AddedHoliday>2026-10-16</AddedHoliday></Calendar>", "calendar:X", false),
        ("Calendar", "RemovedHoliday", "", "<Calendar name=\"X\" base=\"TARGET\" />",
            "<Calendar name=\"X\" base=\"TARGET\"><RemovedHoliday>2026-05-01</RemovedHoliday></Calendar>", "calendar:X", false),
        ("JointCalendar", "Rule", "", $"<JointCalendar name=\"X\"><Rule>JoinHolidays</Rule>{TargetAndUk}</JointCalendar>",
            $"<JointCalendar name=\"X\"><Rule>JoinBusinessDays</Rule>{TargetAndUk}</JointCalendar>", "calendar:X", false),
        ("JointCalendar", "Member", "", $"<JointCalendar name=\"X\">{TargetAndUk}</JointCalendar>",
            "<JointCalendar name=\"X\"><Member>TARGET</Member><Member>UnitedStates.NYSE</Member></JointCalendar>", "calendar:X", false),
        ("JointCalendar", "@base", $"<JointCalendar name=\"J1\">{TargetAndUk}</JointCalendar><JointCalendar name=\"J2\"><Member>TARGET</Member><Member>UnitedStates.NYSE</Member></JointCalendar>",
            "<JointCalendar name=\"X\" base=\"J1\" />", "<JointCalendar name=\"X\" base=\"J2\" />", "calendar:X", false),
        ("BespokeCalendar", "Weekend", "", "<BespokeCalendar name=\"X\"><Weekend>Saturday</Weekend><Weekend>Sunday</Weekend></BespokeCalendar>",
            "<BespokeCalendar name=\"X\"><Weekend>Friday</Weekend></BespokeCalendar>", "calendar:X", false),
        ("BespokeCalendar", "Holiday", "", "<BespokeCalendar name=\"X\" />",
            "<BespokeCalendar name=\"X\"><Holiday>2026-10-16</Holiday></BespokeCalendar>", "calendar:X", false),
        ("BespokeCalendar", "RemovedHoliday", "", "<BespokeCalendar name=\"X\"><Weekend>Sunday</Weekend></BespokeCalendar>",
            "<BespokeCalendar name=\"X\"><Weekend>Sunday</Weekend><RemovedHoliday>2026-10-18</RemovedHoliday></BespokeCalendar>", "calendar:X", false),
        ("BespokeCalendar", "@base", "<BespokeCalendar name=\"B1\"><Weekend>Friday</Weekend></BespokeCalendar><BespokeCalendar name=\"B2\"><Weekend>Sunday</Weekend></BespokeCalendar>",
            "<BespokeCalendar name=\"X\" base=\"B1\" />", "<BespokeCalendar name=\"X\" base=\"B2\" />", "calendar:X", false),
        ("DayCounterPreset", "QuantLibName", "", "<DayCounterPreset name=\"Actual360\"><QuantLibName>Actual/360</QuantLibName></DayCounterPreset>",
            "<DayCounterPreset name=\"Actual360\"><QuantLibName>ACT/360</QuantLibName></DayCounterPreset>", "daycounter:Actual360", true),
        ("DayCounterPreset", "Calendar", "", "<DayCounterPreset name=\"Business252\" base=\"Business252\"><Calendar>TARGET</Calendar></DayCounterPreset>",
            "<DayCounterPreset name=\"Business252\" base=\"Business252\"><Calendar>UnitedKingdom.Settlement</Calendar></DayCounterPreset>", "daycounter:Business252", false),
        ("DayCounterPreset", "@base", "<DayCounterPreset name=\"Business252\" base=\"Business252\"><Calendar>TARGET</Calendar></DayCounterPreset>",
            "<DayCounterPreset name=\"Business252\" base=\"Business252\" />", "<DayCounterPreset name=\"Business252\"><QuantLibName>Business/252(Brazil)</QuantLibName></DayCounterPreset>", "daycounter:Business252", false),
        ("DayCounter", "@base", "", "<DayCounter name=\"X\" base=\"Actual360\" />", "<DayCounter name=\"X\" base=\"Actual365Fixed\" />", "daycounter:X", false),
        ("DayCounter", "Calendar", "", "<DayCounter name=\"X\" base=\"Business252\"><Calendar>TARGET</Calendar></DayCounter>",
            "<DayCounter name=\"X\" base=\"Business252\"><Calendar>UnitedKingdom.Settlement</Calendar></DayCounter>", "daycounter:X", false),
        ("CurrencyPreset", "QuantLibName", "", Currency(""), Currency("<QuantLibName>Euro</QuantLibName>"), "currency:EUR", true),
        ("CurrencyPreset", "NumericCode", "", Currency("<NumericCode>978</NumericCode>"), Currency("<NumericCode>979</NumericCode>"), "currency:EUR", true),
        ("CurrencyPreset", "Symbol", "", Currency(""), Currency("<Symbol>€</Symbol>"), "currency:EUR", false),
        ("CurrencyPreset", "FractionSymbol", "", Currency(""), Currency("<FractionSymbol>ct</FractionSymbol>"), "currency:EUR", false),
        ("CurrencyPreset", "FractionsPerUnit", "", Currency(""), Currency("<FractionsPerUnit>1000</FractionsPerUnit>"), "currency:EUR", false),
        ("CurrencyPreset", "Rounding", "", Currency(""), Currency("<Rounding><Type>Up</Type><Precision>2</Precision><Digit>5</Digit></Rounding>"), "currency:EUR", false),
        ("Rounding", "Type", "", Currency("<Rounding><Type>Closest</Type><Precision>2</Precision><Digit>5</Digit></Rounding>"),
            Currency("<Rounding><Type>Down</Type><Precision>2</Precision><Digit>5</Digit></Rounding>"), "currency:EUR", false),
        ("Rounding", "Precision", "", Currency("<Rounding><Type>Closest</Type><Precision>2</Precision><Digit>5</Digit></Rounding>"),
            Currency("<Rounding><Type>Closest</Type><Precision>4</Precision><Digit>5</Digit></Rounding>"), "currency:EUR", false),
        ("Rounding", "Digit", "", Currency("<Rounding><Type>Closest</Type><Precision>2</Precision><Digit>5</Digit></Rounding>"),
            Currency("<Rounding><Type>Closest</Type><Precision>2</Precision><Digit>9</Digit></Rounding>"), "currency:EUR", false),
        ("CurrencyPreset", "TriangulationCurrency", "", Currency(""), Currency("<TriangulationCurrency>USD</TriangulationCurrency>"), "currency:EUR", false),
        ("CurrencyPreset", "@base", Currency("<Symbol>€</Symbol>"), Currency(""),
            "<CurrencyPreset name=\"EUR\"><QuantLibName>European Euro</QuantLibName><NumericCode>978</NumericCode><Symbol></Symbol><FractionSymbol></FractionSymbol><FractionsPerUnit>100</FractionsPerUnit><Rounding><Type>Closest</Type><Precision>2</Precision><Digit>5</Digit></Rounding></CurrencyPreset>",
            "currency:EUR", false),
        ("Alias", "@base", "", "<Alias name=\"X\" base=\"TARGET\" />", "<Alias name=\"X\" base=\"UnitedKingdom.Settlement\" />", "calendar:X", false),
    ];

    public static TheoryData<string, string> Fields() => [.. Rows.Select(r => (r.Kind, r.Field))];

    [Fact]
    public void TheTableHoldsEveryFieldOfEveryKind()
    {
        var expected = new List<(string, string)>();
        foreach (Type kind in typeof(ConventionRecord).Assembly.GetExportedTypes().Where(t => t.IsSubclassOf(typeof(ConventionRecord))))
        {
            string element = kind.Name[..^"Record".Length];
            expected.AddRange(Properties(kind).Select(p => (element, p)));
        }

        expected.AddRange(Properties(typeof(CurrencyRounding)).Select(p => ("Rounding", p)));

        Assert.Equal(expected.Order(), Rows.Select(r => (r.Kind, r.Field)).Order());

        // a record's fields as the schema names them, a list by its item; its name, which is no field, and its foreign
        // content apart
        static IEnumerable<string> Properties(Type kind) => kind.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name is not ("Name" or "Foreign" or "EqualityContract"))
            .Select(p => p.Name switch
            {
                "Base" => "@base",
                _ when p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(ImmutableArray<>) => p.Name[..^1],
                _ => p.Name,
            });
    }

    [Theory]
    [MemberData(nameof(Fields))]
    public void EachFieldChangesWhatItsKindResolvesToOrIsRefused(string kind, string field)
    {
        var row = Rows.Single(r => r.Kind == kind && r.Field == field);
        ConventionSet Set(string records) => ConventionsText.OnQuantLib(row.Lower).With(ConventionsText.Records(records));

        string a = Observe(Set(row.A), row.Lookup);
        if (row.Refused)
        {
            Assert.Throws<ArgumentException>(() => Set(row.B));
            return;
        }

        Assert.NotEqual(a, Observe(Set(row.B), row.Lookup));
    }

    private static string Currency(string fields) => $"<CurrencyPreset name=\"EUR\" base=\"EUR\">{fields}</CurrencyPreset>";

    // What a resolved value does: a calendar's holidays and weekend days over 2026, a day counter's counts over three
    // periods, a currency's data and a rounded amount.
    private static string Observe(ConventionSet set, string lookup)
    {
        string name = lookup[(lookup.IndexOf(':') + 1)..];
        Date from = new(1, Month.January, 2026), to = new(31, Month.December, 2026);
        switch (lookup[..lookup.IndexOf(':')])
        {
            case "calendar":
                Calendar c = set.Calendar(name);
                return $"{c.Name}: {string.Join(",", c.HolidayList(from, to, true))}";
            case "daycounter":
                DayCounter d = set.DayCounter(name);
                return $"{d.Name}: {d.DayCount(from, to)} {d.YearFraction(from, to)} {d.DayCount(new Date(1, Month.October, 2026), new Date(1, Month.November, 2026))}"
                    + $" {d.YearFraction(new Date(31, Month.January, 2026), new Date(28, Month.February, 2026))}";
            default:
                Currency x = set.Currency(name);
                return FormattableString.Invariant(
                    $"{x.Name} {x.NumericCode} {x.Symbol} {x.FractionSymbol} {x.FractionsPerUnit} {x.Rounding.Evaluate(1.23456789)} {x.Rounding.Evaluate(1.2369)} {x.Rounding.Evaluate(1.2341)} {x.TriangulationCurrency?.Code}");
        }
    }
}
