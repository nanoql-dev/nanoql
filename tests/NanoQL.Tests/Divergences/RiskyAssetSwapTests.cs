// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The risky asset swap's annuities, recovery integral and reads before the discount curve's date, on 31 August 2006
/// with the discount curve 5% flat (Actual/360) and the hazard rate 0.02 flat (Actual/365 Fixed), nominal 100, spread
/// 0.01, an annual fixed and a quarterly float schedule on TARGET to 31 August 2011, both legs Actual/360:
/// <list type="bullet">
/// <item>QuantLib's fixed annuity runs over the float schedule: at recovery 0.4 its fair spread reads
/// 0.011808418663949119 and the package at it 99.57571577267737, not par. Where the schedules coincide, at recovery 0
/// (whose integral reads nothing), its fair spread is 0.019050924808858876 and its NPV 95.97072563457229, the receiver's
/// at a 4% coupon −96.194787454490481.</item>
/// <item>The recovery value's Euler sum stops a day short of each period's end: on five annual periods of a 5%
/// continuous discount curve and a 0.02 hazard rate (Actual/365 Fixed, recovery 0.4) QuantLib's reads
/// 0.033672884840343142, the closed form R·h/(r + h)·(1 − e^(−(r + h)T)) 0.033765376341026734.</item>
/// <item>On a discount curve from two TARGET days out QuantLib throws ("negative time (-0.0111111) given"); on that
/// curve restated from today, discounting 1 to its date, it reads NPV 95.971197157591376 and fair spread
/// 0.019044839323900529 where the schedules coincide at recovery 0.</item>
/// <item>A seasoned swap's past periods are QuantLib's reads, which refuse a date before the curve's: on 15 January
/// 2026, the curves from today, a swap from 15 March 2024 to 15 March 2030 (annual fixed, quarterly float, no calendar)
/// refuses in QuantLib ("negative time (-1.60833) given"), where reading 1 there would price every past period at
/// par.</item>
/// </list>
/// </summary>
public class RiskyAssetSwapTests
{
    private static readonly Date Today = new(31, August, 2006);

    [Fact]
    public void WhereTheSchedulesCoincideTheSwapIsQuantLibs()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        Schedule fl = Float();
        var swap = new RiskyAssetSwap(true, 100.0, fl, fl, new Actual360(), new Actual360(), 0.01, 0.0, Discount(), Hazard());
        RiskyAssetSwapResults results = swap.Calculate(context);
        Assert.Equal(0.019050924808858876, results.FairSpread!.Value, 1e-15);
        Assert.Equal(95.97072563457229, results.NPV!.Value, 1e-12);

