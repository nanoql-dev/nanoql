// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The binary barrier engine below machine-epsilon variance (the S-bar plan's BR5 (xv), fixed under R8). QuantLib's
/// indicators read the spot and its power of the barrier overflows to 0, so a down-and-out cash-or-nothing call whose
/// forward stays above the barrier prices 0 at σ of 0 and 1e-8, where its limit is cash × D = 9.512294 and σ 1e-4 gives
/// 9.51229425 (the planner's P8). nanoql follows the forward, which hits the barrier, if at all, when it reaches it, as
/// QuantLib's master prices the analytic barrier engine (#2853). Checks: the engine at σ 1e-4, and #2853's rule on a
/// forward that falls through the barrier.
/// </summary>
public class BinaryBarrierZeroVolatilityTests
{
    [Fact]
    public void TheUnderlyingFollowsItsForward()
    {
        var today = new Date(16, September, 2015);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var exercise = new AmericanExercise(today, today + 365, true);
        double Price(BarrierType type, double barrier, double strike, double r, double q, double sigma) =>
            AnalyticBinaryBarrierEngine.Calculate(
                new BarrierOption(type, barrier, 0.0, new CashOrNothingPayoff(OptionType.Call, strike, 10.0), exercise),
                context,
                new BlackScholesMertonProcess(
                    100.0, new FlatForward(today, q, dc), new FlatForward(today, r, dc),
                    new BlackConstantVol(today, new NullCalendar(), sigma, dc))).NPV!.Value;

        // the forward stays above the barrier: the cash, discounted; QuantLib: 0 at σ 0 and 1e-8
        double atSmallVolatility = Price(BarrierType.DownOut, 90.0, 100.0, 0.05, 0.01, 1e-4);
        Assert.Equal(9.512294245007139, atSmallVolatility, 1e-12);
        Assert.Equal(10.0 * Math.Exp(-0.05), Price(BarrierType.DownOut, 90.0, 100.0, 0.05, 0.01, 0.0), 1e-12);
        Assert.Equal(atSmallVolatility, Price(BarrierType.DownOut, 90.0, 100.0, 0.05, 0.01, 1e-8), 1e-12);

        // a forward falling through 97 knocks out, and knocks in the European cash-or-nothing
        Assert.Equal(0.0, Price(BarrierType.DownOut, 97.0, 90.0, 0.01, 0.05, 0.0));
        Assert.Equal(10.0 * Math.Exp(-0.01), Price(BarrierType.DownIn, 97.0, 90.0, 0.01, 0.05, 0.0), 1e-12);
        Assert.Equal(Price(BarrierType.DownIn, 97.0, 90.0, 0.01, 0.05, 1e-4), Price(BarrierType.DownIn, 97.0, 90.0, 0.01, 0.05, 0.0), 1e-12);
    }
}
