// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// Batch 7's kinds in a set: each lookup against the object built by hand from the record's fields, a commodity index on
/// an overridden calendar, the set's conversions over QuantLib's known factors, the settings through the set's units,
/// the refusals, an alias of a unit, and each kind's record through the writer, the reader and <c>Describe</c>.
/// </summary>
public class CommoditySetTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.October, 2026) };

    /// <summary>Gets a record of each kind, on QuantLib's units, which the walk builds through too.</summary>
    internal static string Conventions { get; } =
        "<CommodityType name=\"Brent\"><TypeName>Brent crude</TypeName><Code>BRT</Code></CommodityType>"
        + "<UnitOfMeasure name=\"Tonne\"><UnitName>Tonnes</UnitName><Code>T</Code><UnitType>Mass</UnitType></UnitOfMeasure>"
        + "<UnitOfMeasureConversion name=\"Brent-BBL-MT\"><CommodityType>Brent</CommodityType><Source>BarrelUnitOfMeasure</Source>"
        + "<Target>MTUnitOfMeasure</Target><Factor>0.1364</Factor></UnitOfMeasureConversion>"
        + "<PaymentTerm name=\"Pricing+5\"><EventType>PricingDate</EventType><OffsetDays>5</OffsetDays><Calendar>UnitedStates.NYSE</Calendar></PaymentTerm>"
        + "<CommodityIndex name=\"ICE-BRENT\"><IndexName>ICE Brent</IndexName><CommodityType>Brent</CommodityType><Currency>USD</Currency>"
        + "<UnitOfMeasure>BarrelUnitOfMeasure</UnitOfMeasure><Calendar>UnitedKingdom.Exchange</Calendar><LotQuantity>1000</LotQuantity>"
        + "<NearbyOffset>1</NearbyOffset></CommodityIndex>"
        + "<CommoditySettings name=\"EUR-MT\"><Currency>EUR</Currency><UnitOfMeasure>MTUnitOfMeasure</UnitOfMeasure></CommoditySettings>"
        + "<QuantitySettings name=\"ToBarrels\"><ConversionType>BaseUnitOfMeasureConversion</ConversionType><BaseUnitOfMeasure>BarrelUnitOfMeasure</BaseUnitOfMeasure></QuantitySettings>";

    private static readonly ConventionSet Set = ConventionsText.OnQuantLib(Conventions);

    [Fact]
    public void EachLookupBuildsWhatItsTypeBuildsFromTheRecordsFieldsByHand()
    {
        var brent = new CommodityType("Brent crude", "BRT");

        Assert.Equal(Seen(brent), Seen(Set.CommodityType("Brent")));
        Assert.Equal(Seen(new UnitOfMeasure("Tonnes", "T", UnitOfMeasureType.Mass)), Seen(Set.UnitOfMeasure("Tonne")));
        Assert.Equal(Seen(new PaymentTerm("Pricing+5", PaymentTermEventType.PricingDate, 5, new UnitedStates(UnitedStatesMarket.NYSE))), Seen(Set.PaymentTerm("Pricing+5")));
        Assert.Equal(
            Seen(new CommodityIndex("ICE Brent", brent, new Currencies.USDCurrency(), new BarrelUnitOfMeasure(), new UnitedKingdom(UnitedKingdomMarket.Exchange), 1000, null, null, 1)),
            Seen(Set.CommodityIndex("ICE-BRENT")));
        Assert.Equal(new CommoditySettings { Currency = new Currencies.EURCurrency(), UnitOfMeasure = new MTUnitOfMeasure() }, Set.CommoditySettings("EUR-MT"));
        Assert.Equal(
            new QuantitySettings { ConversionType = QuantityConversionType.BaseUnitOfMeasureConversion, BaseUnitOfMeasure = new BarrelUnitOfMeasure() },
            Set.QuantitySettings("ToBarrels"));

        // the payment date on the term's calendar: five days after a Friday pricing date, the Wednesday
        Assert.Equal(new Date(21, Month.October, 2026), Set.PaymentTerm("Pricing+5").GetPaymentDate(new Date(16, Month.October, 2026)));
    }

    [Fact]
    public void ACommodityIndexOnAnOverriddenCalendarTakesItsHoliday()
    {
        Date holiday = new(16, Month.October, 2026);
        ConventionSet set = ConventionsText.OnQuantLib(
            Conventions + "<Calendar name=\"UnitedKingdom.Exchange\" base=\"UnitedKingdom.Exchange\"><AddedHoliday>2026-10-16</AddedHoliday></Calendar>");

        Assert.False(set.CommodityIndex("ICE-BRENT").IsValidFixingDate(holiday));
        Assert.True(Set.CommodityIndex("ICE-BRENT").IsValidFixingDate(holiday));
    }

    [Fact]
    public void TheSetsConversionsAreQuantLibsKnownFactorsWithItsRecordsAddedAsQuantLibAddsThem()
    {
        // a new pair goes last, and QuantLib's backward litre row replaces nanoql's, the known factor's record, where the
        // set's layers first give it
        ConventionSet set = ConventionsText.OnQuantLib(Conventions
            + "<UnitOfMeasureConversion name=\"GAL-l\" base=\"GAL-l\"><Source>LitreUnitOfMeasure</Source><Target>GallonUnitOfMeasure</Target></UnitOfMeasureConversion>");
        UnitOfMeasureConversions conversions = set.UnitOfMeasureConversions();
        var none = new NullCommodityType();

        Assert.Equal(
            [("<NULL>", "MB", "BBL", 1000.0), ("<NULL>", "BBL", "GAL", 42.0), ("<NULL>", "MB", "GAL", 42000.0), ("<NULL>", "l", "GAL", 3.78541),
                ("<NULL>", "BBL", "l", 158.987), ("<NULL>", "kl", "BBL", 6.28981), ("<NULL>", "KL_tk", "BBL", 6.28981), ("BRT", "BBL", "MT", 0.1364)],
            conversions.Conversions.Select(Row));
        Assert.Equal(0.1364 * 1000, conversions.Lookup(new CommodityType("Brent crude", "BRT"), new BarrelUnitOfMeasure(), new MTUnitOfMeasure())
            .Convert(new Quantity(null, new BarrelUnitOfMeasure(), 1000)).Amount, 1e-12);
        Assert.Equal(1000 * 3.78541, conversions.Lookup(none, new LitreUnitOfMeasure(), new GallonUnitOfMeasure(), UnitOfMeasureConversionType.Direct)
            .Convert(new Quantity(none, new LitreUnitOfMeasure(), 1000)).Amount, 1e-9);

        // QuantLib's manager holds its known factors from its start, so a set on Empty does too
        ConventionSet empty = ConventionSet.Empty.With(ConventionsText.Records(
            "<CommodityType name=\"Brent\"><Code>BRT</Code></CommodityType><UnitOfMeasureConversion name=\"X\"><CommodityType>Brent</CommodityType>"
            + "<Source>BarrelUnitOfMeasure</Source><Target>MTUnitOfMeasure</Target><Factor>0.1364</Factor></UnitOfMeasureConversion>"
            + "<UnitOfMeasure name=\"BarrelUnitOfMeasure\"><UnitName>Barrels</UnitName><Code>BBL</Code><UnitType>Volume</UnitType></UnitOfMeasure>"
            + "<UnitOfMeasure name=\"MTUnitOfMeasure\"><UnitName>Metric Tonnes</UnitName><Code>MT</Code><UnitType>Mass</UnitType></UnitOfMeasure>"));
        Assert.Equal([.. UnitOfMeasureConversions.Default.Conversions.Select(Row), ("BRT", "BBL", "MT", 0.1364)], empty.UnitOfMeasureConversions().Conversions.Select(Row));
    }

    [Fact]
    public void AUnitsIdentityCheckBuildsNoPresetThroughTheSet()
    {
        // F5-1: on Empty, gallons through a desk's own unit build without a barrel the set doesn't define; a preset
        // record leaving its triangulation unit out takes QuantLib's, the barrel, which Empty then must define
        ConventionSet desk = ConventionSet.Empty.With(ConventionsText.Records(
            "<UnitOfMeasure name=\"CubicMetre\"><UnitName>Cubic metres</UnitName><Code>m3</Code><UnitType>Volume</UnitType></UnitOfMeasure>"
            + "<UnitOfMeasure name=\"GallonUnitOfMeasure\"><UnitName>US Gallons</UnitName><Code>GAL</Code><UnitType>Volume</UnitType>"
            + "<TriangulationUnit>CubicMetre</TriangulationUnit></UnitOfMeasure>"));
        Assert.Equal("m3", desk.UnitOfMeasure("GallonUnitOfMeasure").TriangulationUnitOfMeasure!.Code);

        // a record giving the triangulation unit alone takes QuantLib's other fields, built without its barrel
        ConventionSet partial = ConventionSet.Empty.With(ConventionsText.Records(
            "<UnitOfMeasure name=\"CubicMetre\"><UnitName>Cubic metres</UnitName><Code>m3</Code><UnitType>Volume</UnitType></UnitOfMeasure>"
            + "<UnitOfMeasure name=\"GallonUnitOfMeasure\" base=\"GallonUnitOfMeasure\"><TriangulationUnit>CubicMetre</TriangulationUnit></UnitOfMeasure>"));
        UnitOfMeasure gallon = partial.UnitOfMeasure("GallonUnitOfMeasure");
        Assert.Equal(("US Gallons", "GAL", "m3"), (gallon.Name, gallon.Code, gallon.TriangulationUnitOfMeasure!.Code));
        Assert.Contains("the set defines no BarrelUnitOfMeasure", Assert.Throws<ArgumentException>(() => ConventionSet.Empty.With(ConventionsText.Records(
            "<UnitOfMeasure name=\"LitreUnitOfMeasure\"><UnitName>Litres</UnitName><Code>l</Code><UnitType>Volume</UnitType></UnitOfMeasure>"))).Message, StringComparison.Ordinal);

        // on QuantLib's, barrels through gallons and gallons through tonnes, no cycle
        ConventionSet chain = ConventionsText.OnQuantLib(
            "<UnitOfMeasure name=\"BarrelUnitOfMeasure\" base=\"BarrelUnitOfMeasure\"><TriangulationUnit>GallonUnitOfMeasure</TriangulationUnit></UnitOfMeasure>"
            + "<UnitOfMeasure name=\"GallonUnitOfMeasure\" base=\"GallonUnitOfMeasure\"><TriangulationUnit>MTUnitOfMeasure</TriangulationUnit></UnitOfMeasure>");
        UnitOfMeasure barrel = chain.UnitOfMeasure("BarrelUnitOfMeasure");
        Assert.Equal(("GAL", "MT"), (barrel.TriangulationUnitOfMeasure!.Code, barrel.TriangulationUnitOfMeasure.TriangulationUnitOfMeasure!.Code));
    }

    [Fact]
    public void TheSettingsTakeTheSetsUnitsWhereTheirRecordsLeaveThemOut()
    {
        ConventionSet set = ConventionsText.OnQuantLib(
            "<UnitOfMeasure name=\"BarrelUnitOfMeasure\" base=\"BarrelUnitOfMeasure\"><UnitName>Oil barrels</UnitName></UnitOfMeasure>"
            + "<CommoditySettings name=\"EUR\"><Currency>EUR</Currency></CommoditySettings>");

        Assert.Equal("Oil barrels", set.CommoditySettings().UnitOfMeasure.Name);
        Assert.Equal(("EUR", "Oil barrels"), (set.CommoditySettings("EUR").Currency.Code, set.CommoditySettings("EUR").UnitOfMeasure.Name));
        Assert.Equal("Oil barrels", set.UnitOfMeasure("BarrelUnitOfMeasure").Name);
        Assert.Equal("Oil barrels", set.UnitOfMeasure("GallonUnitOfMeasure").TriangulationUnitOfMeasure!.Name);
    }

    [Fact]
    public void AnAliasNamesAUnitAndTheGenericIndexesPresetsTakeTheirArguments()
    {
        ConventionSet set = ConventionsText.OnQuantLib("<Alias name=\"bbl\" base=\"BarrelUnitOfMeasure\" />");

        Assert.IsType<BarrelUnitOfMeasure>(set.UnitOfMeasure("bbl"));

        // a unit's name is in the aliases' name space, so an alias and a unit can't share one: in a snapshot, and in a
        // file by the schema's name group
        Assert.Contains("the name bbl is defined twice", Assert.Throws<ArgumentException>(() => ConventionSnapshot.Empty.Add(new AliasRecord("bbl", "TARGET"))
            .Add(new UnitOfMeasureRecord("bbl") { Code = "bbl", UnitType = UnitOfMeasureType.Volume })).Message, StringComparison.Ordinal);
        Assert.Throws<System.Xml.Schema.XmlSchemaValidationException>(() => ConventionsText.Records(
            "<Alias name=\"bbl\" base=\"TARGET\" /><UnitOfMeasure name=\"bbl\"><Code>bbl</Code><UnitType>Volume</UnitType></UnitOfMeasure>"));
        Assert.Contains("takes its frequency, revised flag, lag and currency", Assert.Throws<ArgumentException>(() => Set.ZeroInflationIndex("GenericCPI")).Message, StringComparison.Ordinal);
        Assert.Contains("takes its frequency, revised flag, lag and currency", Assert.Throws<ArgumentException>(() => Set.YoYInflationIndex("YYGenericCPI")).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<UnitOfMeasure name=\"X\"><UnitName>X</UnitName><Code>X</Code><UnitType>Mass</UnitType><TriangulationUnit>MTUnitOfMeasure</TriangulationUnit></UnitOfMeasure>",
        "a unit of a user's own takes no TriangulationUnit")]
    [InlineData("<UnitOfMeasure name=\"X\" base=\"GallonUnitOfMeasure\"><UnitName>Gallons</UnitName></UnitOfMeasure>", "chain gives a TriangulationUnit")]
    [InlineData("<UnitOfMeasure name=\"BarrelUnitOfMeasure\" base=\"BarrelUnitOfMeasure\"><Code>BBX</Code></UnitOfMeasure>", "its code is its identity, QuantLib's BBL, not BBX")]
    [InlineData("<UnitOfMeasure name=\"MTUnitOfMeasure\"><Code>MT</Code></UnitOfMeasure>", "UnitName, Code and UnitType are required without a base")]
    [InlineData("<UnitOfMeasure name=\"BarrelUnitOfMeasure\" base=\"BarrelUnitOfMeasure\"><TriangulationUnit>GallonUnitOfMeasure</TriangulationUnit></UnitOfMeasure>",
        "part of a cycle of triangulation units")]
    [InlineData("<UnitOfMeasure name=\"X\"><Code>X</Code></UnitOfMeasure>", "the unit of measure X gives no UnitType")]
    [InlineData("<UnitOfMeasureConversion name=\"X\"><Source>BarrelUnitOfMeasure</Source><Target>MTUnitOfMeasure</Target></UnitOfMeasureConversion>",
        "the unit of measure conversion X gives no Factor")]
    [InlineData("<UnitOfMeasureConversion name=\"X\"><Source>Barrel</Source><Target>MTUnitOfMeasure</Target><Factor>1</Factor></UnitOfMeasureConversion>",
        "unknown unit of measure Barrel")]
    [InlineData("<CommodityType name=\"X\"><TypeName>X</TypeName></CommodityType>", "the commodity type X gives no Code")]
    [InlineData("<PaymentTerm name=\"X\"><EventType>TradeDate</EventType><OffsetDays>1</OffsetDays></PaymentTerm>", "the payment term X gives no Calendar")]
    [InlineData("<CommodityIndex name=\"X\"><UnitOfMeasure>BarrelUnitOfMeasure</UnitOfMeasure><Calendar>TARGET</Calendar><LotQuantity>1</LotQuantity><NearbyOffset>0</NearbyOffset></CommodityIndex>",
        "the commodity index X gives no Currency")]
    [InlineData("<CommodityIndex name=\"X\"><CommodityType>Oil</CommodityType><Currency>USD</Currency><UnitOfMeasure>BarrelUnitOfMeasure</UnitOfMeasure><Calendar>TARGET</Calendar><LotQuantity>1</LotQuantity><NearbyOffset>0</NearbyOffset></CommodityIndex>",
        "unknown commodity type Oil")]
    [InlineData("<ZeroInflationIndex name=\"GenericCPI\" base=\"GenericCPI\"><Currency>EUR</Currency></ZeroInflationIndex>",
        "its frequency, revised flag, availability lag and currency are its constructor's arguments")]
    [InlineData("<ZeroInflationIndex name=\"X\" base=\"GenericCPI\"><Revised>false</Revised><Frequency>Monthly</Frequency><Currency>EUR</Currency></ZeroInflationIndex>",
        "the zero inflation index X gives no AvailabilityLag")]
    public void WithRefusesABatch7RecordThatBreaksItsTypesRulesNamingIt(string record, string refusal)
    {
        Assert.Contains(refusal, Assert.Throws<ArgumentException>(() => ConventionsText.OnQuantLib(record)).Message, StringComparison.Ordinal);
    }

    public static TheoryData<string> RoundTrips() =>
    [
        "<CommodityType name=\"X\"><TypeName>Brent crude</TypeName><Code>BRT</Code></CommodityType>",
        "<UnitOfMeasure name=\"X\"><UnitName>Tonnes</UnitName><Code>T</Code><UnitType>Mass</UnitType></UnitOfMeasure>",
        "<UnitOfMeasure name=\"GallonUnitOfMeasure\" base=\"GallonUnitOfMeasure\"><UnitName>Gallons</UnitName><UnitType>Energy</UnitType><TriangulationUnit>MBUnitOfMeasure</TriangulationUnit></UnitOfMeasure>",
        "<UnitOfMeasureConversion name=\"X\"><CommodityType>Brent</CommodityType><Source>BarrelUnitOfMeasure</Source><Target>Tonne</Target><Factor>0.1364</Factor></UnitOfMeasureConversion>",
        "<UnitOfMeasureConversion name=\"X\"><Source>GallonUnitOfMeasure</Source><Target>LitreUnitOfMeasure</Target><Factor>3.78541</Factor></UnitOfMeasureConversion>",
        "<PaymentTerm name=\"X\"><TermName>T+2</TermName><EventType>TradeDate</EventType><OffsetDays>2</OffsetDays><Calendar>Desk</Calendar></PaymentTerm>",
        "<CommodityIndex name=\"X\"><IndexName>Desk Brent</IndexName><CommodityType>Brent</CommodityType><Currency>GBP</Currency><UnitOfMeasure>Tonne</UnitOfMeasure>"
            + "<Calendar>Desk</Calendar><LotQuantity>500</LotQuantity><NearbyOffset>2</NearbyOffset></CommodityIndex>",
        "<CommoditySettings name=\"X\"><Currency>EUR</Currency><UnitOfMeasure>Tonne</UnitOfMeasure></CommoditySettings>",
        "<QuantitySettings name=\"X\"><ConversionType>AutomatedConversion</ConversionType><BaseUnitOfMeasure>MTUnitOfMeasure</BaseUnitOfMeasure></QuantitySettings>",
        "<QuantitySettings name=\"X\"><ConversionType>NoConversion</ConversionType></QuantitySettings>",
    ];

    [Theory]
    [MemberData(nameof(RoundTrips))]
    public void EachKindsRecordRoundTripsAndItsBuiltObjectIsDescribedAsRecordsThatRebuildIt(string record)
    {
        // the records the record names: a commodity type, a unit and a calendar of a user's own
        const string Parts = "<CommodityType name=\"Brent\"><Code>BRT</Code></CommodityType>"
            + "<UnitOfMeasure name=\"Tonne\"><UnitName>Tonnes</UnitName><Code>T</Code><UnitType>Mass</UnitType></UnitOfMeasure>"
            + "<Calendar name=\"Desk\" base=\"TARGET\"><AddedHoliday>2026-12-28</AddedHoliday></Calendar>";
        ConventionSnapshot snapshot = ConventionsText.Records(Parts + record);
        Assert.Equal(snapshot, ConventionsText.Read(ConventionsText.Write(snapshot)));

        ConventionSet set = ConventionSet.QuantLib.With(snapshot);
        string name = snapshot.Records[^1].Name;
        object built = Build(set, snapshot.Records[^1], name);
        ConventionSnapshot described = Describe(built);

        Assert.Equal(described, ConventionsText.Read(ConventionsText.Write(described)));
        Assert.Equal(Seen(built), Seen(Build(ConventionSet.QuantLib.With(described), snapshot.Records[^1], "D")));
    }

    [Fact]
    public void QuantLibsNullCommodityTypeAndADerivedConversionAreDescribedByNoRecord()
    {
        UnitOfMeasureConversions conversions = ConventionSet.QuantLib.UnitOfMeasureConversions();
        UnitOfMeasureConversion derived = conversions.Lookup(new NullCommodityType(), new KilolitreUnitOfMeasure(), new LitreUnitOfMeasure());

        Assert.Equal(UnitOfMeasureConversionType.Derived, derived.Type);
        Assert.Contains("a derived conversion chains two others", Assert.Throws<ArgumentException>(() => ConventionRecords.Describe("D", derived)).Message, StringComparison.Ordinal);
        Assert.Contains("null commodity type has no record", Assert.Throws<ArgumentException>(() => ConventionRecords.Describe("D", new NullCommodityType())).Message, StringComparison.Ordinal);

        // a known factor is described without its commodity type
        Assert.Null(((UnitOfMeasureConversionRecord)ConventionRecords.Describe("D", conversions.Conversions[0]).Records[^1]).CommodityType);
    }

    private static object Build(ConventionSet set, ConventionRecord r, string name) => r switch
    {
        CommodityTypeRecord => set.CommodityType(name),
        UnitOfMeasureRecord => set.UnitOfMeasure(name),
        UnitOfMeasureConversionRecord when name == "D" => set.UnitOfMeasureConversions().Conversions[^1],
        UnitOfMeasureConversionRecord => set.UnitOfMeasureConversions().Conversions.Single(c => c.Code == ConversionCode(set, r)),
        PaymentTermRecord => set.PaymentTerm(name),
        CommodityIndexRecord => set.CommodityIndex(name),
        CommoditySettingsRecord => set.CommoditySettings(name),
        QuantitySettingsRecord => set.QuantitySettings(name),
        _ => throw new ArgumentException(r.GetType().Name),
    };

    // a direct conversion's code, its commodity type's name and its units' codes
    private static string ConversionCode(ConventionSet set, ConventionRecord r)
    {
        var c = (UnitOfMeasureConversionRecord)r;
        string type = c.CommodityType is string t ? set.CommodityType(t).Name : new NullCommodityType().Name;
        return type + set.UnitOfMeasure(c.Source!).Code + set.UnitOfMeasure(c.Target!).Code;
    }

    // the records describing a built object, in QuantLib's set
    private static ConventionSnapshot Describe(object built) => built switch
    {
        CommodityType t => ConventionRecords.Describe("D", t),
        UnitOfMeasure u => ConventionRecords.Describe("D", u),
        UnitOfMeasureConversion c => ConventionRecords.Describe("D", c),
        PaymentTerm p => ConventionRecords.Describe("D", p),
        CommodityIndex i => ConventionRecords.Describe("D", i),
        CommoditySettings s => ConventionRecords.Describe("D", s),
        QuantitySettings q => ConventionRecords.Describe("D", q),
        _ => throw new ArgumentException(built.GetType().Name),
    };

    private static (string, string, string, double) Row(UnitOfMeasureConversion c) =>
        (c.CommodityType!.Code, c.Source.Code, c.Target.Code, c.ConversionFactor);

    // an object's fields, a commodity index's spelled out (the fingerprint writes an index as its name)
    private static string Seen(object value) => Fingerprint.Of(
        value is CommodityIndex i
            ? new object?[] { i.Name, i.CommodityType, i.Currency, i.UnitOfMeasure, i.FixingCalendar, i.LotQuantity, i.NearbyOffset, i.ForwardCurve, i.ExchangeContracts }
            : value,
        Context);
}