        var receiver = new RiskyAssetSwap(false, 100.0, fl, fl, new Actual360(), new Actual360(), 0.01, 0.0, Discount(), Hazard(), 0.04);
        Assert.Equal(-96.194787454490481, receiver.Calculate(context).NPV!.Value, 1e-12);
    }

    [Theory]
    [InlineData(null, 95.97072563457229, 0.019315520986759694)]
    [InlineData(0.04, 96.20668598227455, 0.018778123653103697)]
    public void OnEqualSchedulesWithOtherDayCountersTheFairSpreadIsOverTheFloatAnnuity(double? coupon, double npv, double quantLibs)
    {
        // the fixed leg Actual/365 Fixed, the float Actual/360, recovery 0: QuantLib's NPV, and its fair spread over
        // its fixed annuity here over the float annuity
        var context = new EvaluationContext { EvaluationDate = Today };
        Schedule fl = Float();
        var swap = new RiskyAssetSwap(true, 100.0, fl, fl, new Actual365Fixed(), new Actual360(), 0.01, 0.0, Discount(), Hazard(), coupon);
        RiskyAssetSwapResults results = swap.Calculate(context);
        Assert.Equal(npv, results.NPV!.Value, 1e-12);

        FlatForward discount = Discount();
        double fixedAnnuity = 0.0;
        for (int i = 1; i < fl.Dates.Length; i++)
        {
            fixedAnnuity += new Actual365Fixed().YearFraction(fl.Dates[i - 1], fl.Dates[i]) * discount.Discount(fl.Dates[i]);
        }

        Assert.Equal(quantLibs * fixedAnnuity / swap.FloatAnnuity(context), results.FairSpread!.Value, 1e-15);
    }

    [Fact]
    public void ThePackageIsAtParAtTheFairSpread()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        var swap = new RiskyAssetSwap(true, 100.0, Fixed(), Float(), new Actual360(), new Actual360(), 0.01, 0.4, Discount(), Hazard());
        double fairSpread = swap.Calculate(context).FairSpread!.Value;
        var atFair = new RiskyAssetSwap(true, 100.0, Fixed(), Float(), new Actual360(), new Actual360(), fairSpread, 0.4, Discount(), Hazard());

        // QuantLib: 99.57571577267737 at its fair spread, 0.011808418663949119
        Assert.Equal(100.0, atFair.Calculate(context).NPV!.Value, 1e-11);
        Assert.NotEqual(0.011808418663949119, fairSpread, 1e-5);
    }

    [Fact]
    public void TheRecoveryValueRunsToEachPeriodsEnd()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        double r = 0.05, h = 0.02, recovery = 0.4;
        var yieldCurve = new FlatForward(Today, r, new Actual365Fixed(), Compounding.Continuous);
        var hazard = new FlatHazardRate(Today, h, new Actual365Fixed());
        Date end = Today + new Period(5, TimeUnit.Years);
        var schedule = new Schedule(
            Today, end, new Period(1, TimeUnit.Years), new NullCalendar(), BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, DateGenerationRule.Forward, false, context);

        // at a coupon and spread of 0 the NPV is the bond's redemption and recovery plus the swapped discounts
        var swap = new RiskyAssetSwap(true, 1.0, schedule, schedule, new Actual365Fixed(), new Actual365Fixed(), 0.0, recovery, yieldCurve, hazard, 0.0);
        double recoveryValue = swap.Calculate(context).NPV!.Value
            - (yieldCurve.Discount(end) * hazard.SurvivalProbability(end)) - yieldCurve.Discount(Today) + yieldCurve.Discount(end);
        double t = new Actual365Fixed().YearFraction(Today, end);
        double exact = recovery * h / (r + h) * (1 - Math.Exp(-(r + h) * t));

        // the daily right-hand sum's own error, about (r + h)/730 relative; QuantLib's −2.739e-3
        Assert.Equal(exact, recoveryValue, 1.5e-4 * exact);
        Assert.NotEqual(0.033672884840343142, recoveryValue, 1e-3 * exact);
    }

    [Fact]
    public void AT2DiscountCurveReadsAsItsRestatement()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        Schedule fl = Float();
        var t2 = new FlatForward(new TARGET().Advance(Today, 2, TimeUnit.Days), 0.05, new Actual360());
        RiskyAssetSwapResults results = new RiskyAssetSwap(true, 100.0, fl, fl, new Actual360(), new Actual360(), 0.01, 0.0, t2, Hazard()).Calculate(context);
        Assert.Equal(95.971197157591376, results.NPV!.Value, 1e-12);
        Assert.Equal(0.019044839323900529, results.FairSpread!.Value, 1e-15);
    }

    [Fact]
    public void ASeasonedSwapsPastPeriodsAreRefused()
    {
        var today = new Date(15, January, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        var end = new Date(15, March, 2030);
        Schedule Of(int months) => new Schedule(
            new Date(15, March, 2024), end, new Period(months, TimeUnit.Months), new NullCalendar(),
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Forward, false, context);
        var swap = new RiskyAssetSwap(
            true, 100.0, Of(12), Of(3), new Actual360(), new Actual360(), 0.01, 0.4, new FlatForward(today, 0.05, new Actual360()),
            new FlatHazardRate(today, 0.02, new Actual365Fixed()));
        Assert.StartsWith("negative time (-1.6083", Assert.Throws<NanoQLException>(() => swap.Calculate(context)).Message);
        Assert.Throws<NanoQLException>(() => new RiskyAssetSwapOption(swap, today + new Period(1, TimeUnit.Years), 0.012, 0.3).Calculate(context));
    }

    [Fact]
    public void AnExpiredSwapHasNoFairSpread()
    {
        // QuantLib's fair spread reads members its expired calculation leaves unset
        var context = new EvaluationContext { EvaluationDate = new Date(1, September, 2011) };
        RiskyAssetSwapResults results = new RiskyAssetSwap(
            true, 100.0, Fixed(), Float(), new Actual360(), new Actual360(), 0.01, 0.4, new FlatForward(context.EvaluationDate, 0.05, new Actual360()), Hazard())
            .Calculate(context);
        Assert.Equal(0.0, results.NPV);
        Assert.Null(results.FairSpread);
    }

    private static FlatForward Discount() => new(Today, 0.05, new Actual360());

    private static FlatHazardRate Hazard() => new(Today, 0.02, new Actual365Fixed());

    private static Schedule Fixed() =>
        new MakeSchedule().From(Today).To(new Date(31, August, 2011)).WithTenor(new Period(1, TimeUnit.Years)).WithCalendar(new TARGET()).ToSchedule();

    private static Schedule Float() =>
        new MakeSchedule().From(Today).To(new Date(31, August, 2011)).WithTenor(new Period(3, TimeUnit.Months)).WithCalendar(new TARGET()).ToSchedule();
}
