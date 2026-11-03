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
/// The soft barrier with coincident or nearly coincident barriers (fixed). Where U − L &lt; 1e-4 QuantLib prices its
/// analytic barrier engine on flat Actual/360 curves rebuilt from TARGET's adjusted evaluation date
/// (<c>analyticsoftbarrierengine.cpp:250-282</c>), so a process on Actual/365 Fixed prices 10.187731 where the
/// analytic engine on it gives 10.159379; and the switch is absolute in price units, so the same contract changes
/// value with the underlying's scale: a down-in call struck at the spot on the band [0.990, 0.995] × spot prices
/// 0.1027057 × spot at spots 100 and 1 and 0.1063173 × spot at 0.02 and 0.01, a 0.5% band taken as a hard barrier.
/// Haug's own difference quotient cancels as the band narrows, beyond 1e-12 below a relative width of about 1e-3.
/// nanoql prices the single barrier at U = L only, through the analytic barrier engine on the process itself, Haug's
/// form where (U − L)/U ≥ 3e-3, and the band average below it. Checks: QuantLib's analytic barrier engine on the same
/// process at U = L, and homogeneity, the contract scaled to spot 100.
/// </summary>
public class SoftBarrierCoincidentBarriersTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void CoincidentBarriersAreTheSingleBarrierOnTheProcessItself()
    {
        // r 6%, q 2%, σ 25% on Actual/365 Fixed, two years: QuantLib 10.187731 on its rebuilt curves
        GeneralizedBlackScholesProcess process = Process(100.0, 0.06, 0.02, 0.25);
        var exercise = new EuropeanExercise(Today + 730);
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        double soft = AnalyticSoftBarrierEngine.Calculate(
            new SoftBarrierOption(BarrierType.DownOut, 90.0, 90.0, payoff, exercise), Context, process).NPV!.Value;
        double hard = AnalyticBarrierEngine.Calculate(
            new BarrierOption(BarrierType.DownOut, 90.0, 0.0, payoff, exercise), Context, process).NPV!.Value;
        Assert.Equal(hard, soft);
    }

    [Fact]
    public void ABandNarrowerThan1e4IsTheBandAverage()
    {
        // [90, 90.00001]: QuantLib's rebuilt curves; the average is the single barrier's limit
        GeneralizedBlackScholesProcess process = Process(100.0, 0.06, 0.02, 0.25);
        var exercise = new EuropeanExercise(Today + 730);
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        double soft = AnalyticSoftBarrierEngine.Calculate(
            new SoftBarrierOption(BarrierType.DownOut, 90.0, 90.00001, payoff, exercise), Context, process).NPV!.Value;
        double hard = AnalyticBarrierEngine.Calculate(
            new BarrierOption(BarrierType.DownOut, 90.000005, 0.0, payoff, exercise), Context, process).NPV!.Value;
        Assert.Equal(hard, soft, 1e-9);
    }

    [Fact]
    public void ANarrowBandAtASmallSpotIsTheContractScaled()
    {
        // a down-in call struck at the spot on [0.990, 0.995] × spot, r 5%, q 2%, σ 25%, a year: 0.1027057377 × spot
        // at every spot, where QuantLib's absolute switch gives 0.1063172662 × spot at 0.01
        foreach (double spot in new[] { 100.0, 1.0, 0.01 })
        {
            Assert.Equal(0.1027057377, ScaledKnockIn(spot, 0.990, 0.995), 1e-10);
        }
    }

    [Fact]
    public void ABandBelowTheRelativeCutAtASmallSpotIsTheContractScaled()
    {
        // [0.999, 0.9995] × spot, a relative width of 5e-4, below Haug's 3e-3: the band average at every spot
        double atHundred = ScaledKnockIn(100.0, 0.999, 0.9995);
        Assert.Equal(atHundred, ScaledKnockIn(1.0, 0.999, 0.9995), 1e-11);
        Assert.Equal(atHundred, ScaledKnockIn(0.01, 0.999, 0.9995), 1e-11);
    }

    // The down-in call struck at the spot on the band [lo, hi] × spot, over the spot
    private static double ScaledKnockIn(double spot, double lo, double hi)
    {
        GeneralizedBlackScholesProcess process = Process(spot, 0.05, 0.02, 0.25);
        var option = new SoftBarrierOption(
            BarrierType.DownIn, lo * spot, hi * spot, new PlainVanillaPayoff(OptionType.Call, spot),
            new EuropeanExercise(Today + 365));
        return AnalyticSoftBarrierEngine.Calculate(option, Context, process).NPV!.Value / spot;
    }

    private static GeneralizedBlackScholesProcess Process(double spot, double r, double q, double sigma) =>
        new BlackScholesMertonProcess(
            spot,
            new FlatForward(Today, q, A365),
            new FlatForward(Today, r, A365),
            new BlackConstantVol(Today, new NullCalendar(), sigma, A365));
}
