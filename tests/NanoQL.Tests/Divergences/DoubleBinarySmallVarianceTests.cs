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
/// The double-barrier binary knock-out at small variance (the S-bar plan's BR8 (c), a refusal lifted under the user's
/// rule of 2026-10-20). QuantLib refuses "serie did not converge sufficiently fast" where the corridor is wide against
/// σ√T, where Hui's eigenfunction series converges slowest (<c>analyticdoublebarrierbinaryengine.cpp:110</c>). nanoql
/// takes the cash-or-nothing part of QuantLib's own double-barrier image series there (its
/// <c>AnalyticDoubleBarrierEngine::callKO</c>), which converges fastest exactly there, its terms in logs and its
/// normal differences from the small tail. Checks: Hui's value where it converges, and the image series in 50 digits
/// where it doesn't (the reviewer's r2c2), which QuantLib's finite-difference Heston double-barrier engine gives to ten
/// digits on [95, 105] at 2% over a day.
/// </summary>
public class DoubleBinarySmallVarianceTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void WhereHuisSeriesConvergesItsValueStands()
    {
        // QuantLib's [80, 120] at 20% over 182 days and the reviewer's [99, 101] at 5% over a day: Hui's values, which
        // equal the image series in 50 digits
        Assert.Equal(6.694672821096, KnockOut(100.0, 80.0, 120.0, 0.20, 182), 1e-11);
        Assert.Equal(9.9959274311, KnockOut(100.0, 99.0, 101.0, 0.05, 1), 1e-10);
        Assert.Equal(4.5113551781, KnockOut(100.5, 99.0, 101.0, 0.10, 2), 1e-10);
    }

    [Fact]
    public void WhereQuantLibRefusesTheImageSeriesPrices()
    {
        // [95, 105] at 2% over a day: QuantLib refuses; the image series in 50 digits, the FD Heston engine 9.9986302308
        Assert.Equal(9.9986302308, KnockOut(100.0, 95.0, 105.0, 0.02, 1), 1e-10);
        Assert.Equal(9.9904155549, KnockOut(100.0, 98.0, 102.0, 0.01, 7), 1e-10);
    }

    private static double KnockOut(double spot, double lo, double hi, double sigma, int days)
    {
        var process = new BlackScholesMertonProcess(
            spot,
            new FlatForward(Today, 0.02, A365),
            new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), sigma, A365));
        var option = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, lo, hi, 0.0, new CashOrNothingPayoff(OptionType.Call, 0.0, 10.0),
            new EuropeanExercise(Today + days));
        return AnalyticDoubleBarrierBinaryEngine.Calculate(option, Context, process).NPV!.Value;
    }
}
