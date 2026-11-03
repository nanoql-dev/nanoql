// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Monte Carlo variance-swap engine on a risk-free curve settling later (the playbook's row for structures on
/// another clock). QuantLib's grid runs on the risk-free curve's time (<c>mcvarianceswapengine.hpp:191-201</c>), so
/// with that curve settling two days on the paths run two days short and the volatility, dated today, is read at the
/// shorter times. nanoql runs the grid on the process's clock, from the earliest structure's date, each structure read
/// there accruing nothing before its own date, as the vanilla Monte Carlo engines do. The check is nanoql on the
/// risk-free curve restated from the evaluation date (a discount of 1 to its date and its rate beyond): the same paths
/// and value.
/// </summary>
public class McVarianceSwapMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void ARiskFreeCurveSettlingLaterRunsTheGridFromTheEvaluationDate()
    {
        var settling = new FlatForward(Today + 2, 0.05, A365);
        var restated = new DiscountCurve([Today, Today + 2, Today + 2 + 3650], [1.0, 1.0, Math.Exp(-0.05 * 10.0)], A365);
        var swap = new VarianceSwap(PositionType.Long, 0.04, 50000.0, Today, Today + 365);
        VarianceSwapResults expected = Price(swap, restated), actual = Price(swap, settling);
        Assert.Equal(expected.Variance!.Value, actual.Variance!.Value, 1e-12 * expected.Variance!.Value);
        Assert.Equal(expected.NPV!.Value, actual.NPV!.Value, 1e-9);
    }

    private static VarianceSwapResults Price(VarianceSwap swap, YieldTermStructure riskFree)
    {
        var vol = new BlackVarianceCurve(Today, [Today + 91, Today + 365], [0.15, 0.25], A365);
        var process = new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.01, A365), riskFree, vol);
        return new MakeMCVarianceSwapEngine<PseudoRandom, GeneralStatistics>(process, 42)
            .WithStepsPerYear(52)
            .WithSamples(1023)
            .ToEngine()(swap, Context);
    }
}
