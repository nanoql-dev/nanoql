// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The replicating variance-swap engine on structures dated differently. QuantLib's fair variance is per year of the
/// risk-free curve's own time (<c>replicatingvarianceswapengine.hpp:149</c>, <c>:170-171</c>, <c>:211-213</c>), the
/// portfolio's options priced on each structure's own times, so where a risk-free curve settling T+2 sits beside a
/// volatility dated today, QuantLib divides the volatility's variance by the risk-free curve's shorter time: 2.33 times
/// high at a week and 1.1% at a year, the ratio of the two times. nanoql's variance is per year of the swap's time on
/// the risk-free curve's day counter from the earliest reference date, each structure accruing nothing before its own
/// (the user's rulings): QuantLib's on the market restated from today. Where no structure is dated before the risk-free
/// curve, it is QuantLib's.
/// </summary>
public class VarianceSwapMismatchedTimesTests
{
    [Theory]
    [InlineData(7, 0.08429762162350646, 2213.9710430804066)] // QuantLib as given 0.1966944504548484, 7831.503435262107
    [InlineData(365, 0.06176454864494203, 1035.7213171761261)] // 0.0624489203750799, 1068.2888838801214
    public void TheVarianceIsPerYearOfTheSwapsTime(int days, double variance, double npv)
    {
        // the risk-free curve 5% settling T+2, no dividend yield (QuantLib's drift leaves the yield out,
        // ReplicatingVarianceSwapDividendTests), volatility 25% today; strikes 55 to 150 by 5
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        YieldTermStructure settling = FlatForward.Create(2, new TARGET(), 0.05, dc, context);
        Assert.Equal(today + 4, settling.ReferenceDate);
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.0, dc), settling, new BlackConstantVol(today, null, 0.25, dc));
        double[] calls = Enumerable.Range(0, 11).Select(i => 100.0 + (5.0 * i)).ToArray();
        double[] puts = Enumerable.Range(0, 10).Select(i => 100.0 - (5.0 * i)).ToArray();

        VarianceSwapResults results = ReplicatingVarianceSwapEngine.Calculate(
            new VarianceSwap(PositionType.Long, 0.04, 50000.0, today, today + days), context, process, 5.0, calls, puts);
        Assert.Equal(0.0, (results.Variance!.Value / variance) - 1.0, 1e-12);
        Assert.Equal(0.0, (results.NPV!.Value / npv) - 1.0, 1e-12);
    }
}
