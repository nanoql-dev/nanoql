// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// The instrument conventions in the set: overrides of the builders' defaults and of an index reaching what the
/// conventions build; each kind's route against its builder or helper given the same options by hand, bit for bit (the
/// built instrument and its value on a flat curve, a helper's implied quote and pillar); the routes' refusals; the
/// default records' rules; and each kind's records through the writer, the reader and <c>Describe</c>.
/// </summary>
public class InstrumentSetTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.October, 2026) };
    private static readonly FlatForward Curve = new(new Date(15, Month.October, 2026), 0.03, new Actual365Fixed());
    private static readonly FlatHazardRate Hazard = new(new Date(15, Month.October, 2026), 0.02, new Actual365Fixed());

    private const string EurSwap = "<SwapConvention name=\"EUR-SWAP\"><SettlementDays>2</SettlementDays><FixedLegTenor>1Y</FixedLegTenor>"
        + "<FixedLegConvention>ModifiedFollowing</FixedLegConvention><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter><Index>Euribor</Index><IndexTenor>6M</IndexTenor></SwapConvention>";

    // USD SOFR as the docs' example sets it: a payment lag of 2 on the government bond calendar
    private const string UsdSofr = "<OisConvention name=\"USD-SOFR\"><Index>Sofr</Index><SettlementDays>2</SettlementDays><PaymentLag>2</PaymentLag>"
        + "<PaymentFrequency>Annual</PaymentFrequency><PaymentCalendar>USGS</PaymentCalendar><FixedLegDayCounter>ACT/360</FixedLegDayCounter></OisConvention>";

    [Fact]
    public void TheEurSwapDefaultsFixedTenorReachesASwapRecordThatLeavesItOut()
    {
        ConventionSet set = ConventionsText.OnQuantLib(
            "<SwapConvention name=\"MakeVanillaSwap.EUR\" base=\"MakeVanillaSwap.EUR\"><FixedLegTenor>6M</FixedLegTenor></SwapConvention>"
            + "<SwapConvention name=\"X\"><Index>Euribor6M</Index></SwapConvention>");
        VanillaSwap swap = set.Swap("X").Builder(new Period(5, TimeUnit.Years), Curve).WithFixedRate(0.03).ToVanillaSwap(Context);
        SwapRateHelper helper = set.Swap("X").RateHelper(0.03, new Period(5, TimeUnit.Years), Context, forwardingCurve: Curve);

        Assert.Equal(10, swap.FixedLeg.Length);
        Assert.Equal(10, helper.Swap.FixedLeg.Length);
    }

    [Fact]
    public void ATenorABasedRecordGivesAloneAppliesToItsBasesIndex()
    {
        // A 3M swap convention based on a 6M one, and a swap index based on a family's preset, built
        ConventionSet set = ConventionsText.OnQuantLib(EurSwap
            + "<SwapConvention name=\"EUR-SWAP-3M\" base=\"EUR-SWAP\"><IndexTenor>3M</IndexTenor></SwapConvention>"
            + "<SwapIndex name=\"MySwap\" base=\"EuriborSwapIsdaFixA\"><IborIndexTenor>3M</IborIndexTenor><ShortIborIndexTenor>1M</ShortIborIndexTenor></SwapIndex>");
        VanillaSwap swap = set.Swap("EUR-SWAP-3M").Builder(new Period(5, TimeUnit.Years), Curve).WithFixedRate(0.03).ToVanillaSwap(Context);
        SwapRateHelper helper = set.Swap("EUR-SWAP-3M").RateHelper(0.03, new Period(5, TimeUnit.Years), Context, forwardingCurve: Curve);

        Assert.Equal(new Period(3, TimeUnit.Months), swap.IborIndex.Tenor);
        Assert.Equal(new Period(3, TimeUnit.Months), ((VanillaSwap)helper.Swap).IborIndex.Tenor);
        Assert.Equal(new Period(3, TimeUnit.Months), set.SwapIndex("MySwap", new Period(5, TimeUnit.Years)).IborIndex.Tenor);
        Assert.Equal(new Period(1, TimeUnit.Months), set.SwapIndex("MySwap", new Period(1, TimeUnit.Years)).IborIndex.Tenor);
    }

    [Fact]
    public void EachPairedFieldABasedRecordGivesAloneIsTakenWithItsBasesPartner()
    {
        // Every generated record's index tenor over a base giving the index and another tenor
        ConventionSet set = ConventionsText.OnQuantLib(Conventions
            + "<CapFloorConvention name=\"CAPF\"><Index>Euribor</Index><IndexTenor>6M</IndexTenor></CapFloorConvention>"
            + "<CapFloorConvention name=\"CAPF3M\" base=\"CAPF\"><IndexTenor>3M</IndexTenor></CapFloorConvention>"
            + "<CmsConvention name=\"CMS5Y\" base=\"CMS\"><SwapIndexTenor>5Y</SwapIndexTenor></CmsConvention>"
            + "<CmsConvention name=\"CMSF\" base=\"CMS\"><FloatingLegIndex>Euribor</FloatingLegIndex><FloatingLegIndexTenor>6M</FloatingLegIndexTenor></CmsConvention>"
            + "<CmsConvention name=\"CMSF3M\" base=\"CMSF\"><FloatingLegIndexTenor>3M</FloatingLegIndexTenor></CmsConvention>"
            + "<MultipleResetsSwapConvention name=\"MRSF\"><Index>Euribor</Index><IndexTenor>3M</IndexTenor><ResetsPerCoupon>4</ResetsPerCoupon></MultipleResetsSwapConvention>"
            + "<MultipleResetsSwapConvention name=\"MRSF1M\" base=\"MRSF\"><IndexTenor>1M</IndexTenor></MultipleResetsSwapConvention>"
            + "<CrossCurrencyBasisSwapConvention name=\"XB6M\" base=\"XB\"><QuoteCurrencyIndexTenor>6M</QuoteCurrencyIndexTenor></CrossCurrencyBasisSwapConvention>"
            + "<CrossCurrencyBasisSwapConvention name=\"XBF\" base=\"XB\"><BaseCurrencyIndex>Euribor</BaseCurrencyIndex><BaseCurrencyIndexTenor>3M</BaseCurrencyIndexTenor></CrossCurrencyBasisSwapConvention>"
            + "<CrossCurrencyBasisSwapConvention name=\"XBF6M\" base=\"XBF\"><BaseCurrencyIndexTenor>6M</BaseCurrencyIndexTenor></CrossCurrencyBasisSwapConvention>"
            + "<CrossCurrencySwapConvention name=\"XSF\" base=\"XS\"><FloatingIndex>Euribor</FloatingIndex><FloatingIndexTenor>3M</FloatingIndexTenor></CrossCurrencySwapConvention>"
            + "<CrossCurrencySwapConvention name=\"XSF6M\" base=\"XSF\"><FloatingIndexTenor>6M</FloatingIndexTenor></CrossCurrencySwapConvention>");
        Period m3 = new(3, TimeUnit.Months), m6 = new(6, TimeUnit.Months);

        Assert.Equal(("Euribor", (Period?)m3), (set.CapFloor("CAPF3M").Record.Index, set.CapFloor("CAPF3M").Record.IndexTenor));
        Assert.Equal(("EuriborSwapIsdaFixA", (Period?)new Period(5, TimeUnit.Years)), (set.Cms("CMS5Y").Record.SwapIndex, set.Cms("CMS5Y").Record.SwapIndexTenor));
        Assert.Equal(("Euribor", (Period?)m3), (set.Cms("CMSF3M").Record.FloatingLegIndex, set.Cms("CMSF3M").Record.FloatingLegIndexTenor));
        Assert.Equal(("Euribor", (Period?)new Period(1, TimeUnit.Months)), (set.MultipleResetsSwap("MRSF1M").Record.Index, set.MultipleResetsSwap("MRSF1M").Record.IndexTenor));
        Assert.Equal(("USDLibor", (Period?)m6), (set.CrossCurrencyBasisSwap("XB6M").Record.QuoteCurrencyIndex, set.CrossCurrencyBasisSwap("XB6M").Record.QuoteCurrencyIndexTenor));
        Assert.Equal(("Euribor", (Period?)m6), (set.CrossCurrencyBasisSwap("XBF6M").Record.BaseCurrencyIndex, set.CrossCurrencyBasisSwap("XBF6M").Record.BaseCurrencyIndexTenor));
        Assert.Equal(("Euribor", (Period?)m6), (set.CrossCurrencySwap("XSF6M").Record.FloatingIndex, set.CrossCurrencySwap("XSF6M").Record.FloatingIndexTenor));

        // a record giving the index without a tenor takes the index alone, its base's tenor left behind
        Assert.Null(ConventionsText.OnQuantLib(EurSwap + "<SwapConvention name=\"X\" base=\"EUR-SWAP\"><Index>Euribor3M</Index></SwapConvention>").Swap("X").Record.IndexTenor);
    }

    [Fact]
    public void AThresholdADefaultRecordGivesAloneReplacesItsBasesAndReachesTheBuilder()
    {
        // GBP's threshold at 2Y over the shipped record, and a user's NOK record giving a threshold only
        ConventionSet set = ConventionsText.OnQuantLib(
            "<SwapConvention name=\"MakeVanillaSwap.GBP\" base=\"MakeVanillaSwap.GBP\"><FixedLegTenorThreshold><Comparison>AtMost</Comparison>"
            + "<SwapTenor>2Y</SwapTenor><FixedLegTenor>1Y</FixedLegTenor></FixedLegTenorThreshold></SwapConvention>"
            + "<SwapConvention name=\"MakeVanillaSwap.NOK\"><FixedLegTenorThreshold><Comparison>AtMost</Comparison>"
            + "<SwapTenor>2Y</SwapTenor><FixedLegTenor>1Y</FixedLegTenor></FixedLegTenorThreshold><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter></SwapConvention>");
        Period FixedTenor(IborIndex index, int months) =>
            new MakeVanillaSwap(new Period(months, TimeUnit.Months), index).WithFixedRate(0.03).ToVanillaSwap(Context).FixedSchedule.Tenor();

        Assert.Equal(new Period(1, TimeUnit.Years), FixedTenor(set.IborIndex("GBPLibor", new Period(6, TimeUnit.Months), Curve), 18));
        Assert.Equal(new Period(6, TimeUnit.Months), FixedTenor(set.IborIndex("GBPLibor", new Period(6, TimeUnit.Months), Curve), 36));
        Assert.Equal(new Period(1, TimeUnit.Years), FixedTenor(set.IborIndex("Nibor", new Period(6, TimeUnit.Months), Curve), 12));
        Assert.Equal("unknown fixed leg default tenor for NOK",
            Assert.Throws<NanoQLException>(() => FixedTenor(set.IborIndex("Nibor", new Period(6, TimeUnit.Months), Curve), 36)).Message);
    }

    [Theory]
    [InlineData("<BondConvention name=\"X\" base=\"FixedRateBond\"><SettlementDays>1</SettlementDays></BondConvention>", "FixedRateBond is the FixedRateBond builder's default record")]
    [InlineData("<OisConvention name=\"X\" base=\"MakeOIS\"><Index>Sofr</Index></OisConvention>", "MakeOIS is the MakeOIS builder's default record")]
    [InlineData("<OisConvention name=\"X\" base=\"MakeOIS.Sonia\"><Index>Sonia</Index></OisConvention>", "MakeOIS.Sonia is the MakeOIS builder's default record")]
    [InlineData("<SwapConvention name=\"X\" base=\"MakeVanillaSwap.EUR\"><Index>Euribor6M</Index></SwapConvention>", "MakeVanillaSwap.EUR is the MakeVanillaSwap builder's default record")]
    public void AConventionBasedOnABuildersDefaultRecordIsRefusedNamingTheRule(string record, string refusal)
    {
        // The defaults reach a convention without the base, which the routes would read as the record's own fields
        string message = Assert.Throws<ArgumentException>(() => ConventionsText.OnQuantLib(record)).Message;

        Assert.Contains("a convention takes its builder's defaults without basing on them: " + refusal, message, StringComparison.Ordinal);
    }

    [Fact]
    public void SofrsFixingCalendarReachesAnOisBuiltThroughItsRecord()
    {
        Date holiday = new(16, Month.December, 2026);
        ConventionSet set = ConventionsText.OnQuantLib(
            "<Calendar name=\"Fed\" base=\"UnitedStates.SOFR\"><AddedHoliday>2026-12-16</AddedHoliday></Calendar>"
            + "<OvernightIndex name=\"Sofr\" base=\"Sofr\"><FixingCalendar>Fed</FixingCalendar></OvernightIndex>"
            + "<OisConvention name=\"X\"><Index>Sofr</Index></OisConvention>");
        OvernightIndexedSwap swap = set.Ois("X").Builder(new Period(1, TimeUnit.Years), Curve).WithFixedRate(0.03).ToOvernightIndexedSwap(Context);

        var coupon = (OvernightIndexedCoupon)swap.OvernightLeg[0];
        Assert.True(swap.OvernightSchedule.Calendar!.IsHoliday(holiday));
        Assert.DoesNotContain(holiday, coupon.FixingDates());
        Assert.IsType<Sofr>(swap.OvernightIndex);
    }

    [Fact]
    public void ASwapDefaultRecordAUserAddsBuildsWhereQuantLibsBuilderRefuses()
    {
        ConventionSet set = ConventionsText.OnQuantLib(
            "<SwapConvention name=\"MakeVanillaSwap.NOK\"><FixedLegTenor>1Y</FixedLegTenor><FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter></SwapConvention>");
        IborIndex Nibor() => new Nibor(new Period(6, TimeUnit.Months), Curve);

        VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), set.IborIndex("Nibor", new Period(6, TimeUnit.Months), Curve)).WithFixedRate(0.03).ToVanillaSwap(Context);

        Assert.Equal((5, new Thirty360(Thirty360Convention.BondBasis)), (swap.FixedLeg.Length, swap.FixedDayCount));
        Assert.Equal("unknown fixed leg default tenor for NOK",
            Assert.Throws<NanoQLException>(() => new MakeVanillaSwap(new Period(5, TimeUnit.Years), Nibor()).WithFixedRate(0.03).ToVanillaSwap(Context)).Message);
    }

    public static TheoryData<string> Kinds() => [.. Routes.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EachRouteBuildsWhatItsBuilderOrHelperBuildsGivenTheRecordsOptionsByHand(string route)
    {
        (Func<string> throughRecord, Func<string> byHand) = Routes[route];
        Assert.Equal(byHand(), throughRecord());
    }

    [Theory]
    [InlineData("<CdsConvention name=\"X\"><SettlementDays>1</SettlementDays></CdsConvention>", "cds", "MakeCreditDefaultSwap can't take the CdsConvention X's SettlementDays: it takes none")]
    [InlineData("<CdsConvention name=\"X\"><PricingModel>ISDA</PricingModel></CdsConvention>", "cds", "MakeCreditDefaultSwap can't take the CdsConvention X's PricingModel: it takes none")]
    [InlineData("<CdsConvention name=\"X\"><SettlementDays>1</SettlementDays><CashSettlementDays>1</CashSettlementDays></CdsConvention>", "spread",
        "SpreadCdsHelper can't take the CdsConvention X's CashSettlementDays: it uses 3, not 1")]
    [InlineData("<FxSwapConvention name=\"X\"><BaseCurrency>EUR</BaseCurrency><QuoteCurrency>USD</QuoteCurrency><Calendar>TARGET</Calendar></FxSwapConvention>", "forward",
        "FxForward can't take the FxSwapConvention X's Calendar: it takes none")]
    [InlineData("<FxSwapConvention name=\"X\"><BaseCurrency>EUR</BaseCurrency><QuoteCurrency>USD</QuoteCurrency><Convention>Following</Convention></FxSwapConvention>", "forward",
        "FxForward can't take the FxSwapConvention X's Convention: it takes none")]
    [InlineData(ZcisRecord + "<InflationCalendar>UnitedKingdom.Settlement</InflationCalendar></ZeroCouponInflationSwapConvention>", "zcis",
        "ZeroCouponInflationSwapHelper can't take the ZeroCouponInflationSwapConvention X's InflationCalendar: it uses TARGET, not UK settlement")]
    [InlineData(ZcisRecord + "<InflationConvention>Following</InflationConvention></ZeroCouponInflationSwapConvention>", "zcis",
        "ZeroCouponInflationSwapHelper can't take the ZeroCouponInflationSwapConvention X's InflationConvention: it uses ModifiedFollowing, not Following")]
    [InlineData(ZcisRecord + "<AdjustObservationDates>true</AdjustObservationDates></ZeroCouponInflationSwapConvention>", "zcis",
        "ZeroCouponInflationSwapHelper can't take the ZeroCouponInflationSwapConvention X's AdjustObservationDates: it uses False, not True")]
    [InlineData("<OisConvention name=\"X\"><Index>Euribor6M</Index></OisConvention>", "with", "OisConvention X: the index Euribor6M is no overnight index")]
    [InlineData("<SwapConvention name=\"X\"><FixedLegRule>Forward</FixedLegRule><Index>Euribor6M</Index></SwapConvention>", "swaphelper",
        "SwapRateHelper can't take the SwapConvention X's FixedLegRule: it uses Backward, not Forward")]
    [InlineData("<BondConvention name=\"X\" base=\"BTP\" />", "bond",
        "FixedRateBond can't take the BondConvention X's base: its chain reaches the BTP preset, whose class rounds its accrued amount: build BTP in the set")]
    public void ARouteRefusesAFieldItCantTakeNamingTheKindTheRecordAndTheField(string record, string route, string message)
    {
        // a record naming an index of another kind is refused when its set is built, before any route
        if (route == "with")
        {
            Assert.StartsWith(message, Assert.Throws<ArgumentException>(() => ConventionsText.OnQuantLib(record)).Message, StringComparison.Ordinal);
            return;
        }

        ConventionSet set = ConventionsText.OnQuantLib(record);
        Action build = route switch
        {
            "cds" => () => set.Cds("X").Builder(new Period(5, TimeUnit.Years), 0.01),
            "spread" => () => set.Cds("X").SpreadHelper(0.01, new Period(5, TimeUnit.Years), 0.4, Curve, Context),
            "forward" => () => set.FxSwap("X").Forward(1e6, 1.1e6, new Date(15, Month.January, 2027), true),
            "zcis" => () => set.ZeroCouponInflationSwap("X").RateHelper(0.02, new Date(15, Month.October, 2031), Context),
            "swaphelper" => () => set.Swap("X").RateHelper(0.03, new Period(5, TimeUnit.Years), Context, forwardingCurve: Curve),
            _ => () => set.Bond("X").Bond(100.0, new Date(1, Month.September, 2024), new Date(1, Month.March, 2034), [0.04]),
        };

        Assert.StartsWith(message, Assert.Throws<ArgumentException>(build).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueItsBuilderRefusesPassesTheBuildersOwnException()
    {
        ConventionSet set = ConventionsText.OnQuantLib("<MultipleResetsSwapConvention name=\"X\"><Index>Euribor3M</Index><ResetsPerCoupon>0</ResetsPerCoupon></MultipleResetsSwapConvention>");
        Assert.Equal("resetsPerCoupon", Assert.Throws<ArgumentOutOfRangeException>(() => set.MultipleResetsSwap("X").Builder(new Period(5, TimeUnit.Years), Curve)).ParamName);
    }

    [Theory]
    [InlineData("<SwapConvention name=\"MakeVanillaSwap.EUR\" base=\"MakeVanillaSwap.EUR\"><FixedLegCalendar>TARGET</FixedLegCalendar></SwapConvention>",
        "a default record holds only what its builder fixes, not FixedLegCalendar")]
    [InlineData("<OisConvention name=\"MakeOIS\" base=\"MakeOIS\"><Index>Sofr</Index></OisConvention>", "a default record holds only what its builder fixes, not Index")]
    [InlineData("<CdsConvention name=\"MakeOIS\" />", "the name is the MakeOIS builder's default record, a OisConvention")]
    [InlineData("<OisConvention name=\"MakeOIS.Sofr\" base=\"USD-SOFR\" />", "a default record takes only its own name or its builder's record as its base")]
    [InlineData("<SwapConvention name=\"X\"><FixedLegTenor>1Y</FixedLegTenor><FixedLegTenorThreshold><Comparison>AtMost</Comparison><SwapTenor>1Y</SwapTenor><FixedLegTenor>6M</FixedLegTenor></FixedLegTenorThreshold></SwapConvention>",
        "FixedLegTenorThreshold is a currency's default record's")]
    [InlineData("<ScheduleConvention name=\"X\"><Tenor>3M</Tenor><Frequency>Quarterly</Frequency></ScheduleConvention>", "Tenor and Frequency are one option")]
    [InlineData("<BondConvention name=\"BTP\" base=\"BTP\"><Redemption>99</Redemption></BondConvention>", "a default record holds only what its builder fixes, not Redemption")]
    [InlineData("<SwapConvention name=\"MakeVanillaSwap.XYZ\"><FixedLegTenor>1Y</FixedLegTenor></SwapConvention>", "unknown currency XYZ")]
    public void TheSetRefusesADefaultRecordThatBreaksItsBuildersRules(string record, string refusal)
    {
        Assert.Contains(refusal, Assert.Throws<ArgumentException>(() => ConventionsText.OnQuantLib(UsdSofr + record)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AConventionTakesANameAnAliasHasEachConventionKindItsOwnNames()
    {
        // the shipped USD-SOFR alias names an index; in one document, an alias and a convention may share a name
        ConventionSet set = ConventionsText.OnQuantLib(UsdSofr + "<Alias name=\"SOFR\" base=\"Sofr\" /><OisConvention name=\"SOFR\" base=\"USD-SOFR\" /><SwapConvention name=\"SOFR\"><Index>Euribor6M</Index></SwapConvention>");
        Assert.IsType<Sofr>(set.OvernightIndex("USD-SOFR"));
        Assert.Equal("Sofr", set.Ois("SOFR").Record.Index);
        Assert.Equal("Euribor6M", set.Swap("SOFR").Record.Index);
        Assert.IsType<Sofr>(set.OvernightIndex("SOFR"));
    }

    [Fact]
    public void AnOverrideOfAConventionsIndexReachesItsHelpers()
    {
        ConventionSet set = ConventionsText.OnQuantLib(EurSwap + "<IborFamily name=\"Euribor\" base=\"Euribor\"><FixingDays>1</FixingDays></IborFamily>");
        SwapRateHelper helper = set.Swap("EUR-SWAP").RateHelper(0.03, new Period(5, TimeUnit.Years), Context, forwardingCurve: Curve);
        Assert.Equal(1, ((IborCoupon)helper.Swap.FloatingLeg[0]).FixingDays);
    }

    public static TheoryData<string> Records() => [.. RoundTrips];

    [Theory]
    [MemberData(nameof(Records))]
    public void EachKindsRecordRoundTripsAndIsDescribedAsItsFlattenedRecord(string record)
    {
        ConventionSnapshot read = ConventionsText.Records(record);
        Assert.Equal(read, ConventionsText.Read(ConventionsText.Write(read)));

        // through Describe, the writer and the reader, a convention resolves to its own record under another name
        ConventionSet set = ConventionsText.OnQuantLib(UsdSofr + EurSwap + record);
        ConventionRecord flattened = Lookup(set, read.Records[0])!;
        ConventionSnapshot described = ConventionsText.Read(ConventionsText.Write(DescribeOf(set, read.Records[0])));
        ConventionRecord again = Lookup(set.With(described), described.Records[0])!;
        Assert.Equal(described.Records[0], again);
        Assert.Equal(ConventionsText.Write(ConventionSnapshot.Empty.Add(flattened)).Replace("\"X\"", "\"Y\"", StringComparison.Ordinal), ConventionsText.Write(described));
    }

    private const string ZcisRecord = "<ZeroCouponInflationSwapConvention name=\"X\"><Index>UKRPI</Index><ObservationLag>3M</ObservationLag><Interpolation>Flat</Interpolation>"
        + "<Calendar>TARGET</Calendar><Convention>ModifiedFollowing</Convention><DayCounter>Actual365Fixed</DayCounter>";

    private static readonly string[] RoundTrips =
    [
        "<ScheduleConvention name=\"X\"><Tenor>6M</Tenor><Calendar>TARGET</Calendar><Convention>ModifiedFollowing</Convention><Rule>Forward</Rule><EndOfMonth>true</EndOfMonth></ScheduleConvention>",
        "<SwapConvention name=\"X\" base=\"EUR-SWAP\"><FixedLegRule>Backward</FixedLegRule><FloatingLegTenor>3M</FloatingLegTenor></SwapConvention>",
        "<OisConvention name=\"X\" base=\"USD-SOFR\"><LookbackDays>2</LookbackDays><ObservationShift>true</ObservationShift></OisConvention>",
        "<CdsConvention name=\"X\"><SettlementDays>1</SettlementDays><LastPeriodDayCounter>Actual360</LastPeriodDayCounter><PricingModel>ISDA</PricingModel></CdsConvention>",
        ZcisRecord + "<AdjustObservationDates>false</AdjustObservationDates></ZeroCouponInflationSwapConvention>",
        "<YearOnYearInflationSwapConvention name=\"X\"><Index>YYUKRPI</Index><ObservationLag>3M</ObservationLag><Interpolation>Linear</Interpolation><Calendar>TARGET</Calendar></YearOnYearInflationSwapConvention>",
        "<CrossCurrencyBasisSwapConvention name=\"X\"><FixingDays>2</FixingDays><BaseCurrencyIndex>Euribor</BaseCurrencyIndex><BaseCurrencyIndexTenor>3M</BaseCurrencyIndexTenor><MarkToMarket>true</MarkToMarket><FxResetFixingCalendar>TARGET</FxResetFixingCalendar></CrossCurrencyBasisSwapConvention>",
        "<CrossCurrencySwapConvention name=\"X\"><FixedFrequency>Annual</FixedFrequency><FloatingIndex>Euribor3M</FloatingIndex><IndexedCoupons>true</IndexedCoupons></CrossCurrencySwapConvention>",
        "<FxSwapConvention name=\"X\"><BaseCurrency>EUR</BaseCurrency><QuoteCurrency>USD</QuoteCurrency><FixingDays>2</FixingDays><TradingCalendar>UnitedStates.NYSE</TradingCalendar></FxSwapConvention>",
        "<CapFloorConvention name=\"X\"><Index>Euribor6M</Index><Tenor>3M</Tenor><DayCounter>Actual365Fixed</DayCounter></CapFloorConvention>",
        "<SwaptionConvention name=\"X\"><SwapIndex>EuriborSwapIsdaFixA</SwapIndex><SettlementType>Cash</SettlementType><SettlementMethod>ParYieldCurve</SettlementMethod></SwaptionConvention>",
        "<CmsConvention name=\"X\"><SwapIndex>EuriborSwapIsdaFixA</SwapIndex><SwapIndexTenor>10Y</SwapIndexTenor><CmsLegTenor>6M</CmsLegTenor><FloatingLegIndex>Euribor</FloatingLegIndex><FloatingLegIndexTenor>6M</FloatingLegIndexTenor></CmsConvention>",
        "<MultipleResetsSwapConvention name=\"X\"><Index>Euribor3M</Index><ResetsPerCoupon>4</ResetsPerCoupon><Averaging>Simple</Averaging></MultipleResetsSwapConvention>",
        "<YoYInflationCapFloorConvention name=\"X\"><Index>YYEUHICP</Index><ObservationLag>3M</ObservationLag><Interpolation>Flat</Interpolation><Calendar>TARGET</Calendar><FixingDays>2</FixingDays></YoYInflationCapFloorConvention>",
        "<BondConvention name=\"X\"><SettlementDays>1</SettlementDays><Frequency>Semiannual</Frequency><Redemption>100.5</Redemption><ExCouponPeriod>7D</ExCouponPeriod><PriceType>Dirty</PriceType></BondConvention>",
    ];

    private static ConventionRecord? Lookup(ConventionSet set, ConventionRecord r) => r switch
    {
        ScheduleConventionRecord => set.Schedule(r.Name).Record,
        SwapConventionRecord => set.Swap(r.Name).Record,
        OisConventionRecord => set.Ois(r.Name).Record,
        CdsConventionRecord => set.Cds(r.Name).Record,
        ZeroCouponInflationSwapConventionRecord => set.ZeroCouponInflationSwap(r.Name).Record,
        YearOnYearInflationSwapConventionRecord => set.YearOnYearInflationSwap(r.Name).Record,
        CrossCurrencyBasisSwapConventionRecord => set.CrossCurrencyBasisSwap(r.Name).Record,
        CrossCurrencySwapConventionRecord => set.CrossCurrencySwap(r.Name).Record,
        FxSwapConventionRecord => set.FxSwap(r.Name).Record,
        CapFloorConventionRecord => set.CapFloor(r.Name).Record,
        SwaptionConventionRecord => set.Swaption(r.Name).Record,
        CmsConventionRecord => set.Cms(r.Name).Record,
        MultipleResetsSwapConventionRecord => set.MultipleResetsSwap(r.Name).Record,
        YoYInflationCapFloorConventionRecord => set.YoYInflationCapFloor(r.Name).Record,
        BondConventionRecord => set.Bond(r.Name).Record,
        _ => null,
    };

    private static ConventionSnapshot DescribeOf(ConventionSet set, ConventionRecord r) => r switch
    {
        ScheduleConventionRecord => ConventionRecords.Describe("Y", set.Schedule(r.Name)),
        SwapConventionRecord => ConventionRecords.Describe("Y", set.Swap(r.Name)),
        OisConventionRecord => ConventionRecords.Describe("Y", set.Ois(r.Name)),
        CdsConventionRecord => ConventionRecords.Describe("Y", set.Cds(r.Name)),
        ZeroCouponInflationSwapConventionRecord => ConventionRecords.Describe("Y", set.ZeroCouponInflationSwap(r.Name)),
        YearOnYearInflationSwapConventionRecord => ConventionRecords.Describe("Y", set.YearOnYearInflationSwap(r.Name)),
        CrossCurrencyBasisSwapConventionRecord => ConventionRecords.Describe("Y", set.CrossCurrencyBasisSwap(r.Name)),
        CrossCurrencySwapConventionRecord => ConventionRecords.Describe("Y", set.CrossCurrencySwap(r.Name)),
        FxSwapConventionRecord => ConventionRecords.Describe("Y", set.FxSwap(r.Name)),
        CapFloorConventionRecord => ConventionRecords.Describe("Y", set.CapFloor(r.Name)),
        SwaptionConventionRecord => ConventionRecords.Describe("Y", set.Swaption(r.Name)),
        CmsConventionRecord => ConventionRecords.Describe("Y", set.Cms(r.Name)),
        MultipleResetsSwapConventionRecord => ConventionRecords.Describe("Y", set.MultipleResetsSwap(r.Name)),
        YoYInflationCapFloorConventionRecord => ConventionRecords.Describe("Y", set.YoYInflationCapFloor(r.Name)),
        _ => ConventionRecords.Describe("Y", set.Bond(r.Name)),
    };

    private static string Seen(params object?[] values) => Fingerprint.Of(values, Context);

    private static double Npv(Swap swap) => DiscountingSwapEngine.Calculate(swap, Context, Curve).NPV!.Value;

    /// <summary>Gets a convention record of each kind, the routes' facts', which the walk builds through too.</summary>
    internal static string Conventions { get; } =
        UsdSofr + EurSwap
        + "<OisConvention name=\"SOFR-LB\" base=\"USD-SOFR\"><LookbackDays>2</LookbackDays><ObservationShift>true</ObservationShift></OisConvention>"
        + "<ScheduleConvention name=\"S\"><Frequency>Quarterly</Frequency><Calendar>TARGET</Calendar><Convention>ModifiedFollowing</Convention><Rule>Forward</Rule></ScheduleConvention>"
        + "<CdsConvention name=\"CDS\"><CouponFrequency>Quarterly</CouponFrequency><Rule>CDS2015</Rule></CdsConvention>"
        + "<CdsConvention name=\"CDSH\" base=\"CDS\"><SettlementDays>1</SettlementDays></CdsConvention>"
        + "<ZeroCouponInflationSwapConvention name=\"ZC\"><Index>UKRPI</Index><ObservationLag>3M</ObservationLag><Interpolation>Flat</Interpolation><Calendar>UnitedKingdom.Settlement</Calendar>"
        + "<Convention>ModifiedFollowing</Convention><DayCounter>Actual365Fixed</DayCounter></ZeroCouponInflationSwapConvention>"
        + "<YearOnYearInflationSwapConvention name=\"YY\"><Index>YYUKRPI</Index><ObservationLag>3M</ObservationLag><Interpolation>Flat</Interpolation><Calendar>UnitedKingdom.Settlement</Calendar>"
        + "<PaymentConvention>ModifiedFollowing</PaymentConvention><DayCounter>Actual365Fixed</DayCounter></YearOnYearInflationSwapConvention>"
        + "<CrossCurrencyBasisSwapConvention name=\"XB\"><FixingDays>2</FixingDays><Calendar>TARGET</Calendar><Convention>ModifiedFollowing</Convention><EndOfMonth>false</EndOfMonth>"
        + "<BaseCurrencyIndex>Euribor3M</BaseCurrencyIndex><QuoteCurrencyIndex>USDLibor</QuoteCurrencyIndex><QuoteCurrencyIndexTenor>3M</QuoteCurrencyIndexTenor>"
        + "<CollateralOnBaseCurrency>false</CollateralOnBaseCurrency><BasisOnBaseCurrencyLeg>true</BasisOnBaseCurrencyLeg></CrossCurrencyBasisSwapConvention>"
        + "<CrossCurrencySwapConvention name=\"XS\"><FixingDays>2</FixingDays><Calendar>TARGET</Calendar><Convention>ModifiedFollowing</Convention><EndOfMonth>false</EndOfMonth>"
        + "<FixedFrequency>Annual</FixedFrequency><FixedDayCounter>Thirty360.BondBasis</FixedDayCounter><FloatingIndex>Euribor3M</FloatingIndex><CollateralOnFixedLeg>false</CollateralOnFixedLeg></CrossCurrencySwapConvention>"
        + "<FxSwapConvention name=\"FX\"><FixingDays>2</FixingDays><Calendar>TARGET</Calendar><Convention>ModifiedFollowing</Convention><EndOfMonth>false</EndOfMonth>"
        + "<CollateralOnBaseCurrency>false</CollateralOnBaseCurrency><TradingCalendar>UnitedStates.NYSE</TradingCalendar></FxSwapConvention>"
        + "<FxSwapConvention name=\"FXF\"><BaseCurrency>EUR</BaseCurrency><QuoteCurrency>USD</QuoteCurrency><FixingDays>2</FixingDays><PaymentCalendar>TARGET</PaymentCalendar></FxSwapConvention>"
        + "<CapFloorConvention name=\"CAP\"><Index>Euribor6M</Index><Rule>Forward</Rule></CapFloorConvention>"
        + "<SwaptionConvention name=\"SWO\"><SwapIndex>EuriborSwapIsdaFixA</SwapIndex><SettlementType>Cash</SettlementType><SettlementMethod>ParYieldCurve</SettlementMethod></SwaptionConvention>"
        + "<CmsConvention name=\"CMS\"><SwapIndex>EuriborSwapIsdaFixA</SwapIndex><SwapIndexTenor>10Y</SwapIndexTenor><CmsLegTenor>6M</CmsLegTenor></CmsConvention>"
        + "<MultipleResetsSwapConvention name=\"MRS\"><Index>Euribor3M</Index><ResetsPerCoupon>4</ResetsPerCoupon><Averaging>Simple</Averaging></MultipleResetsSwapConvention>"
        + "<YoYInflationCapFloorConvention name=\"YYCAP\"><Index>YYUKRPI</Index><ObservationLag>3M</ObservationLag><Interpolation>Flat</Interpolation><Calendar>UnitedKingdom.Settlement</Calendar>"
        + "<FixingDays>2</FixingDays></YoYInflationCapFloorConvention>"
        + "<BondConvention name=\"UST\"><SettlementDays>1</SettlementDays><Frequency>Semiannual</Frequency><Calendar>UnitedStates.GovernmentBond</Calendar><Convention>Unadjusted</Convention>"
        + "<AccrualDayCounter>ActualActual.ISMA</AccrualDayCounter></BondConvention>"
        + "<BondConvention name=\"USTH\" base=\"UST\"><PriceType>Dirty</PriceType></BondConvention>";

    private static readonly ConventionSet Set = ConventionsText.OnQuantLib(Conventions);

    // Each route through its record, and its builder or helper given the record's options by hand.
    private static readonly Dictionary<string, (Func<string> Record, Func<string> ByHand)> Routes = new(StringComparer.Ordinal)
    {
        ["Schedule"] = (
            () => Seen(Set.Schedule("S").Builder().From(new Date(15, Month.October, 2026)).To(new Date(15, Month.October, 2031)).ToSchedule()),
            () => Seen(new MakeSchedule().From(new Date(15, Month.October, 2026)).To(new Date(15, Month.October, 2031)).WithFrequency(Frequency.Quarterly)
                .WithCalendar(new TARGET()).WithConvention(BusinessDayConvention.ModifiedFollowing).WithRule(DateGenerationRule.Forward).ToSchedule())),
        ["Swap builder"] = (
            () => SwapSeen(Set.Swap("EUR-SWAP").Builder(new Period(5, TimeUnit.Years), Curve).WithFixedRate(0.03).ToVanillaSwap(Context)),
            () => SwapSeen(new MakeVanillaSwap(new Period(5, TimeUnit.Years), Set.IborIndex("Euribor", new Period(6, TimeUnit.Months), Curve)).WithSettlementDays(2)
                .WithFixedLegTenor(new Period(1, TimeUnit.Years)).WithFixedLegConvention(BusinessDayConvention.ModifiedFollowing)
                .WithFixedLegDayCount(new Thirty360(Thirty360Convention.BondBasis)).WithFixedRate(0.03).ToVanillaSwap(Context))),
        ["Swap helper"] = (
            () => HelperSeen(Set.Swap("EUR-SWAP").RateHelper(0.03, new Period(5, TimeUnit.Years), Context, forwardingCurve: Curve)),
            () => HelperSeen(new SwapRateHelper(0.03, new Period(5, TimeUnit.Years), new TARGET(), Frequency.Annual, BusinessDayConvention.ModifiedFollowing,
                new Thirty360(Thirty360Convention.BondBasis), Set.IborIndex("Euribor", new Period(6, TimeUnit.Months), Curve), Context, settlementDays: 2))),
        ["OIS builder"] = (
            () => SwapSeen(Set.Ois("USD-SOFR").Builder(new Period(5, TimeUnit.Years), Curve).WithFixedRate(0.03).ToOvernightIndexedSwap(Context)),
            () => SwapSeen(new MakeOIS(new Period(5, TimeUnit.Years), Set.OvernightIndex("Sofr", Curve)).WithSettlementDays(2).WithPaymentLag(2)
                .WithPaymentFrequency(Frequency.Annual).WithPaymentCalendar(new UnitedStates(UnitedStatesMarket.GovernmentBond)).WithFixedLegDayCount(new Actual360())
                .WithFixedRate(0.03).ToOvernightIndexedSwap(Context))),
        ["OIS builder with a lookback"] = (
            () => SwapSeen(Set.Ois("SOFR-LB").Builder(new Period(5, TimeUnit.Years), Curve).WithFixedRate(0.03).ToOvernightIndexedSwap(Context)),
            () => SwapSeen(new MakeOIS(new Period(5, TimeUnit.Years), Set.OvernightIndex("Sofr", Curve)).WithSettlementDays(2).WithPaymentLag(2)
                .WithPaymentFrequency(Frequency.Annual).WithPaymentCalendar(new UnitedStates(UnitedStatesMarket.GovernmentBond)).WithFixedLegDayCount(new Actual360())
                .WithLookbackDays(2).WithObservationShift(true).WithFixedRate(0.03).ToOvernightIndexedSwap(Context))),
        ["OIS helper, USD SOFR"] = (
            () => HelperSeen(Set.Ois("USD-SOFR").RateHelper(0.03, new Period(5, TimeUnit.Years), Context, forwardingCurve: Curve)),
            () => HelperSeen(new OISRateHelper(2, new Period(5, TimeUnit.Years), 0.03, Set.OvernightIndex("Sofr", Curve), Context, paymentLag: 2,
                paymentConvention: BusinessDayConvention.Following, paymentFrequency: Frequency.Annual, paymentCalendar: new UnitedStates(UnitedStatesMarket.GovernmentBond),
                fixedPaymentFrequency: Frequency.Annual, fixedDayCount: new Actual360()))),
        ["CDS builder"] = (
            () => Seen(Set.Cds("CDS").Builder(new Period(5, TimeUnit.Years), 0.01).ToCreditDefaultSwap(Context)),
            () => Seen(new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01).WithCouponTenor(new Period(3, TimeUnit.Months))
                .WithDateGenerationRule(DateGenerationRule.CDS2015).ToCreditDefaultSwap(Context))),
        ["CDS spread helper"] = (
            () => Seen(Set.Cds("CDSH").SpreadHelper(0.01, new Period(5, TimeUnit.Years), 0.4, Curve, Context).SetTermStructure(Hazard)),
            () => Seen(new SpreadCdsHelper(0.01, new Period(5, TimeUnit.Years), 1, new WeekendsOnly(), Frequency.Quarterly, BusinessDayConvention.Following,
                DateGenerationRule.CDS2015, new Actual360(), 0.4, Curve, Context, lastPeriodDayCounter: new Actual360(true)).SetTermStructure(Hazard))),
        ["CDS upfront helper"] = (
            () => Seen(Set.Cds("CDSH").UpfrontHelper(0.02, 0.01, new Period(5, TimeUnit.Years), 0.4, Curve, Context).SetTermStructure(Hazard)),
            () => Seen(new UpfrontCdsHelper(0.02, 0.01, new Period(5, TimeUnit.Years), 1, new WeekendsOnly(), Frequency.Quarterly, BusinessDayConvention.Following,
                DateGenerationRule.CDS2015, new Actual360(), 0.4, Curve, Context, lastPeriodDayCounter: new Actual360(true)).SetTermStructure(Hazard))),
        ["Zero-coupon inflation swap"] = (
            () => Seen(Set.ZeroCouponInflationSwap("ZC").Swap(SwapType.Payer, 1e6, new Date(19, Month.October, 2026), new Date(19, Month.October, 2031), 0.02)),
            () => Seen(new ZeroCouponInflationSwap(SwapType.Payer, 1e6, new Date(19, Month.October, 2026), new Date(19, Month.October, 2031),
                new UnitedKingdom(UnitedKingdomMarket.Settlement), BusinessDayConvention.ModifiedFollowing, new Actual365Fixed(), 0.02, new UKRPI(),
                new Period(3, TimeUnit.Months), CPIInterpolationType.Flat))),
        ["Zero-coupon inflation swap helper"] = (
            () => Seen(Set.ZeroCouponInflationSwap("ZC").RateHelper(0.02, new Date(19, Month.October, 2031), Context)),
            () => Seen(new ZeroCouponInflationSwapHelper(0.02, new Period(3, TimeUnit.Months), new Date(19, Month.October, 2031), new UnitedKingdom(UnitedKingdomMarket.Settlement),
                BusinessDayConvention.ModifiedFollowing, new Actual365Fixed(), new UKRPI(), CPIInterpolationType.Flat, Context))),
        ["Year-on-year inflation swap helper"] = (
            () => Seen(Set.YearOnYearInflationSwap("YY").RateHelper(0.02, new Date(19, Month.October, 2031), Curve, Context)),
            () => Seen(new YearOnYearInflationSwapHelper(0.02, new Period(3, TimeUnit.Months), new Date(19, Month.October, 2031), new UnitedKingdom(UnitedKingdomMarket.Settlement),
                BusinessDayConvention.ModifiedFollowing, new Actual365Fixed(), new YYUKRPI(), CPIInterpolationType.Flat, Curve, Context))),
        ["Cross-currency basis swap helper"] = (
            () => HelperSeen(Set.CrossCurrencyBasisSwap("XB").RateHelper(0.001, new Period(5, TimeUnit.Years), Curve, Context, Curve, Curve)),
            () => HelperSeen(new ConstNotionalCrossCurrencyBasisSwapRateHelper(0.001, new Period(5, TimeUnit.Years), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing,
                false, Set.IborIndex("Euribor3M", Curve), Set.IborIndex("USDLibor", new Period(3, TimeUnit.Months), Curve), Curve, false, true, Context))),
        ["Cross-currency swap helper"] = (
            () => HelperSeen(Set.CrossCurrencySwap("XS").RateHelper(0.02, new Period(5, TimeUnit.Years), Curve, Context, Curve)),
            () => HelperSeen(new ConstNotionalCrossCurrencySwapRateHelper(0.02, new Period(5, TimeUnit.Years), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
                Frequency.Annual, new Thirty360(Thirty360Convention.BondBasis), Set.IborIndex("Euribor3M", Curve), Curve, false, Context))),
        ["FX swap helper"] = (
            () => HelperSeen(Set.FxSwap("FX").RateHelper(0.001, 1.1, new Period(3, TimeUnit.Months), Curve, Context)),
            () => HelperSeen(new FxSwapRateHelper(0.001, 1.1, new Period(3, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false, false, Curve, Context,
                new UnitedStates(UnitedStatesMarket.NYSE)))),
        ["FX forward"] = (
            () => Seen(Set.FxSwap("FXF").Forward(1e6, 1.1e6, new Date(15, Month.January, 2027), true)),
            () => Seen(new FxForward(1e6, Set.Currency("EUR"), 1.1e6, Set.Currency("USD"), new Date(15, Month.January, 2027), true, 2, new TARGET()))),
        ["Cap"] = (
            () => Seen(Set.CapFloor("CAP").Builder(CapFloorType.Cap, new Period(5, TimeUnit.Years), Curve).WithStrike(0.03).ToCapFloor(Context)),
            () => Seen(new MakeCapFloor(CapFloorType.Cap, new Period(5, TimeUnit.Years), Set.IborIndex("Euribor6M", Curve)).WithRule(DateGenerationRule.Forward)
                .WithStrike(0.03).ToCapFloor(Context))),
        ["Swaption"] = (
            () => Seen(Set.Swaption("SWO").Builder(new Period(1, TimeUnit.Years), 0.03, new Period(5, TimeUnit.Years), Curve).ToSwaption(Context)),
            () => Seen(new MakeSwaption(Set.SwapIndex("EuriborSwapIsdaFixA", new Period(5, TimeUnit.Years), Curve), new Period(1, TimeUnit.Years), 0.03)
                .WithSettlementType(SettlementType.Cash).WithSettlementMethod(SettlementMethod.ParYieldCurve).ToSwaption(Context))),
        ["CMS swap"] = (
            () => Seen(Set.Cms("CMS").Builder(new Period(5, TimeUnit.Years), Curve).ToSwap(Context)),
            () => Seen(new MakeCms(new Period(5, TimeUnit.Years), Set.SwapIndex("EuriborSwapIsdaFixA", new Period(10, TimeUnit.Years), Curve))
                .WithCmsLegTenor(new Period(6, TimeUnit.Months)).ToSwap(Context))),
        ["Multiple-resets swap"] = (
            () => SwapSeen(Set.MultipleResetsSwap("MRS").Builder(new Period(5, TimeUnit.Years), Curve).WithFixedRate(0.03).ToSwap(Context)),
            () => SwapSeen(new MakeMultipleResetsSwap(new Period(5, TimeUnit.Years), Set.IborIndex("Euribor3M", Curve), 4).WithAveragingMethod(RateAveragingType.Simple)
                .WithFixedRate(0.03).ToSwap(Context))),
        ["Year-on-year inflation cap"] = (
            () => Seen(Set.YoYInflationCapFloor("YYCAP").Builder(YoYInflationCapFloorType.Cap, 5).WithStrike(0.02).ToYoYInflationCapFloor(Context)),
            () => Seen(new MakeYoYInflationCapFloor(YoYInflationCapFloorType.Cap, Set.YoYInflationIndex("YYUKRPI"), 5, new UnitedKingdom(UnitedKingdomMarket.Settlement),
                new Period(3, TimeUnit.Months), CPIInterpolationType.Flat).WithFixingDays(2).WithStrike(0.02).ToYoYInflationCapFloor(Context))),
        ["Bond"] = (
            () => Seen(Set.Bond("UST").Bond(100.0, new Date(15, Month.May, 2025), new Date(15, Month.May, 2035), [0.04])),
            () => Seen(new FixedRateBond(1, 100.0, UstSchedule(), [0.04], new ActualActual(ActualActualConvention.ISMA), issueDate: new Date(15, Month.May, 2025)))),
        ["Bond helper"] = (
            () => HelperSeen(Set.Bond("USTH").RateHelper(100.0, new Date(15, Month.May, 2025), new Date(15, Month.May, 2035), [0.04], Context)),
            () => HelperSeen(new FixedRateBondHelper(100.0, 1, 100.0, UstSchedule(), [0.04], new ActualActual(ActualActualConvention.ISMA), Context,
                issueDate: new Date(15, Month.May, 2025), priceType: BondPriceType.Dirty))),
    };

    private static Schedule UstSchedule() =>
        new MakeSchedule().From(new Date(15, Month.May, 2025)).To(new Date(15, Month.May, 2035)).WithFrequency(Frequency.Semiannual)
            .WithCalendar(new UnitedStates(UnitedStatesMarket.GovernmentBond)).WithConvention(BusinessDayConvention.Unadjusted).ToSchedule();

    private static string SwapSeen(Swap swap) => Seen(swap, Npv(swap));

    // A yield helper's built instrument and dates, and its implied quote and pillar on the flat curve.
    private static string HelperSeen(RateHelper helper) => Seen(helper, helper.SetTermStructure(Curve).ImpliedQuote(Context), helper.PillarDate);
}
