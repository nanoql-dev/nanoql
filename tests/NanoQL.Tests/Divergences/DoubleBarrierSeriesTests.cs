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
/// The analytic double-barrier engine's series length (fixed). QuantLib sums n from −<c>series</c> to <c>series</c>,
/// five by default (<c>analyticdoublebarrierengine.cpp:176</c>), unconverged on narrow corridors and long terms: [95,
/// 105] at a volatility of 50% over two years prices a knock-out 3.3e-3 where 50 terms and QuantLib's binomial
/// double-barrier engine give about 0. nanoql takes <c>series</c> as the minimum and adds terms while the next pair
/// exceeds machine epsilon of the sum, QuantLib's value wherever five have converged. Checks: 50 terms; QuantLib's
/// binomial engine, 5.5e-88 (QuantLib-Python 1.44's).
/// </summary>
public class DoubleBarrierSeriesTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void ANarrowCorridorOverTwoYearsIsWorthItsConvergedSeries()
    {
        var process = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, 0.02, A365),
            new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.50, A365));
        var option = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 95.0, 105.0, 0.0, new PlainVanillaPayoff(OptionType.Call, 100.0),
            new EuropeanExercise(Today + 730));

        // QuantLib's five terms: 0.00331311663; fifty: 0
        double five = AnalyticDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;
        double fifty = AnalyticDoubleBarrierEngine.Calculate(option, Context, process, 50).NPV!.Value;
        Assert.Equal(fifty, five, 1e-15);
        Assert.True(five < 1e-12, $"{five}");
    }
}
