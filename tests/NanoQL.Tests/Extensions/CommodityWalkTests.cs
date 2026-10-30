// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Extensions.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// The no-bypass walk (CVD34) over batch 7's kinds: what each builds in the marker set reaches only the set's calendars,
/// currencies and units of measure.
/// </summary>
public class CommodityWalkTests
{
    private static readonly ConventionSet Set = ConventionWalk.Set.With(ConventionsText.Records(
        "<CommodityType name=\"Brent\"><Code>BRT</Code></CommodityType>"
        + "<UnitOfMeasureConversion name=\"Brent-BBL-MT\"><CommodityType>Brent</CommodityType><Source>BarrelUnitOfMeasure</Source>"
        + "<Target>MTUnitOfMeasure</Target><Factor>0.1364</Factor></UnitOfMeasureConversion>"
        + "<PaymentTerm name=\"Pricing+5\"><EventType>PricingDate</EventType><OffsetDays>5</OffsetDays><Calendar>UnitedStates.NYSE</Calendar></PaymentTerm>"
        + "<CommodityIndex name=\"ICE-BRENT\"><CommodityType>Brent</CommodityType><Currency>USD</Currency><UnitOfMeasure>BarrelUnitOfMeasure</UnitOfMeasure>"
        + "<Calendar>UnitedKingdom.Exchange</Calendar><LotQuantity>1000</LotQuantity><NearbyOffset>1</NearbyOffset></CommodityIndex>"
        + "<CommoditySettings name=\"EUR-MT\"><Currency>EUR</Currency><UnitOfMeasure>MTUnitOfMeasure</UnitOfMeasure></CommoditySettings>"
        + "<QuantitySettings name=\"ToBarrels\"><ConversionType>BaseUnitOfMeasureConversion</ConversionType><BaseUnitOfMeasure>BarrelUnitOfMeasure</BaseUnitOfMeasure></QuantitySettings>"
        + "<ZeroInflationIndex name=\"G\" base=\"GenericCPI\"><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>3M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex>"
        + "<YoYInflationIndex name=\"YG\" base=\"YYGenericCPI\"><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>3M</AvailabilityLag><Currency>EUR</Currency></YoYInflationIndex>"));

    public static TheoryData<string> Builds() => [.. Built.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Builds))]
    public void EachKindsBuildReachesOnlyTheMarkerSetsCalendarsCurrenciesAndUnits(string build)
    {
        Assert.Empty(ConventionWalk.Unmarked(Built[build]()));
        Assert.NotEmpty(ConventionWalk.Unmarked(new CommodityIndex(
            "ICE Brent", new CommodityType("Brent", "BRT"), new USDCurrency(), new BarrelUnitOfMeasure(), new TARGET(), 1000, null, null, 1)));
    }

    private static readonly Dictionary<string, Func<object>> Built = new(StringComparer.Ordinal)
    {
        ["CommodityIndex"] = () => Set.CommodityIndex("ICE-BRENT"),
        ["PaymentTerm"] = () => Set.PaymentTerm("Pricing+5"),
        ["CommoditySettings"] = () => Set.CommoditySettings(),
        ["CommoditySettings EUR-MT"] = () => Set.CommoditySettings("EUR-MT"),
        ["QuantitySettings"] = () => Set.QuantitySettings("ToBarrels"),
        ["UnitOfMeasureConversions"] = () => Set.UnitOfMeasureConversions(),
        ["UnitsOfMeasure"] = () => new[]
        {
            "BarrelUnitOfMeasure", "MTUnitOfMeasure", "MBUnitOfMeasure", "GallonUnitOfMeasure", "LitreUnitOfMeasure", "KilolitreUnitOfMeasure",
            "TokyoKilolitreUnitOfMeasure", "LotUnitOfMeasure",
        }.Select(Set.UnitOfMeasure).ToArray(),
        ["GenericCPI"] = () => Set.ZeroInflationIndex("G"),
        ["YYGenericCPI"] = () => Set.YoYInflationIndex("YG"),
        ["A unit built in the set's scope"] = () =>
        {
            using (ConventionProvider.Use(Set))
            {
                return new GallonUnitOfMeasure();
            }
        },
    };
}
