// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// The index kinds in the set: a user's index on an overridden calendar; fixings found under the resolved index's
/// name; built indexes through <c>Describe</c>, the writer, the reader and resolution (CVD14); a coded index preset's
/// name keeping its class and its constructor's arguments (CVD28); the fields a user's style can't take refused
/// (CVD38); and <see cref="ConventionSet.Empty"/> refusing an index preset it doesn't define (CVD25).
/// </summary>
public class IndexSetTests
{
    private static readonly Date Holiday = new(16, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.October, 2026) };
    private static readonly FlatForward Curve = new(new Date(15, Month.October, 2026), 0.03, new Actual365Fixed());

    [Fact]
    public void AUsersIndexOnAnOverriddenCalendarTakesItsEdits()
    {
        ConventionSet set = ConventionsText.OnQuantLib("""
            <Calendar name="Desk" base="TARGET"><AddedHoliday>2026-10-16</AddedHoliday></Calendar>
            <IborIndex name="DeskIndex"><Family>Desk</Family><Tenor>3M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency>
              <FixingCalendar>Desk</FixingCalendar><Convention>ModifiedFollowing</Convention><EndOfMonth>true</EndOfMonth><DayCounter>ACT/360</DayCounter></IborIndex>
            """);
        IborIndex index = set.IborIndex("DeskIndex");

        Assert.IsType<IborIndex>(index);
        Assert.True(index.FixingCalendar.IsHoliday(Holiday));
        Assert.False(index.IsValidFixingDate(Holiday));
        Assert.Equal(new Actual360(), index.DayCounter);
        Assert.Equal("Desk3M Actual/360", index.Name);
    }

    [Fact]
    public void AFixingIsFoundUnderTheResolvedIndexsName()
    {
        // the fixings are keyed by the index's name, which its family name, tenor and day counter make
        IborIndex shipped = ConventionSet.QuantLib.IborIndex("Euribor6M");
        EvaluationContext context = Context with { Fixings = Context.Fixings.AddFixing(new Euribor6M(), new Date(14, Month.October, 2026), 0.025) };
        ConventionSet renamed = ConventionsText.OnQuantLib("<IborIndex name=\"Euribor6M\" base=\"Euribor6M\"><DayCounter>Actual365Fixed</DayCounter></IborIndex>");

        Assert.Equal(0.025, shipped.Fixing(new Date(14, Month.October, 2026), context));
        Assert.Equal("Euribor6M Actual/365 (Fixed)", renamed.IborIndex("Euribor6M").Name);
        Assert.ThrowsAny<Exception>(() => renamed.IborIndex("Euribor6M").Fixing(new Date(14, Month.October, 2026), context));
    }

    public static TheoryData<string> Described() => ["calendar", "overnight", "edited", "libor", "custom"];

    [Theory]
    [MemberData(nameof(Described))]
    public void ABuiltIndexIsDescribedAndResolvesFieldByField(string kind)
    {
        IborIndex value = kind switch
        {
            // an index differing from its preset only in its fixing calendar
            "calendar" => new IborIndex("Euribor", new Period(6, TimeUnit.Months), 2, new EURCurrency(), new UnitedKingdom(),
                BusinessDayConvention.ModifiedFollowing, true, new Actual360()),

            // a plain overnight index with Sonia's fields
            "overnight" => new OvernightIndex("Sonia", 0, new GBPCurrency(), new UnitedKingdom(UnitedKingdomMarket.Exchange), new Actual365Fixed()),

            // an index on an edited TARGET, and a Business252 on an edited calendar
            "edited" => new IborIndex("Desk", new Period(3, TimeUnit.Months), 2, new EURCurrency(), new TARGET().AddHoliday(Holiday),
                BusinessDayConvention.Following, false, new Business252(new TARGET().AddHoliday(Holiday))),
            "libor" => new Libor("MyLibor", new Period(3, TimeUnit.Months), 2, new CHFCurrency(), new Switzerland(), new Actual360()),
            _ => new CustomIborIndex("MyCustom", new Period(6, TimeUnit.Months), 2, new EURCurrency(), new TARGET(),
                new UnitedKingdom(), new UnitedKingdom(UnitedKingdomMarket.Exchange), BusinessDayConvention.ModifiedFollowing, true, new Actual360()),
        };
        ConventionSnapshot s = ConventionRecords.Describe("Mine", value);
        ConventionSet set = ConventionSet.QuantLib.With(ConventionsText.Read(ConventionsText.Write(s)));
        IborIndex resolved = set.IborIndex("Mine");

        Assert.Equal(value.GetType(), resolved.GetType());
        Assert.Equal(IndexFields.Of(value), IndexFields.Of(resolved));
        Assert.Equal(value.FixingCalendar, resolved.FixingCalendar);
        Assert.Equal(value.DayCounter, resolved.DayCounter);
        Assert.Equal(IndexFields.Dates(value, 2026), IndexFields.Dates(resolved, 2026));
    }

    [Fact]
    public void AnIndexEqualToAPresetIsItsAliasAndABehaviourClassEqualToNoneIsRefused()
    {
        Assert.Equal([new AliasRecord("Mine", "Euribor6M")], ConventionRecords.Describe("Mine", new Euribor6M()).Records);
        Assert.Equal([new AliasRecord("Mine", "Sofr")], ConventionRecords.Describe("Mine", new Sofr()).Records);

        // a CDI on another curve is still the preset; one under other conventions has behaviour no record gives
        ConventionSet edited = ConventionsText.OnQuantLib("<OvernightIndex name=\"Cdi\" base=\"Cdi\"><FixingDays>1</FixingDays></OvernightIndex>");
        Cdi cdi;
        using (ConventionProvider.Use(edited))
        {
            cdi = new Cdi();
        }

        ArgumentException e = Assert.Throws<ArgumentException>(() => ConventionRecords.Describe("Mine", cdi));
        Assert.Contains("has behaviour no record gives", e.Message, StringComparison.Ordinal);
    }

    public static TheoryData<string, string?> IndexPairings() => new()
    {
        // a coded index preset's name takes a record of its own kind, on itself or QuantLib's base for it
        { "<IborIndex name=\"Euribor6M\" base=\"Euribor6M\"><FixingDays>1</FixingDays></IborIndex>", null },
        { "<IborIndex name=\"Euribor6M\" base=\"Euribor\"><Tenor>6M</Tenor><FixingDays>1</FixingDays></IborIndex>", null },
        { "<IborIndex name=\"Euribor6M\" base=\"Euribor365\"><Tenor>6M</Tenor></IborIndex>", "a preset record takes only its own name, or the preset QuantLib's record bases it on, as its base" },
        { "<IborIndex name=\"Euribor6M\" base=\"Euribor6M\"><Tenor>3M</Tenor></IborIndex>", "its tenor is its class's, 6M" },
        { "<OvernightIndex name=\"Euribor6M\"><FixingDays>0</FixingDays></OvernightIndex>", "the name is a coded preset's, which takes a IborIndex record" },
        { "<IborIndex name=\"Sofr\" base=\"Sofr\" />", "the name is a coded preset's, which takes a OvernightIndex record" },
        { "<IborIndex name=\"Euribor\" />", "the name is a coded preset's, which takes a IborFamily record" },
        { "<YoYInflationIndex name=\"EUHICP\" />", "the name is a coded preset's, which takes a ZeroInflationIndex record" },
        { "<IborIndex name=\"Euribor6M\" base=\"Euribor6M\"><Style>Libor</Style></IborIndex>", "a preset takes no style" },
        { "<IborIndex name=\"DailyTenorUSDLibor\" base=\"DailyTenorUSDLibor\"><FixingDays>1</FixingDays></IborIndex>", "its settlement days are its constructor's argument" },
        { "<ZeroInflationIndex name=\"AUCPI\" base=\"AUCPI\"><Frequency>Quarterly</Frequency></ZeroInflationIndex>", "its frequency and revised flag are its constructor's arguments" },
        { "<IborIndex name=\"BMAIndex\" base=\"BMAIndex\"><Convention>Following</Convention></IborIndex>", "the BMA index takes no convention, end of month or calendar but its fixing calendar, and its tenor is a week" },
        { "<YoYInflationIndex name=\"YYEUHICP\" base=\"YYEUHICP\"><UnderlyingIndex>EUHICP</UnderlyingIndex></YoYInflationIndex>", "a quoted preset takes no underlying index" },
        { "<Region name=\"EURegion\" base=\"EURegion\"><RegionName>Eurozone</RegionName></Region>", "QuantLib's name is EU, not Eurozone" },
        { "<Region name=\"EURegion\" base=\"EURegion\"><Code>EZ</Code></Region>", null },
        { "<OvernightIndex name=\"Sofr\" base=\"Sofr\"><FixingDays>1</FixingDays></OvernightIndex>", null },
        { "<SwapIndex name=\"EuriborSwapIsdaFixA\" base=\"EuriborSwapIsdaFixA\"><SettlementDays>1</SettlementDays></SwapIndex>", null },

        // a record based on a preset taking arguments gives only them
        { "<ZeroInflationIndex name=\"AUCPI-Q\" base=\"AUCPI\"><Revised>false</Revised><Frequency>Quarterly</Frequency></ZeroInflationIndex>", null },
        { "<ZeroInflationIndex name=\"AUCPI-Q\" base=\"AUCPI\"><Revised>false</Revised><Frequency>Quarterly</Frequency><Currency>USD</Currency></ZeroInflationIndex>", "a record based on AUCPI gives only its constructor's arguments" },
        { "<IborIndex name=\"USDLiborTN\" base=\"DailyTenorUSDLibor\"><FixingDays>1</FixingDays></IborIndex>", null },
        { "<IborIndex name=\"USDLiborTN\" base=\"DailyTenorUSDLibor\"><FixingDays>1</FixingDays><DayCounter>Actual365Fixed</DayCounter></IborIndex>", "a record based on DailyTenorUSDLibor gives only its constructor's arguments" },

        // a user's own index takes the fields its style builds
        { "<IborIndex name=\"Mine\"><Family>M</Family><Tenor>3M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><Convention>Following</Convention><EndOfMonth>false</EndOfMonth><DayCounter>Actual360</DayCounter><FinancialCenterCalendar>TARGET</FinancialCenterCalendar></IborIndex>", "an Ibor-style index has no financial-centre, value or maturity calendar" },
        { "<IborIndex name=\"Mine\"><Family>M</Family><Tenor>3M</Tenor><FixingDays>2</FixingDays><Currency>CHF</Currency><Convention>Following</Convention><DayCounter>Actual360</DayCounter><Style>Libor</Style><FinancialCenterCalendar>Switzerland</FinancialCenterCalendar></IborIndex>", "a Libor's fixing calendar, convention and end of month are Libor's own" },
    };

    [Theory]
    [MemberData(nameof(IndexPairings))]
    public void AnIndexRecordIsTakenOnlyWhereItsClassAndStyleCanTakeIt(string record, string? refusal)
    {
        if (refusal is not null)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => ConventionsText.OnQuantLib(record));
            Assert.Contains(refusal, e.Message, StringComparison.Ordinal);
            return;
        }

        ConventionSet set = ConventionsText.OnQuantLib(record);
        Assert.NotEqual(ConventionSet.QuantLib, set);
    }

    [Fact]
    public void ARecordBasedOnAPresetTakingArgumentsBuildsThePresetWithThem()
    {
        ConventionSet set = ConventionsText.OnQuantLib("""
            <ZeroInflationIndex name="AUCPI-Q" base="AUCPI"><Revised>true</Revised><Frequency>Quarterly</Frequency></ZeroInflationIndex>
            <IborIndex name="USDLiborTN" base="DailyTenorUSDLibor"><FixingDays>1</FixingDays></IborIndex>
            """);

        ZeroInflationIndex aucpi = set.ZeroInflationIndex("AUCPI-Q");
        IborIndex tn = set.IborIndex("USDLiborTN");

        Assert.Equal((typeof(AUCPI), Frequency.Quarterly, true), (aucpi.GetType(), aucpi.Frequency, aucpi.Revised));
        Assert.Equal((typeof(DailyTenorUSDLibor), 1), (tn.GetType(), tn.FixingDays));
        Assert.Throws<ArgumentException>(() => set.ZeroInflationIndex("AUCPI"));
        Assert.Throws<ArgumentException>(() => set.IborIndex("DailyTenorUSDLibor"));
    }

    [Fact]
    public void TheEmptySetRefusesAnIndexPresetItDoesntDefineAndResolvesItsCalendar()
    {
        ConventionSet empty = ConventionSet.Empty;

        Assert.Contains("the set defines no Euribor", Assert.Throws<ArgumentException>(() => empty.IborIndex("Euribor6M")).Message, StringComparison.Ordinal);
        using (ConventionProvider.Use(empty))
        {
            Assert.Throws<ArgumentException>(() => new Sofr());
            Assert.Throws<ArgumentException>(() => new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years)));
            Assert.Equal(new TARGET(), new TARGET());
        }

        Assert.Equal(new TARGET(), empty.Calendar("TARGET"));

        // the defaults' full copy, read alone onto the empty set, defines them
        ConventionSet full = ConventionSet.Empty.With(ConventionSet.QuantLib.Layers[0]);
        Assert.Equal(IndexFields.Of(new Euribor6M()), IndexFields.Of(full.IborIndex("Euribor6M")));
    }

    [Fact]
    public void ASwapFamilyWithNoShortVariantTakesATenorOfAYearInDays()
    {
        // QuantLib builds UsdLiborSwapIsdaFixAm(365 * Days): only a family with a short variant compares its tenor with a
        // year, which 365 days against a year can't answer.
        var tenor = new Period(365, TimeUnit.Days);
        Assert.Equal(IndexFields.Of(new UsdLiborSwapIsdaFixAm(tenor)), IndexFields.Of(ConventionSet.QuantLib.SwapIndex("UsdLiborSwapIsdaFixAm", tenor)));
        using (ConventionProvider.Use(ConventionSet.QuantLib))
        {
            Assert.Equal(IndexFields.Of(new UsdLiborSwapIsdaFixAm(tenor)), IndexFields.Of(ConventionSet.QuantLib.SwapIndex("UsdLiborSwapIsdaFixAm", tenor)));
        }

        // A family with one still can't, as QuantLib's EuriborSwapIsdaFixA(365 * Days) can't.
        Assert.Throws<NanoQLException>(() => new EuriborSwapIsdaFixA(tenor));
        Assert.Throws<NanoQLException>(() => ConventionSet.QuantLib.SwapIndex("EuriborSwapIsdaFixA", tenor));
    }

    [Fact]
    public void AUsersSwapAndInflationIndexesAreBuiltFromTheirRecords()
    {
        ConventionSet set = ConventionsText.OnQuantLib("""
            <Region name="Atlantis"><RegionName>Atlantis</RegionName><Code>AT</Code></Region>
            <ZeroInflationIndex name="ATCPI"><Family>CPI</Family><Region>Atlantis</Region><Revised>false</Revised><Frequency>Monthly</Frequency><AvailabilityLag>2M</AvailabilityLag><Currency>EUR</Currency></ZeroInflationIndex>
            <YoYInflationIndex name="YYATCPI"><UnderlyingIndex>ATCPI</UnderlyingIndex></YoYInflationIndex>
            <SwapIndex name="DeskSwap"><Family>DeskSwap</Family><SettlementDays>2</SettlementDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><FixedLegTenor>1Y</FixedLegTenor><FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>30/360</FixedLegDayCounter><IborIndex>Euribor6M</IborIndex></SwapIndex>
            <OvernightIndexedSwapIndex name="EstrSwap"><SettlementDays>2</SettlementDays><Currency>EUR</Currency><OvernightIndex>EUR-EuroSTR</OvernightIndex></OvernightIndexedSwapIndex>
            """);

        ZeroInflationIndex zero = set.ZeroInflationIndex("ATCPI");
        YoYInflationIndex yoy = set.YoYInflationIndex("YYATCPI");
        SwapIndex swap = set.SwapIndex("DeskSwap", new Period(5, TimeUnit.Years));
        OvernightIndexedSwapIndex ois = set.OvernightIndexedSwapIndex("EstrSwap", new Period(2, TimeUnit.Years));

        Assert.Equal(("Atlantis CPI", "AT"), (zero.Name, zero.Region.Code));
        Assert.True(yoy.Ratio);
        Assert.Same(typeof(Euribor6M), swap.IborIndex.GetType());
        Assert.Equal(("DeskSwap5Y 30/360 (Bond Basis)", new Thirty360(Thirty360Convention.BondBasis)), (swap.Name, swap.DayCounter));
        Assert.Equal(("EstrSwap", typeof(Estr)), (ois.FamilyName, ois.OvernightIndex.GetType()));
    }

    public static TheoryData<string> IborPresets() => [.. CodedPresets.IborIndexes.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(IborPresets))]
    public void AnIborOrOvernightPresetBuiltOnACurveKeepsItsClass(string name)
    {
        // QuantLib's clones are its base classes', so the set builds an index on its curve, as its constructor does
        IborIndex index = ConventionSet.QuantLib.IborIndex(name, Curve);

        Assert.Same(CodedPresets.IborIndexes[name]().GetType(), index.GetType());
        Assert.Same(Curve, index.ForwardingTermStructure);
        Assert.Same(ConventionSet.QuantLib, TestConventions.ProviderOf(index));
    }

    [Fact]
    public void EveryOtherIndexLookupBuildsItsClassOnItsCurves()
    {
        ConventionSet set = ConventionSet.QuantLib;
        var discount = new FlatForward(Context.EvaluationDate, 0.02, new Actual365Fixed());
        var zeroCurve = new ZeroInflationCurve(
            new Date(15, Month.June, 2026), [new Date(1, Month.April, 2026), new Date(1, Month.April, 2032)], [0.03, 0.03], Frequency.Monthly, new Actual365Fixed());
        foreach ((string name, Func<Period, IborIndex> family) in CodedPresets.IborFamilies)
        {
            IborIndex index = set.IborIndex(name, new Period(6, TimeUnit.Months), Curve);
            Assert.Equal((family(new Period(6, TimeUnit.Months)).GetType(), Curve), (index.GetType(), index.ForwardingTermStructure));
        }

        foreach ((string name, Func<Period, SwapIndex> swap) in CodedPresets.SwapIndexes)
        {
            Type coded = swap(new Period(5, TimeUnit.Years)).GetType();
            SwapIndex one = set.SwapIndex(name, new Period(5, TimeUnit.Years), Curve);
            SwapIndex two = set.SwapIndex(name, new Period(5, TimeUnit.Years), Curve, discount);
            Assert.Equal((coded, Curve, false), (one.GetType(), one.ForwardingTermStructure, one.ExogenousDiscount));
            Assert.Equal((coded, Curve, true, (YieldTermStructure?)discount), (two.GetType(), two.ForwardingTermStructure, two.ExogenousDiscount, two.DiscountingTermStructure));
        }

        ZeroInflationIndex hicp = set.ZeroInflationIndex("EUHICP", zeroCurve);
        Assert.Equal((typeof(EUHICP), (ZeroInflationTermStructure?)zeroCurve), (hicp.GetType(), hicp.ZeroInflationTermStructure));
    }

    [Theory]
    [InlineData("Sonia", 15)]
    [InlineData("GBP-SONIA", 15)]
    [InlineData("Corra", 16)]
    [InlineData("Sofr", 19)]
    public void AnOisOnTheSetsOvernightIndexOnACurveSettlesAsTheClassBuiltDirectly(string name, int startDay)
    {
        // MakeOIS's default settlement days test the index's class: SONIA T+0, CORRA T+1, the others T+2
        OvernightIndex index = ConventionSet.QuantLib.OvernightIndex(name, Curve);
        OvernightIndex direct = index switch
        {
            Sonia => new Sonia(Curve),
            Corra => new Corra(Curve),
            _ => new Sofr(Curve),
        };

        OvernightIndexedSwap swap = new MakeOIS(new Period(5, TimeUnit.Years), index).ToOvernightIndexedSwap(Context);
        OvernightIndexedSwap expected = new MakeOIS(new Period(5, TimeUnit.Years), direct).ToOvernightIndexedSwap(Context);

        Assert.Equal(new Date(startDay, Month.October, 2026), swap.StartDate());
        Assert.Equal((expected.StartDate(), expected.MaturityDate()), (swap.StartDate(), swap.MaturityDate()));
    }

    [Fact]
    public void AUsersOvernightIndexedSwapIndexOnACurveBuildsAnOisSwaption()
    {
        ConventionSet set = ConventionsText.OnQuantLib("""
            <OvernightIndexedSwapIndex name="EstrSwap"><SettlementDays>2</SettlementDays><Currency>EUR</Currency><OvernightIndex>EUR-EuroSTR</OvernightIndex></OvernightIndexedSwapIndex>
            """);
        OvernightIndexedSwapIndex index = set.OvernightIndexedSwapIndex("EstrSwap", new Period(5, TimeUnit.Years), Curve);
        var direct = new OvernightIndexedSwapIndex("EstrSwap", new Period(5, TimeUnit.Years), 2, new EURCurrency(), new Estr(Curve));

        Swaption swaption = new MakeSwaption(index, new Period(1, TimeUnit.Years), 0.03).ToSwaption(Context);
        Swaption expected = new MakeSwaption(direct, new Period(1, TimeUnit.Years), 0.03).ToSwaption(Context);

        Assert.Equal((typeof(Estr), Curve), (index.OvernightIndex.GetType(), index.OvernightIndex.ForwardingTermStructure));
        Assert.IsType<OvernightIndexedSwap>(swaption.Underlying);
        Assert.Equal((expected.Underlying.StartDate(), expected.Underlying.MaturityDate()), (swaption.Underlying.StartDate(), swaption.Underlying.MaturityDate()));
    }

    [Fact]
    public void ASwapPresetsOtherIborIndexIsBuiltOnItsCurveAsItsClass()
    {
        // a swap family's record naming an index other than QuantLib's builds it on the swap index's curve
        ConventionSet set = ConventionsText.OnQuantLib("""
            <SwapIndex name="EuriborSwapIsdaFixA" base="EuriborSwapIsdaFixA"><IborIndex>Euribor365</IborIndex><IborIndexTenor>6M</IborIndexTenor></SwapIndex>
            """);
        SwapIndex swap = set.SwapIndex("EuriborSwapIsdaFixA", new Period(5, TimeUnit.Years), Curve);

        Assert.Equal((typeof(Euribor365), Curve), (swap.IborIndex.GetType(), swap.IborIndex.ForwardingTermStructure));
        Assert.Equal(new Period(6, TimeUnit.Months), swap.IborIndex.Tenor);
    }

}
