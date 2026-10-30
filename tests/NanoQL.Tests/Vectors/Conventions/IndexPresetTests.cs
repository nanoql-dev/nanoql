// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Tests.Extensions;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The shipped index presets against QuantLib's coded ones: each fixed Ibor, overnight, daily-tenor Libor and BMA
/// preset, swap index family, inflation index and region, resolved through <see cref="ConventionSet.QuantLib"/>, is of
/// its coded class with every field the coded preset's; and so, so that a preset whose class adds behaviour shows,
/// are its fixing, value and maturity dates on every fixing date of 2026 and an overnight index's growth factor and
/// simple accrual on a grid, bit for bit. The families at every tenor are IndexFamiliesVectorTests'.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IndexPresetTests
{
    private static readonly ConventionSet Set = ConventionSet.QuantLib;
    private static readonly Period[] SwapTenors = [new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(5, TimeUnit.Years), new(10, TimeUnit.Years), new(30, TimeUnit.Years)];

    public static TheoryData<string> IborPresets() => [.. CodedPresets.IborIndexes.Keys.Order(StringComparer.Ordinal)];

    public static TheoryData<string> SwapFamilies() => [.. CodedPresets.SwapIndexes.Keys.Order(StringComparer.Ordinal)];

    public static TheoryData<string> DailyTenorLibors() => [.. CodedPresets.DailyTenorLibors.Keys.Order(StringComparer.Ordinal)];

    [Fact]
    public void TheShippedIndexRecordsAreQuantLibsPresets()
    {
        ConventionSnapshot shipped = Set.Layers[0];
        string[] Names<T>() => [.. shipped.Records.OfType<T>().Cast<ConventionRecord>().Select(r => r.Name).Order(StringComparer.Ordinal)];

        Assert.Equal(
            CodedPresets.IborIndexes.Keys.Where(n => !CodedPresets.OvernightIndexes.ContainsKey(n)).Concat(CodedPresets.DailyTenorLibors.Keys).Append("BMAIndex").Order(StringComparer.Ordinal),
            Names<IborIndexRecord>());
        Assert.Equal(CodedPresets.OvernightIndexes.Keys.Order(StringComparer.Ordinal), Names<OvernightIndexRecord>());
        Assert.Equal(CodedPresets.IborFamilies.Keys.Order(StringComparer.Ordinal), Names<IborFamilyRecord>());
        Assert.Equal(CodedPresets.SwapIndexes.Keys.Order(StringComparer.Ordinal), Names<SwapIndexRecord>());
        Assert.Equal(CodedPresets.ZeroInflationIndexes.Keys.Append("AUCPI").Order(StringComparer.Ordinal), Names<ZeroInflationIndexRecord>());
        Assert.Equal(CodedPresets.YoYInflationIndexes.Keys.Append("YYAUCPI").Order(StringComparer.Ordinal), Names<YoYInflationIndexRecord>());
        Assert.Equal(CodedPresets.Regions.Keys.Order(StringComparer.Ordinal), Names<RegionRecord>());
        Assert.Equal((27, 27, 16, 12, 6), (CodedPresets.IborFamilies.Count, CodedPresets.IborIndexes.Count - CodedPresets.OvernightIndexes.Count - 4, CodedPresets.OvernightIndexes.Count, CodedPresets.SwapIndexes.Count, CodedPresets.Regions.Count));
    }

    [Theory]
    [MemberData(nameof(IborPresets))]
    public void AnIborOrOvernightPresetResolvesToItsCodedPreset(string name)
    {
        IborIndex coded = CodedPresets.IborIndexes[name]();
        IborIndex resolved = Set.IborIndex(name);

        Assert.Equal(IndexFields.Of(coded), IndexFields.Of(resolved));
        Assert.Equal(coded.FixingCalendar, resolved.FixingCalendar);
        Assert.Equal(coded.DayCounter, resolved.DayCounter);
        Assert.Equal(IndexFields.Dates(coded, 2026), IndexFields.Dates(resolved, 2026));
        if (coded is OvernightIndex overnight)
        {
            Assert.Equal(IndexFields.Accruals(overnight), IndexFields.Accruals((OvernightIndex)resolved));
        }
    }

    [Theory]
    [MemberData(nameof(DailyTenorLibors))]
    public void ADailyTenorLiborBuiltInTheSetIsItsCodedPreset(string name)
    {
        foreach (int days in new[] { 0, 1, 2 })
        {
            IborIndex coded = CodedPresets.DailyTenorLibors[name](days);
            IborIndex resolved = Under(() => CodedPresets.DailyTenorLibors[name](days));
            Assert.Equal(IndexFields.Of(coded), IndexFields.Of(resolved));
            Assert.Equal(IndexFields.Dates(coded, 2026), IndexFields.Dates(resolved, 2026));
        }
    }

    [Fact]
    public void TheBmaIndexBuiltInTheSetIsItsCodedPreset()
    {
        var resolved = Under(() => new BMAIndex());

        Assert.Equal(IndexFields.Of(new BMAIndex()), IndexFields.Of(resolved));
        Assert.Equal(IndexFields.Dates(new BMAIndex(), 2026), IndexFields.Dates(resolved, 2026));
    }

    [Theory]
    [MemberData(nameof(SwapFamilies))]
    public void ASwapIndexFamilyResolvesToItsCodedPresetAtEveryTenor(string name)
    {
        foreach (Period tenor in SwapTenors)
        {
            SwapIndex coded = CodedPresets.SwapIndexes[name](tenor);
            SwapIndex resolved = Set.SwapIndex(name, tenor);
            Assert.Equal(IndexFields.Of(coded), IndexFields.Of(resolved));
            Assert.Equal(coded.DayCounter, resolved.DayCounter);
            Assert.Equal(IndexFields.Dates(coded, 2026), IndexFields.Dates(resolved, 2026));
        }
    }

    [Fact]
    public void EachInflationIndexAndRegionResolvesToItsCodedPreset()
    {
        foreach ((string name, Func<ZeroInflationIndex> coded) in CodedPresets.ZeroInflationIndexes)
        {
            Assert.Equal(Inflation(coded()), Inflation(Set.ZeroInflationIndex(name)));
        }

        foreach ((string name, Func<YoYInflationIndex> coded) in CodedPresets.YoYInflationIndexes)
        {
            Assert.Equal(Inflation(coded()), Inflation(Set.YoYInflationIndex(name)));
        }

        // the presets taking their frequency and revised flag, built in the set with each
        foreach (Frequency f in new[] { Frequency.Monthly, Frequency.Quarterly })
        {
            foreach (bool revised in new[] { false, true })
            {
                Assert.Equal(Inflation(new AUCPI(f, revised)), Inflation(Under(() => new AUCPI(f, revised))));
                Assert.Equal(Inflation(new YYAUCPI(f, revised)), Inflation(Under(() => new YYAUCPI(f, revised))));
            }
        }

        foreach ((string name, Func<Region> coded) in CodedPresets.Regions)
        {
            Region resolved = Set.Region(name);
            Assert.Equal((coded().GetType(), coded().Name, coded().Code), (resolved.GetType(), resolved.Name, resolved.Code));
        }
    }

    private static string Inflation(InflationIndex i) =>
        $"{i.GetType().Name} | {i.Name} | {i.FamilyName} | {i.Region.GetType().Name} {i.Region.Name} {i.Region.Code} | {i.Revised} | {i.Frequency} | {i.AvailabilityLag} | {i.Currency.Code}"
        + (i is YoYInflationIndex y ? $" | {y.Ratio}" : "");

    private static T Under<T>(Func<T> build)
    {
        using (ConventionProvider.Use(Set))
        {
            return build();
        }
    }
}
