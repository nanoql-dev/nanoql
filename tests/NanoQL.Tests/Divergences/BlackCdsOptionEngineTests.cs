// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The CDS option's time before the discount curve's date (the S-xcr plan's XR8 (xv)): floored at 0, the intrinsic
/// value, where QuantLib's is negative and its standard deviation NaN. On 31 August 2006, a knock-out payer option
/// exercising the next day on a five-year buyer swap from a month out (spread 0.0005, notional 10⁶, quarterly, TARGET,
/// Actual/360), the hazard rate 0.001 flat from today and the discount curve 5% flat from two days out, QuantLib throws
/// ("stdDev (-nan(ind)) must be non-negative"); the Black formula at a standard deviation of 0 on its swap's fair spread
/// 0.00060383738773871025 and coupon-leg NPV −2212.1143600025548 reads 459.4003530439083 (the S-xcr run's
/// <c>c4_facts.cpp</c>). The same option not knocking out adds its front-end protection, read as XR8 (x) reads a curve
/// before its date: a discount of 1 and a default probability of 0 (the S-xcr code review's C4 O1-3), where QuantLib
/// refuses at the time first.
/// </summary>
public class BlackCdsOptionEngineTests
{
    [Fact]
    public void AnExerciseBeforeTheCurvesDateIsTheIntrinsicValue()
    {
        (CdsOptionResults results, _, _) = Price(discountFromT2: true, knocksOut: true);
        Assert.Equal(459.4003530439083, results.NPV!.Value, 1e-9 * 459.4003530439083);
        Assert.Equal(2212.1143600025548 / 0.0005, results.RiskyAnnuity!.Value, 1e-9 * 2212.1143600025548 / 0.0005);
    }

    [Fact]
    public void AKnockInsFrontEndProtectionReadsTheCurvesBeforeTheirDates()
    {
        // the discount curve from T+2: the protection discounted at 1
        (CdsOptionResults knockIn, FlatHazardRate hazard, Date exercise) = Price(discountFromT2: true, knocksOut: false);
        (CdsOptionResults knockOut, _, _) = Price(discountFromT2: true, knocksOut: true);
        Assert.Equal(knockOut.NPV!.Value + (1e6 * 0.6 * hazard.DefaultProbability(exercise)), knockIn.NPV!.Value, 1e-9);

        // the hazard curve from T+2: no default before it, so no protection
        (CdsOptionResults knockIn2, _, _) = Price(discountFromT2: false, knocksOut: false);
        (CdsOptionResults knockOut2, _, _) = Price(discountFromT2: false, knocksOut: true);
        Assert.Equal(knockOut2.NPV, knockIn2.NPV);
    }

    // The fact's option on 31 August 2006: its discount curve from T+2 and its hazard curve from today, or the reverse.
    private static (CdsOptionResults Results, FlatHazardRate Hazard, Date Exercise) Price(bool discountFromT2, bool knocksOut)
    {
        var today = new Date(31, August, 2006);
        var context = new EvaluationContext { EvaluationDate = today };
        Calendar calendar = new TARGET();
        Date start = calendar.Advance(today, 1, TimeUnit.Months);
        var schedule = new Schedule(
            start, calendar.Advance(start, 5, TimeUnit.Years), new Period(Frequency.Quarterly), calendar,
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward,
            false, context);
        var hazard = new FlatHazardRate(discountFromT2 ? today : today + 2, 0.001, new Actual360());
        var discount = new FlatForward(discountFromT2 ? today + 2 : today, 0.05, new Actual360());
        var swap = new CreditDefaultSwap(ProtectionSide.Buyer, 1e6, 0.0005, schedule, BusinessDayConvention.ModifiedFollowing, new Actual360());
        var option = new CdsOption(swap, new EuropeanExercise(today + 1), knocksOut);

        CreditDefaultSwapResults SwapEngine(CreditDefaultSwap cds, EvaluationContext ctx) =>
            MidPointCdsEngine.Calculate(cds, ctx, hazard, 0.4, discount);

        return (BlackCdsOptionEngine.Calculate(option, context, hazard, 0.4, discount, 0.2, SwapEngine), hazard, today + 1);
    }
}
