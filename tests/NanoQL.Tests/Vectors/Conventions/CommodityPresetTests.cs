// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Tests.Extensions;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Batch 7's shipped records, <c>commodities.cxx.xml</c>, against the coded classes and QuantLib's defaults: the eight
/// units of measure, the seven known conversion factors with XRR4's two rows as nanoql's <c>Default</c> holds them,
/// the commodity and quantity settings' defaults, and the generic indexes and their region.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CommodityPresetTests
{
    private static readonly ConventionSet Set = ConventionSet.QuantLib;

    // QuantLib's unit presets, petroleumunitsofmeasure.hpp's seven and unitofmeasure.hpp's lot, in its order.
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

    public static TheoryData<string> UnitNames() => [.. Units.Keys];

    [Fact]
    public void TheShippedCommodityRecordsAreQuantLibsPresetsKnownFactorsAndDefaults()
    {
        ConventionRecord[] shipped = [.. Set.Layers[0].Records];

        Assert.Equal(Units.Keys, shipped.OfType<UnitOfMeasureRecord>().Select(r => r.Name));
        Assert.Equal(["MB-BBL", "BBL-GAL", "MB-GAL", "GAL-l", "BBL-l", "kl-BBL", "KL_tk-BBL"], shipped.OfType<UnitOfMeasureConversionRecord>().Select(r => r.Name));
        Assert.Equal(["CommoditySettings"], shipped.OfType<CommoditySettingsRecord>().Select(r => r.Name));
        Assert.Equal(["QuantitySettings"], shipped.OfType<QuantitySettingsRecord>().Select(r => r.Name));

        // QuantLib has no commodity type, payment term or commodity index presets
        Assert.Empty(shipped.OfType<CommodityTypeRecord>());
        Assert.Empty(shipped.OfType<PaymentTermRecord>());
        Assert.Empty(shipped.OfType<CommodityIndexRecord>());
    }

    [Theory]
    [MemberData(nameof(UnitNames))]
    public void EachUnitBuiltInTheSetIsItsCodedClassWithItsData(string name)
    {
        UnitOfMeasure coded = Units[name]();
        UnitOfMeasure built = Set.UnitOfMeasure(name);

        Assert.IsType(coded.GetType(), built);
        Assert.Equal(Data(coded), Data(built));
        Assert.Equal(coded.TriangulationUnitOfMeasure?.GetType(), built.TriangulationUnitOfMeasure?.GetType());
    }

    [Fact]
    public void TheSetsConversionsAreDefaultsKnownFactorsXrr4sTwoRowsTheRightWayRound()
    {
        UnitOfMeasureConversions built = Set.UnitOfMeasureConversions();

        // S-xrt's equality: the same conversions in the same order
        Assert.Equal(UnitOfMeasureConversions.Default, built);
        Assert.Equal(
            [("MB", "BBL", 1000.0), ("BBL", "GAL", 42.0), ("MB", "GAL", 42000.0), ("GAL", "l", 3.78541), ("BBL", "l", 158.987),
                ("kl", "BBL", 6.28981), ("KL_tk", "BBL", 6.28981)],
            built.Conversions.Select(c => (c.Source.Code, c.Target.Code, c.ConversionFactor)));
        Assert.All(built.Conversions, c => Assert.IsType<NullCommodityType>(c.CommodityType));

        // XRR4: QuantLib stores (LitreUnitOfMeasure, GallonUnitOfMeasure, 3.78541) and (GallonUnitOfMeasure,
        // MBUnitOfMeasure, 1000 * 42) (unitofmeasureconversionmanager.cpp:115-122), so its direct lookup converts 1,000
        // litres into 3,785.41 gallons and 1,000 thousand-barrels into 2.381e-2 gallons; nanoql's, 264.17 and 42,000,000
        var none = new NullCommodityType();
        double Gallons(UnitOfMeasure from) =>
            built.Lookup(none, from, new GallonUnitOfMeasure(), UnitOfMeasureConversionType.Direct).Convert(new Quantity(none, from, 1000)).Amount;
        Assert.Equal(1000 / 3.78541, Gallons(new LitreUnitOfMeasure()), 1e-12);
        Assert.Equal(42e6, Gallons(new MBUnitOfMeasure()), 1e-6);
        Assert.NotEqual(1000 * 3.78541, Gallons(new LitreUnitOfMeasure()), 1e-6);
        Assert.NotEqual(1000 / 42000.0, Gallons(new MBUnitOfMeasure()), 1e-6);
    }

    [Fact]
    public void TheSettingsDefaultRecordsBuildEvaluationContextsDefaults()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        CommoditySettings commodity = Set.CommoditySettings();
        QuantitySettings quantity = Set.QuantitySettings();

        Assert.Equal(context.CommoditySettings, commodity);
        Assert.Equal(context.QuantitySettings, quantity);

        // commoditysettings.cpp:28, USD and barrels; quantity.cpp:27-29, no conversion and QuantLib's empty unit
        Assert.Equal(("USD", "BBL", "Barrels"), (commodity.Currency.Code, commodity.UnitOfMeasure.Code, commodity.UnitOfMeasure.Name));
        Assert.IsType<USDCurrency>(commodity.Currency);
        Assert.IsType<BarrelUnitOfMeasure>(commodity.UnitOfMeasure);
        Assert.Equal((QuantityConversionType.NoConversion, (UnitOfMeasure?)null), (quantity.ConversionType, quantity.BaseUnitOfMeasure));
    }

    [Theory]
    [InlineData(Frequency.Monthly, false, 3, "EUR")]
    [InlineData(Frequency.Quarterly, true, 2, "GBP")]
    public void TheGenericIndexesFromARecordGivingTheirArgumentsAreTheirCodedClasses(Frequency frequency, bool revised, int lag, string currency)
    {
        string fields = $"<Revised>{(revised ? "true" : "false")}</Revised><Frequency>{frequency}</Frequency><AvailabilityLag>{lag}M</AvailabilityLag><Currency>{currency}</Currency>";
        ConventionSet set = ConventionsText.OnQuantLib(
            $"<ZeroInflationIndex name=\"G\" base=\"GenericCPI\">{fields}</ZeroInflationIndex><YoYInflationIndex name=\"YG\" base=\"YYGenericCPI\">{fields}</YoYInflationIndex>");
        Currency ccy = currency == "EUR" ? new EURCurrency() : new GBPCurrency();
        var period = new Period(lag, TimeUnit.Months);

        ZeroInflationIndex zero = set.ZeroInflationIndex("G");
        YoYInflationIndex yoy = set.YoYInflationIndex("YG");

        Assert.IsType<GenericCPI>(zero);
        Assert.IsType<YYGenericCPI>(yoy);
        Assert.Equal(Data(new GenericCPI(frequency, revised, period, ccy)), Data(zero));
        Assert.Equal(Data(new YYGenericCPI(frequency, revised, period, ccy)), Data(yoy));
        Assert.Equal(("CPI", "YY_CPI", "Generic", "GENERIC"), (zero.FamilyName, yoy.FamilyName, zero.Region.Name, zero.Region.Code));
    }

    [Fact]
    public void TheGenericRegionBuiltInTheSetIsItsCodedClass()
    {
        Region built = Set.Region("GenericRegion");

        Assert.IsType<GenericRegion>(built);
        Assert.Equal((new GenericRegion().Name, new GenericRegion().Code), (built.Name, built.Code));
    }

    private static (string, string, UnitOfMeasureType, string?) Data(UnitOfMeasure u) =>
        (u.Name, u.Code, u.UnitType, u.TriangulationUnitOfMeasure?.Code);

    private static (string, string, string, string, bool, Frequency, Period, string) Data(InflationIndex i) =>
        (i.Name, i.FamilyName, i.Region.Name, i.Region.Code, i.Revised, i.Frequency, i.AvailabilityLag, i.Currency.Code);
}
