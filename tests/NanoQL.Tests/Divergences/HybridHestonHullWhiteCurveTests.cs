// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The hybrid Heston-Hull-White process with its forward Hull-White process on another curve. QuantLib builds the
/// hybrid's Hull-White model on the Heston process's risk-free curve but steps the short rate on the given process's
/// curve (<c>hybridhestonhullwhiteprocess.cpp:37-46</c>, <c>:64</c>, <c>:137</c>, <c>:179</c>), so its numeraire prices
/// a bond on one curve at a rate taken from the other: a year's put at the money, priced 6.23248 with the forward
/// process on the Heston curve, is 6.11498 with it on a flat 3% curve, 3.6 Monte Carlo errors away (QuantLib 1.44).
/// nanoql rebuilds the forward process on the Heston curve with the given reversion, volatility and forward-measure
/// time, so the hybrid is the one on the Heston curve, and the three engines agree on it.
/// </summary>
public class HybridHestonHullWhiteCurveTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.05, A365);
    private static readonly YieldTermStructure Other = new FlatForward(Today, 0.03, A365);

    private static readonly HestonProcess Heston =
        new(RiskFree, new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.4, -0.6);

    private static readonly VanillaOption Put =
        new(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + 365));

    [Fact]
    public void AForwardProcessOnAnotherCurveIsRebuiltOnTheHestonCurve()
    {
        HybridHestonHullWhiteProcess other = Hybrid(Other, -0.4), own = Hybrid(RiskFree, -0.4);
        var x0 = new Vector([100.0, 0.04, 0.0]);
        var dw = new Vector([0.3, -1.2, 0.7]);
        Assert.Equal(own.Evolve(0.0, x0, 1.0, dw).ToArray(), other.Evolve(0.0, x0, 1.0, dw).ToArray());
        Assert.Equal(own.Numeraire(1.0, x0), other.Numeraire(1.0, x0));

        OneAssetOptionResults onOther = Mc(other), onOwn = Mc(own);
        Assert.Equal(onOwn.NPV, onOther.NPV);

        // QuantLib on the Heston curve 6.2324765519539804, error 0.0331; on the 3% curve 6.1149785281579199
        Assert.True(Math.Abs(onOther.NPV!.Value - 6.2324765519539804) <= 1e-10 * 6.2324765519539804, $"{onOther.NPV}");
    }

    [Fact]
    public void TheThreeEnginesAgreeOnAForwardProcessOnAnotherCurve()
    {
        // at no equity-rate correlation, where the analytic engine's model holds, the Monte Carlo engine on weekly steps
        OneAssetOptionResults mc = new MakeMCHestonHullWhiteEngine<PseudoRandom, GeneralStatistics>(Hybrid(Other, 0.0), 42)
            .WithStepsPerYear(52)
            .WithAntitheticVariate()
            .WithSamples(20000)
            .ToEngine()(Put, Context);
        double analytic = AnalyticHestonHullWhiteEngine.Calculate(Put, Context, new HestonModel(Heston), new HullWhite(RiskFree, 0.1, 0.01), 144).NPV!.Value;
        double fd = FdHestonHullWhiteVanillaEngine.Calculate(Put, Context, new HestonModel(Heston), new HullWhiteProcess(Other, 0.1, 0.01), 0.0).NPV!.Value;
        double error = mc.ErrorEstimate!.Value;
        Assert.True(Math.Abs(mc.NPV!.Value - analytic) <= 3 * error, $"MC {mc.NPV} ± {error}, analytic {analytic}");
        Assert.True(Math.Abs(fd - analytic) <= 3 * error, $"FD {fd}, analytic {analytic}");
    }

    private static HybridHestonHullWhiteProcess Hybrid(YieldTermStructure hullWhiteCurve, double correlation) =>
        new(Heston, new HullWhiteForwardProcess(hullWhiteCurve, 0.1, 0.01).WithForwardMeasureTime(2.0), correlation);

    private static OneAssetOptionResults Mc(HybridHestonHullWhiteProcess process) =>
        new MakeMCHestonHullWhiteEngine<PseudoRandom, GeneralStatistics>(process, 42)
            .WithSteps(1)
            .WithAntitheticVariate()
            .WithSamples(20000)
            .ToEngine()(Put, Context);
}
