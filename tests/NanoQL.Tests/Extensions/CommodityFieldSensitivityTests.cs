// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Experimental;
using NanoQL.Extensions.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// The field sensitivity table on the experimental commodity kinds: every field of every kind, given two values,
/// changes what the kind builds, or the set refuses the second where the kind can't take it; and a base chain, the same
/// record on two bases that differ in one field.
/// </summary>
public class CommodityFieldSensitivityTests
{
    /// <summary>The commodity record kinds, which this table holds and FieldSensitivityTests' leaves out.</summary>
    internal static readonly Type[] Kinds =
    [
        typeof(CommodityTypeRecord), typeof(UnitOfMeasureRecord), typeof(UnitOfMeasureConversionRecord), typeof(PaymentTermRecord),
        typeof(CommodityIndexRecord), typeof(CommoditySettingsRecord), typeof(QuantitySettingsRecord),
    ];

    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.October, 2026) };
    private const string Types = "<CommodityType name=\"C1\"><Code>C1</Code></CommodityType><CommodityType name=\"C2\"><Code>C2</Code></CommodityType>";

    // Each case: its element, the record's name and base, the records it names, its build, and its fields in the schema's
    // order, each with two values, the first null for left out, and the set's refusal of the second where it can't take it.
    private static readonly Case[] Cases =
    [
        new Case("CommodityType", "X", null, "", s => s.CommodityType("X"), [
            new Row("TypeName", "Brent", "WTI"),
            new Row("Code", "BRT", "WTI"),
        ]),
        new Case("UnitOfMeasure", "X", null, "", s => s.UnitOfMeasure("X"), [
            new Row("UnitName", "Tonnes", "Kilograms"),
            new Row("Code", "T", "KG"),
            new Row("UnitType", "Mass", "Energy"),
            new Row("TriangulationUnit", null, "BarrelUnitOfMeasure", "a unit of a user's own takes no TriangulationUnit"),
        ]),
        new Case("UnitOfMeasure", "GallonUnitOfMeasure", "GallonUnitOfMeasure", "", s => s.UnitOfMeasure("GallonUnitOfMeasure"), [
            new Row("UnitName", "US Gallons", "Gallons"),
            new Row("Code", "GAL", "GLN", "its code is its identity, QuantLib's GAL, not GLN"),
            new Row("UnitType", "Volume", "Energy"),
            new Row("TriangulationUnit", "BarrelUnitOfMeasure", "MBUnitOfMeasure"),
        ]),
        new Case("UnitOfMeasureConversion", "X", null, Types, s => s.UnitOfMeasureConversions(), [
            new Row("CommodityType", "C1", "C2"),
            new Row("Source", "BarrelUnitOfMeasure", "GallonUnitOfMeasure"),
            new Row("Target", "MTUnitOfMeasure", "LotUnitOfMeasure"),
            new Row("Factor", "0.136", "0.2"),
        ]),
        new Case("PaymentTerm", "X", null, "", s => s.PaymentTerm("X"), [
            new Row("TermName", "T1", "T2"),
            new Row("EventType", "TradeDate", "PricingDate"),
            new Row("OffsetDays", "2", "5"),
            new Row("Calendar", "TARGET", "UnitedStates.NYSE"),
        ]),
        new Case("CommodityIndex", "X", null, Types, s => s.CommodityIndex("X"), [
            new Row("IndexName", "I1", "I2"),
            new Row("CommodityType", "C1", "C2"),
            new Row("Currency", "USD", "EUR"),
            new Row("UnitOfMeasure", "BarrelUnitOfMeasure", "MTUnitOfMeasure"),
            new Row("Calendar", "TARGET", "UnitedStates.NYSE"),
            new Row("LotQuantity", "1000", "500"),
            new Row("NearbyOffset", "0", "1"),
        ]),
        new Case("CommoditySettings", "X", null, "", s => s.CommoditySettings("X"), [
            new Row("Currency", "USD", "EUR"),
            new Row("UnitOfMeasure", "BarrelUnitOfMeasure", "MTUnitOfMeasure"),
        ]),
        new Case("QuantitySettings", "X", null, "", s => s.QuantitySettings("X"), [
            new Row("ConversionType", "NoConversion", "AutomatedConversion"),
            new Row("BaseUnitOfMeasure", "BarrelUnitOfMeasure", "MTUnitOfMeasure"),
        ]),
    ];

    // a base chain on each kind's record of a user's own (a preset's record takes only its own name as its base)
    public static TheoryData<string, string> Rows() =>
        [.. Cases.SelectMany(c => c.Fields.Select(f => (c.Label, f.Field)).Concat(c.Base is null ? [(c.Label, "@base")] : []))];

    [Fact]
    public void TheTableHoldsEveryFieldOfEveryBatch7Kind()
    {
        var expected = new HashSet<(string, string)>();
        foreach (Type kind in Kinds)
        {
            string element = kind.Name[..^"Record".Length];
            expected.Add((element, "@base"));
            foreach (PropertyInfo p in kind.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name is not ("Name" or "Base" or "Foreign" or "EqualityContract")))
            {
                expected.Add((element, p.Name));
            }
        }

        var covered = new HashSet<(string, string)>(Cases.SelectMany(c => c.Fields.Select(f => (c.Element, f.Field)).Append((c.Element, "@base"))));
        Assert.Equal(expected.Order(), covered.Order());
        Assert.Equal(
            Kinds.Order(Comparer<Type>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name))),
            typeof(ConventionRecord).Assembly.GetExportedTypes().Where(t => t.IsSubclassOf(typeof(ConventionRecord)) && t.Namespace == typeof(ConventionRecord).Namespace
                && t.GetCustomAttribute<System.Diagnostics.CodeAnalysis.ExperimentalAttribute>() is not null && !t.Name.EndsWith("ConventionRecord", StringComparison.Ordinal))
                .Order(Comparer<Type>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name))));
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void EachFieldChangesWhatItsKindBuildsOrTheSetRefusesIt(string label, string field)
    {
        Case c = Cases.Single(x => x.Label == label);
        if (field == "@base")
        {
            // the same record on two bases, which differ in the first field
            Row first = c.Fields[0];
            string bases = c.Parts + c.Record("X1", null, c.Fields, null) + c.Record("X2", null, c.Fields, first);
            string a = Outcome(ConventionsText.OnQuantLib(bases + $"<{c.Element} name=\"{c.Name}\" base=\"X1\" />"), c);
            string b = Outcome(ConventionsText.OnQuantLib(bases + $"<{c.Element} name=\"{c.Name}\" base=\"X2\" />"), c);
            Assert.NotEqual(a, b);
            return;
        }

        Row row = c.Fields.Single(f => f.Field == field);
        ConventionSet setA = ConventionsText.OnQuantLib(c.Parts + c.Record(c.Name, c.Base, c.Fields, null));
        if (row.Refused is string refusal)
        {
            Assert.Contains(refusal, Assert.Throws<ArgumentException>(() => ConventionsText.OnQuantLib(c.Parts + c.Record(c.Name, c.Base, c.Fields, row))).Message, StringComparison.Ordinal);
            return;
        }

        ConventionSet setB = ConventionsText.OnQuantLib(c.Parts + c.Record(c.Name, c.Base, c.Fields, row));
        Assert.NotEqual(Outcome(setA, c), Outcome(setB, c));
    }

    // What a case builds, a commodity index's fields spelled out (the fingerprint writes an index as its name).
    private static string Outcome(ConventionSet set, Case c) => Fingerprint.Of(
        c.Build(set) is CommodityIndex i
            ? new object?[] { i.Name, i.CommodityType, i.Currency, i.UnitOfMeasure, i.FixingCalendar, i.LotQuantity, i.NearbyOffset }
            : c.Build(set),
        Context);

    private sealed record Row(string Field, string? A, string B, string? Refused = null);

    private sealed record Case(string Element, string Name, string? Base, string Parts, Func<ConventionSet, object> Build, Row[] Fields)
    {
        public string Label => Name == "X" ? Element : $"{Element} {Name}";

        // The record under a name, every field its first value but one field's second.
        public string Record(string name, string? @base, Row[] fields, Row? changed)
        {
            string body = string.Concat(fields.Select(f => (f == changed ? f.B : f.A) is string v ? $"<{f.Field}>{v}</{f.Field}>" : ""));
            string attributes = @base is null ? $"name=\"{name}\"" : $"name=\"{name}\" base=\"{@base}\"";
            return $"<{Element} {attributes}>{body}</{Element}>";
        }
    }
}
