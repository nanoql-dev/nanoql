// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Tests.Extensions;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Batch 7's restructure: with nothing set the eight units, the generic indexes and their region hold QuantLib's data;
/// under a scope a unit's or the region's record reaches the coded constructor; the registry default stays QuantLib's
/// (CVD45); and a provider that changes a unit's code, its identity, is refused.
/// </summary>
public class CommodityProviderTests
{
    // QuantLib's data: petroleumunitsofmeasure.hpp:33-95 and unitofmeasure.hpp:138-145
    public static TheoryData<string, string, string, UnitOfMeasureType, string?> QuantLibUnits() => new()
    {
        { "BarrelUnitOfMeasure", "Barrels", "BBL", UnitOfMeasureType.Volume, null },
        { "MTUnitOfMeasure", "Metric Tonnes", "MT", UnitOfMeasureType.Mass, null },
        { "MBUnitOfMeasure", "1000 Barrels", "MB", UnitOfMeasureType.Volume, "BBL" },
        { "GallonUnitOfMeasure", "US Gallons", "GAL", UnitOfMeasureType.Volume, "BBL" },
        { "LitreUnitOfMeasure", "Litres", "l", UnitOfMeasureType.Volume, "BBL" },
        { "KilolitreUnitOfMeasure", "Kilolitres", "kl", UnitOfMeasureType.Volume, "BBL" },
        { "TokyoKilolitreUnitOfMeasure", "Tokyo Kilolitres", "KL_tk", UnitOfMeasureType.Volume, "BBL" },
        { "LotUnitOfMeasure", "Lot", "Lot", UnitOfMeasureType.Quantity, null },
    };

    private static readonly Dictionary<string, Func<UnitOfMeasure>> Units = new(StringComparer.Ordinal)
    {
        ["BarrelUnitOfMeasure"] = () => new BarrelUnitOfMeasure(),
        ["MTUnitOfMeasure"] = () => new MTUnitOfMeasure(),
        ["MBUnitOfMeasure"] = () => new MBUnitOfMeasure(),
        ["GallonUnitOfMeasure"] = () => new GallonUnitOfMeasure(),
        ["LitreUnitOfMeasure"] = () => new LitreUnitOfMeasure(),
        ["KilolitreUnitOfMeasure"] = () => new KilolitreUnitOfMeasure(),
        ["TokyoKilolitreUnitOfMeasure"] = () => new TokyoKilolitreUnitOfMeasure(),
        ["LotUnitOfMeasure"] = () => new LotUnitOfMeasure(),
    };

    [Theory]
    [MemberData(nameof(QuantLibUnits))]
    public void WithNothingSetEachUnitHoldsQuantLibsData(string preset, string name, string code, UnitOfMeasureType type, string? triangulation)
    {
        UnitOfMeasure unit = Units[preset]();

        Assert.Equal((name, code, type, triangulation), (unit.Name, unit.Code, unit.UnitType, unit.TriangulationUnitOfMeasure?.Code));
    }

    [Fact]
    public void WithNothingSetTheGenericIndexesAndTheirRegionHoldQuantLibsConventions()
    {
        // genericindexes.hpp:33-73: the generic region, and the indexes' arguments as given
        var lag = new Period(2, TimeUnit.Months);
        var cpi = new GenericCPI(Frequency.Quarterly, true, lag, new GBPCurrency());
        var yy = new YYGenericCPI(Frequency.Monthly, false, lag, new EURCurrency());

        Assert.Equal(("Generic", "GENERIC"), (new GenericRegion().Name, new GenericRegion().Code));
        Assert.Equal(("CPI", "Generic", true, Frequency.Quarterly, lag, "GBP"), (cpi.FamilyName, cpi.Region.Name, cpi.Revised, cpi.Frequency, cpi.AvailabilityLag, cpi.Currency.Code));
        Assert.Equal(("YY_CPI", "Generic", false, Frequency.Monthly, lag, "EUR"), (yy.FamilyName, yy.Region.Name, yy.Revised, yy.Frequency, yy.AvailabilityLag, yy.Currency.Code));
        Assert.Throws<ArgumentNullException>(() => new GenericCPI(Frequency.Monthly, false, lag, null!));
    }

    [Fact]
    public void UnderAScopeAUnitsRecordReachesItsConstructorAndTheRegistryDefaultStaysQuantLibs()
    {
        ConventionSet set = ConventionsText.OnQuantLib(
            "<UnitOfMeasure name=\"BarrelUnitOfMeasure\" base=\"BarrelUnitOfMeasure\"><UnitName>Oil barrels</UnitName></UnitOfMeasure>");

        using (ConventionProvider.Use(set))
        {
            Assert.Equal("Oil barrels", new BarrelUnitOfMeasure().Name);
            Assert.Equal("Oil barrels", new GallonUnitOfMeasure().TriangulationUnitOfMeasure!.Name);
            Assert.Equal("Oil barrels", new CommoditySettings().UnitOfMeasure.Name);

            // CVD45: the registry default and the context's settings are built under QuantLib's conventions
            Assert.Equal("Barrels", UnitOfMeasureConversions.Default.Conversions[0].Target.Name);
            Assert.Equal("Barrels", new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) }.CommoditySettings.UnitOfMeasure.Name);
        }

        Assert.Equal("Barrels", new BarrelUnitOfMeasure().Name);
        Assert.Equal("Oil barrels", set.UnitOfMeasureConversions().Conversions[0].Target.Name);
    }

    [Fact]
    public void UnderAScopeTheGenericRegionsRecordReachesTheGenericIndexes()
    {
        ConventionSet set = ConventionsText.OnQuantLib("<Region name=\"GenericRegion\" base=\"GenericRegion\"><Code>GEN</Code></Region>");

        using (ConventionProvider.Use(set))
        {
            Assert.Equal("GEN", new GenericRegion().Code);
            Assert.Equal("GEN", new GenericCPI(Frequency.Monthly, false, new Period(3, TimeUnit.Months), new EURCurrency()).Region.Code);
            Assert.Equal("GEN", new YYGenericCPI(Frequency.Monthly, false, new Period(3, TimeUnit.Months), new EURCurrency()).Region.Code);
        }

        Assert.Equal("GENERIC", new GenericRegion().Code);
    }

    [Fact]
    public void AProviderChangingAUnitsCodeIsRefused()
    {
        using (ConventionProvider.Use(new CodeChanging()))
        {
            Assert.Equal(
                "the data for the BarrelUnitOfMeasure unit of measure changes its code: BBX for QuantLib's BBL",
                Assert.Throws<NanoQLException>(() => new BarrelUnitOfMeasure()).Message);
        }
    }

    // A provider of a user's own that renames every unit's code.
    private sealed class CodeChanging : ConventionProvider
    {
        public override UnitOfMeasureData ResolveUnitOfMeasure(string preset, Func<UnitOfMeasureData> reference) =>
            reference() with { Code = "BBX" };
    }
}
