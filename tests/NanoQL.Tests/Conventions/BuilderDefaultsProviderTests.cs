// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Tests.Extensions;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The market builders read the defaults QuantLib's builders fix from their provider when they build, once an option
/// needs one: with nothing set, QuantLib's values, so each builds what it builds given them explicitly; on an index
/// built in a set, the set's default records, after the set's scope is left; the CDS builder's, the provider current
/// when it was created. <see cref="BTP"/> reads its conventions as a preset does.
/// </summary>
public class BuilderDefaultsProviderTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.October, 2026) };
    private static readonly FlatForward Curve = new(new Date(15, Month.October, 2026), 0.03, new Actual365Fixed());

    public static TheoryData<string> Builders() => [.. Explicit.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Builders))]
    public void WithNothingSetEachBuilderBuildsWhatItBuildsGivenQuantLibsValues(string builder)
    {
        (Func<object> bare, Func<object> explicitly) = Explicit[builder];
        Assert.Equal(Fingerprint.Of(explicitly(), Context), Fingerprint.Of(bare(), Context));
    }

    [Fact]
    public void EachBuilderReadsItsDefaultsOnceFromItsIndexsProviderWhenItBuilds()
    {
        var p = new TestConventions("p");
        IborIndex euribor = Under(p, () => new Euribor6M(Curve));
        OvernightIndex sonia = Under(p, () => new Sonia(Curve));
        SwapIndex swapIndex = Under(p, () => new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve));
        YoYInflationIndex yoy = Under(p, () => new YYEUHICP());
        MakeCreditDefaultSwap cds = Under(p, () => new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01));

        // built outside the provider's scope, each reads it through its index, or the CDS builder its own
        _ = new MakeVanillaSwap(new Period(5, TimeUnit.Years), euribor).WithFixedRate(0.03).ToVanillaSwap(Context);
        _ = new MakeOIS(new Period(5, TimeUnit.Years), sonia).WithFixedRate(0.03).ToOvernightIndexedSwap(Context);
        _ = cds.ToCreditDefaultSwap(Context);
        _ = new MakeCapFloor(CapFloorType.Cap, new Period(5, TimeUnit.Years), euribor).WithStrike(0.03).ToCapFloor(Context);
        _ = new MakeSwaption(swapIndex, new Period(1, TimeUnit.Years), 0.03).ToSwaption(Context);
        _ = new MakeCms(new Period(5, TimeUnit.Years), swapIndex).ToSwap(Context);
        _ = new MakeMultipleResetsSwap(new Period(5, TimeUnit.Years), Under(p, () => new Euribor3M(Curve)), 2).WithFixedRate(0.03).ToSwap(Context);
        _ = new MakeYoYInflationCapFloor(YoYInflationCapFloorType.Cap, yoy, 5, new TARGET(), new Period(3, TimeUnit.Months), CPIInterpolationType.Flat)
            .WithStrike(0.02).ToYoYInflationCapFloor(Context);

        string[] reads = [.. p.Resolved.Where(r => !r.StartsWith("calendar:", StringComparison.Ordinal) && !r.StartsWith("currency:", StringComparison.Ordinal)
            && !r.StartsWith("family:", StringComparison.Ordinal) && !r.StartsWith("overnight:", StringComparison.Ordinal)
            && !r.StartsWith("swap:", StringComparison.Ordinal) && !r.StartsWith("inflation:", StringComparison.Ordinal)
            && !r.StartsWith("region:", StringComparison.Ordinal) && !r.StartsWith("ibor:", StringComparison.Ordinal))];

        // each builder reads its own once; the cap's leg and the swaption's underlying are swaps the same provider's
        // swap builder builds, so it reads the EUR swap defaults too
        Assert.Equal(
            ["capfloor", "cds", "cms", "multipleresets", "ois:Sonia", "swaption", "vanilla:EUR", "yoycapfloor"],
            reads.Distinct().Order(StringComparer.Ordinal));
        Assert.All(new[] { "capfloor", "cds", "cms", "multipleresets", "ois:Sonia", "swaption", "yoycapfloor" }, r => Assert.Equal(1, reads.Count(x => x == r)));
    }

    [Fact]
    public void ABuilderGivenEveryOptionItsDefaultsGiveReadsNone()
    {
        var p = new TestConventions("p");
        IborIndex euribor = Under(p, () => new Euribor6M(Curve));
        _ = AllGiven(new MakeVanillaSwap(new Period(5, TimeUnit.Years), euribor)).ToVanillaSwap(Context);
        Assert.DoesNotContain(p.Resolved, r => r.StartsWith("vanilla:", StringComparison.Ordinal));

        _ = new MakeVanillaSwap(new Period(5, TimeUnit.Years), euribor).WithFixedRate(0.03).ToVanillaSwap(Context);
        Assert.Contains("vanilla:EUR", p.Resolved);
    }

    [Fact]
    public void ASetsSwapDefaultsReachABuilderOnAnIndexBuiltInTheSetAfterItsScopeIsLeft()
    {
        ConventionSet set = ConventionsText.OnQuantLib(
            "<SwapConvention name=\"MakeVanillaSwap.EUR\" base=\"MakeVanillaSwap.EUR\"><FixedLegTenor>6M</FixedLegTenor></SwapConvention>");
        IborIndex index = set.IborIndex("Euribor6M", Curve);

        VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), index).WithFixedRate(0.03).ToVanillaSwap(Context);
        VanillaSwap coded = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(Curve)).WithFixedRate(0.03).ToVanillaSwap(Context);

        Assert.Equal(10, swap.FixedLeg.Length);
        Assert.Equal(5, coded.FixedLeg.Length);
    }

    [Fact]
    public void ASwapIndexBuiltInTheSetForecastsItsCmsRateWithTheSetsRuleAndEndOfMonth()
    {
        // the swap index's underlying swap takes its rules and end of month from the set's EUR defaults
        ConventionSet set = ConventionsText.OnQuantLib(
            "<SwapConvention name=\"MakeVanillaSwap.EUR\" base=\"MakeVanillaSwap.EUR\"><FixedLegRule>Forward</FixedLegRule><FixedLegEndOfMonth>true</FixedLegEndOfMonth>"
            + "<FloatingLegRule>Forward</FloatingLegRule><FloatingLegEndOfMonth>true</FloatingLegEndOfMonth></SwapConvention>");
        var tenor = new Period(10, TimeUnit.Years);
        SwapIndex index = set.SwapIndex("EuriborSwapIsdaFixA", tenor, Curve);
        var coded = new EuriborSwapIsdaFixA(tenor, Curve);
        // a value date at a month end, where the floating leg's end of month shows
        Date fixing = new(25, Month.February, 2028);

        VanillaSwap swap = index.UnderlyingSwap(fixing, Context);
        Schedule codedSchedule = coded.UnderlyingSwap(fixing, Context).FixedSchedule;
        Assert.Equal((DateGenerationRule.Forward, true), (swap.FixedSchedule.Rule(), swap.FixedSchedule.EndOfMonth()));
        Assert.Equal((DateGenerationRule.Backward, false), (codedSchedule.Rule(), codedSchedule.EndOfMonth()));
        Assert.NotEqual(coded.Fixing(fixing, Context, true), index.Fixing(fixing, Context, true));
    }

    [Fact]
    public void TheCdsBuildersCalendarFollowsTheProviderCurrentWhenItWasCreated()
    {
        // a holiday on the upfront date, the trade date plus three business days on the defaults' calendar
        Date upfront = new(20, Month.October, 2026);
        var p = new TestConventions("p") { CdsDefaults = d => d with { Calendar = new WeekendsOnly().AddHoliday(upfront) } };
        MakeCreditDefaultSwap builder = Under(p, () => new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01));

        CreditDefaultSwap cds = builder.ToCreditDefaultSwap(Context);
        CreditDefaultSwap coded = new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01).ToCreditDefaultSwap(Context);

        Assert.Equal(upfront, coded.UpfrontPayment.Date);
        Assert.Equal(new Date(21, Month.October, 2026), cds.UpfrontPayment.Date);
        Assert.Contains("cds", p.Resolved);
    }

    [Fact]
    public void BtpReadsItsConventionsAsAPreset()
    {
        var p = new TestConventions("p") { Bonds = new Dictionary<string, Func<BondConventions, BondConventions>> { ["BTP"] = c => c with { SettlementDays = 3 } } };
        BTP bond = Under(p, () => new BTP(new Date(1, Month.March, 2034), 0.0395, Context, new Date(1, Month.September, 2024)));

        Assert.Equal(3, bond.SettlementDays);
        Assert.Equal(2, new BTP(new Date(1, Month.March, 2034), 0.0395, Context, new Date(1, Month.September, 2024)).SettlementDays);
        Assert.Equal(["bond:BTP"], p.Resolved.Where(r => r.StartsWith("bond:", StringComparison.Ordinal)));
    }

    [Fact]
    public void ASetWithoutQuantLibsPresetsRefusesABuilderWhoseDefaultRecordItDoesntDefine()
    {
        // an index of the set's own, and no MakeVanillaSwap.EUR or MakeOIS record
        ConventionSet set = ConventionSet.Empty.With(ConventionsText.Records(
            "<IborIndex name=\"Desk6M\"><Family>Desk</Family><Tenor>6M</Tenor><FixingDays>2</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar>"
            + "<Convention>ModifiedFollowing</Convention><EndOfMonth>false</EndOfMonth><DayCounter>Actual360</DayCounter></IborIndex>"
            + "<OvernightIndex name=\"DeskON\"><Family>DeskON</Family><FixingDays>0</FixingDays><Currency>EUR</Currency><FixingCalendar>TARGET</FixingCalendar><DayCounter>Actual360</DayCounter></OvernightIndex>"));

        var swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), set.IborIndex("Desk6M", Curve)).WithFixedRate(0.03);
        var ois = new MakeOIS(new Period(5, TimeUnit.Years), set.OvernightIndex("DeskON", Curve)).WithFixedRate(0.03);

        Assert.Equal("the set defines no MakeVanillaSwap.EUR, and isn't built on QuantLib's presets (Parameter 'preset')",
            Assert.Throws<ArgumentException>(() => swap.ToVanillaSwap(Context)).Message);
        Assert.Equal("the set defines no MakeOIS, and isn't built on QuantLib's presets (Parameter 'preset')",
            Assert.Throws<ArgumentException>(() => ois.ToOvernightIndexedSwap(Context)).Message);
        _ = AllGiven(new MakeVanillaSwap(new Period(5, TimeUnit.Years), set.IborIndex("Desk6M", Curve))).ToVanillaSwap(Context);
    }

    // Every option the swap builder's defaults give, QuantLib's EUR values.
    private static MakeVanillaSwap AllGiven(MakeVanillaSwap b) =>
        b.WithFixedRate(0.03)
            .WithFixedLegTenor(new Period(1, TimeUnit.Years))
            .WithFixedLegDayCount(new Thirty360(Thirty360Convention.BondBasis))
            .WithFixedLegConvention(BusinessDayConvention.ModifiedFollowing)
            .WithFixedLegTerminationDateConvention(BusinessDayConvention.ModifiedFollowing)
            .WithFixedLegRule(DateGenerationRule.Backward)
            .WithFixedLegEndOfMonth(false)
            .WithFloatingLegRule(DateGenerationRule.Backward)
            .WithFloatingLegEndOfMonth(false);

    private static T Under<T>(ConventionProvider provider, Func<T> build)
    {
        using (ConventionProvider.Use(provider))
        {
            return build();
        }
    }

    // Each builder bare, and given QuantLib's values for every default explicitly.
    private static readonly Dictionary<string, (Func<object> Bare, Func<object> Explicitly)> Explicit = new(StringComparer.Ordinal)
    {
        ["MakeVanillaSwap.EUR"] = (
            () => new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(Curve)).WithFixedRate(0.03).ToVanillaSwap(Context),
            () => AllGiven(new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(Curve))).ToVanillaSwap(Context)),
        ["MakeVanillaSwap.GBP at 1Y and 2Y"] = (
            () => new object[]
            {
                new MakeVanillaSwap(new Period(1, TimeUnit.Years), new GBPLibor(new Period(6, TimeUnit.Months), Curve)).WithFixedRate(0.03).ToVanillaSwap(Context),
                new MakeVanillaSwap(new Period(2, TimeUnit.Years), new GBPLibor(new Period(6, TimeUnit.Months), Curve)).WithFixedRate(0.03).ToVanillaSwap(Context),
            },
            () => new object[]
            {
                new MakeVanillaSwap(new Period(1, TimeUnit.Years), new GBPLibor(new Period(6, TimeUnit.Months), Curve)).WithFixedRate(0.03)
                    .WithFixedLegTenor(new Period(1, TimeUnit.Years)).WithFixedLegDayCount(new Actual365Fixed()).ToVanillaSwap(Context),
                new MakeVanillaSwap(new Period(2, TimeUnit.Years), new GBPLibor(new Period(6, TimeUnit.Months), Curve)).WithFixedRate(0.03)
                    .WithFixedLegTenor(new Period(6, TimeUnit.Months)).WithFixedLegDayCount(new Actual365Fixed()).ToVanillaSwap(Context),
            }),
        ["MakeOIS"] = (
            () => new MakeOIS(new Period(5, TimeUnit.Years), new Sonia(Curve)).WithFixedRate(0.03).ToOvernightIndexedSwap(Context),
            () => new MakeOIS(new Period(5, TimeUnit.Years), new Sonia(Curve)).WithFixedRate(0.03).WithSettlementDays(0)
                .WithPaymentFrequency(Frequency.Annual).WithPaymentAdjustment(BusinessDayConvention.Following).WithPaymentLag(0)
                .WithConvention(BusinessDayConvention.ModifiedFollowing).WithTerminationDateConvention(BusinessDayConvention.ModifiedFollowing)
                .WithRule(DateGenerationRule.Backward).WithTelescopicValueDates(false).WithAveragingMethod(RateAveragingType.Compound)
                .WithLockoutDays(0).WithObservationShift(false).ToOvernightIndexedSwap(Context)),
        ["MakeCreditDefaultSwap"] = (
            () => new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01).ToCreditDefaultSwap(Context),
            () => new MakeCreditDefaultSwap(new Period(5, TimeUnit.Years), 0.01).WithCouponTenor(new Period(3, TimeUnit.Months))
                .WithDateGenerationRule(DateGenerationRule.CDS).WithConvention(BusinessDayConvention.Following).WithDayCounter(new Actual360())
                .WithLastPeriodDayCounter(new Actual360(true)).SettleAccrual(true).PayAtDefaultTime(true).RebateAccrual(true)
                .WithCashSettlementDays(3).ToCreditDefaultSwap(Context)),
        ["MakeCapFloor"] = (
            () => new MakeCapFloor(CapFloorType.Cap, new Period(5, TimeUnit.Years), new Euribor6M(Curve)).WithStrike(0.03).ToCapFloor(Context),
            () => new MakeCapFloor(CapFloorType.Cap, new Period(5, TimeUnit.Years), new Euribor6M(Curve)).WithStrike(0.03)
                .WithForwardStart(new Period(0, TimeUnit.Days)).ToCapFloor(Context)),
        ["MakeSwaption"] = (
            () => new MakeSwaption(new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve), new Period(1, TimeUnit.Years), 0.03).ToSwaption(Context),
            () => new MakeSwaption(new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve), new Period(1, TimeUnit.Years), 0.03)
                .WithSettlementType(SettlementType.Physical).WithSettlementMethod(SettlementMethod.PhysicalOTC)
                .WithOptionConvention(BusinessDayConvention.ModifiedFollowing).ToSwaption(Context)),
        ["MakeCms"] = (
            () => new MakeCms(new Period(5, TimeUnit.Years), new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve)).ToSwap(Context),
            () => new MakeCms(new Period(5, TimeUnit.Years), new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), Curve))
                .WithCmsLegTenor(new Period(3, TimeUnit.Months)).WithCmsLegConvention(BusinessDayConvention.ModifiedFollowing)
                .WithCmsLegTerminationDateConvention(BusinessDayConvention.ModifiedFollowing).WithCmsLegRule(DateGenerationRule.Backward)
                .WithCmsLegEndOfMonth(false).WithCmsLegDayCount(new Actual360()).WithFloatingLegRule(DateGenerationRule.Backward)
                .WithFloatingLegEndOfMonth(false).ToSwap(Context)),
        ["MakeMultipleResetsSwap"] = (
            () => new MakeMultipleResetsSwap(new Period(5, TimeUnit.Years), new Euribor3M(Curve), 2).WithFixedRate(0.03).ToSwap(Context),
            () => new MakeMultipleResetsSwap(new Period(5, TimeUnit.Years), new Euribor3M(Curve), 2).WithFixedRate(0.03)
                .WithFixedLegConvention(BusinessDayConvention.ModifiedFollowing).WithAveragingMethod(RateAveragingType.Compound).ToSwap(Context)),
        ["MakeYoYInflationCapFloor"] = (
            () => new MakeYoYInflationCapFloor(YoYInflationCapFloorType.Cap, new YYEUHICP(), 5, new TARGET(), new Period(3, TimeUnit.Months), CPIInterpolationType.Flat)
                .WithStrike(0.02).ToYoYInflationCapFloor(Context),
            () => new MakeYoYInflationCapFloor(YoYInflationCapFloorType.Cap, new YYEUHICP(), 5, new TARGET(), new Period(3, TimeUnit.Months), CPIInterpolationType.Flat)
                .WithStrike(0.02).WithPaymentDayCounter(new Thirty360(Thirty360Convention.BondBasis)).WithPaymentAdjustment(BusinessDayConvention.ModifiedFollowing)
                .WithFixingDays(0).ToYoYInflationCapFloor(Context)),
        ["BTP"] = (
            () => new BTP(new Date(1, Month.March, 2034), 0.0395, Context, new Date(1, Month.September, 2024)).Cashflows,
            () => new FixedRateBond(
                2,
                100.0,
                new Schedule(new Date(1, Month.September, 2024), new Date(1, Month.March, 2034), new Period(6, TimeUnit.Months), new NullCalendar(),
                    BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, true, Context),
                [0.0395],
                new ActualActual(ActualActualConvention.ISMA),
                BusinessDayConvention.ModifiedFollowing,
                100.0,
                null,
                new TARGET()).Cashflows),
    };
}
