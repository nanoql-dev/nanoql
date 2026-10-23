// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The binomial convertible engine's clock (the S-sr plan's SSR8, ruled by the user on 2026-10-22, (a)). QuantLib's
/// engine flattens the volatility at the last exercise date on its own day counter, σ = blackVol(T), and its tree reads it
/// over the risk-free curve's year fraction, so a volatility on another day counter integrates the wrong variance
/// (<c>binomialconvertibleengine.hpp:80-125</c>): on the wheel, a 5-year zero-coupon convertible (conversion ratio 2,
/// redemption 100, three settlement days) on a spot of 50, flat 5% and 2% curves on Actual/360 and a 15% volatility on
/// Actual/365 (Fixed), from 15 January 2025, a credit spread of 0.5%, Cox-Ross-Rubinstein at 101 steps, prices
/// 95.56792008293525, its price with the volatility read on Actual/360; on the market restated on one clock, the
/// volatility 0.15·√(τ365/τ360) = 0.14896905994842624 on Actual/360, it prices 95.50250080004932. nanoql flattens a
/// structure on another day counter or reference date from its variance or discount between the risk-free curve's
/// reference date and the last exercise date, over that interval on the risk-free day counter. QuantLib's check: its own
/// engine on the restated market. The engine's NPV stays QuantLib's, the bond's value at its settlement date, which its
/// settlement value equals: a named departure from the playbook's lattice clock, which QuantLib's convertible cases
/// assert (<c>convertiblebonds.cpp:170</c>, <c>:178</c>, <c>:218</c>, against the plain bond's settlement value); on the
/// wheel, the deep out-of-the-money convertible (a conversion ratio of 1e-16, 1,001 steps) prices 75.69370775331546,
/// within 3e-3 of the plain zero-coupon bond's settlement value on the spreaded curve, 75.69077550691921, and 0.06 above
/// its NPV at the curve's date, 75.63297824267259.
/// </summary>
public class ConvertibleClockTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly TARGET Calendar = new();
    private static readonly Date Issue = Calendar.Advance(Today, 2, TimeUnit.Days);
    private static readonly Date Maturity = Calendar.Advance(Issue, 5, TimeUnit.Years);

    [Fact]
    public void AVolatilityOnAnotherDayCounterIsFlattenedOnTheRiskFreeClock()
    {
        GeneralizedBlackScholesProcess process = Process(new BlackConstantVol(Today, new NullCalendar(), 0.15, new Actual365Fixed()));
        BondResults results = BinomialConvertibleEngine.Calculate<CoxRossRubinstein>(Bond(2.0), Context, process, 101, 0.005);
        Assert.True(Math.Abs((results.NPV!.Value / 95.50250080004932) - 1.0) <= 1e-12, $"{results.NPV}");
        Assert.True(Math.Abs(results.NPV!.Value - 95.56792008293525) > 0.05, $"{results.NPV}");
    }

    [Fact]
    public void TheNPVIsTheValueAtSettlement()
    {
        GeneralizedBlackScholesProcess process = Process(new BlackConstantVol(Today, new NullCalendar(), 0.15, A360));
        BondResults convertible = BinomialConvertibleEngine.Calculate<CoxRossRubinstein>(Bond(1e-16), Context, process, 1001, 0.005);
        Assert.Equal(convertible.NPV, convertible.SettlementValue);

        var zero = new ZeroCouponBond(3, Calendar, 100.0, Maturity, BusinessDayConvention.Following, 100.0, Issue);
        var spreaded = new ZeroSpreadedTermStructure(
            new FlatForward(Today, 0.05, A360), 0.005, Compounding.Continuous, Frequency.NoFrequency);
        BondResults plain = DiscountingBondEngine.Calculate(zero, Context, spreaded);
        Assert.True(Math.Abs(convertible.NPV!.Value - plain.SettlementValue!.Value) <= 1.0e-2, $"{convertible.NPV} against {plain.SettlementValue}");
        Assert.True(Math.Abs(convertible.NPV!.Value - plain.NPV!.Value) > 0.05, $"{convertible.NPV} against {plain.NPV}");
    }

    private static GeneralizedBlackScholesProcess Process(BlackVolTermStructure volatility) =>
        new BlackScholesMertonProcess(50.0, new FlatForward(Today, 0.02, A360), new FlatForward(Today, 0.05, A360), volatility);

    private static ConvertibleZeroCouponBond Bond(double conversionRatio)
    {
        Schedule schedule = new MakeSchedule().From(Issue).To(Maturity).WithFrequency(Frequency.Once).WithCalendar(Calendar)
                                              .Backwards().ToSchedule();
        return new ConvertibleZeroCouponBond(
            new EuropeanExercise(Maturity), conversionRatio, [], Issue, 3, A360, schedule, 100.0);
    }
}
