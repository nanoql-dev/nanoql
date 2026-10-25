// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Heston extensions' analytic engines, surface and density on curves dated or counted differently (the S-hes
/// plan's HR7 (xi), the mismatched-dates class of master Revision 58). QuantLib's engines run the model's time from the
/// risk-free curve's reference date (<c>process-&gt;time</c>: <c>coshestonengine.cpp:66</c>,
/// <c>exponentialfittinghestonengine.cpp:221</c>, <c>hestonexpansionengine.cpp:70</c>,
/// <c>analyticpdfhestonengine.cpp:44</c>, <c>analyticptdhestonengine.cpp:268-270</c>), and the PDF engine, the surface
/// and the density calculator read the dividend curve at the risk-free curve's times
/// (<c>analyticpdfhestonengine.cpp:51-52</c>, <c>hestonblackvolsurface.cpp:75-92</c>,
/// <c>hestonrndcalculator.cpp:117-118</c>). nanoql's run on the model's clock, from the earliest reference date of the
/// curves on the risk-free curve's day counter, each curve read at the instant a time denotes, nothing accruing before
/// its own date; the density calculator's times are the risk-free curve's, as <see cref="BSMRNDCalculator"/>'s. The
/// check: QuantLib on the market restated on one clock, which nanoql on the original market reproduces to rounding
/// [executed on the wheel, and through the harness for the expansions and the det-jump Bates engines].
/// </summary>
public class HestonAnalyticMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly DayCounter A360 = new Actual360();

    private static readonly VanillaOption Put =
        new(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + 365));

    [Fact]
    public void CosExponentialFittingAndTheExpansionsRunFromTheEarliestDate()
    {
        // r 5% settling T+2, q 2% today; QuantLib's restated market: every curve today, the risk-free discount 1 to T+2
        // and 5% after (a log-linear discount curve); for the expansions, which read the curves only at the expiry, the
        // risk-free rate 5%·363/365 from today
        HestonModel model = Model(SettlingLater(), new FlatForward(Today, 0.02, A365));
        AssertClose(5.955214446010749, COSHestonEngine.Calculate(Put, Context, model).NPV!.Value); // as given 5.936838388506515
        AssertClose(5.955214447341254, ExponentialFittingHestonEngine.Calculate(Put, Context, model).NPV!.Value); // as given 5.9368383898238495
        foreach ((HestonExpansionEngineHestonExpansionFormula formula, double quantLib) in new[]
                 {
                     (HestonExpansionEngineHestonExpansionFormula.LPP2, 5.909666291125407),
                     (HestonExpansionEngineHestonExpansionFormula.LPP3, 5.929047049877357),
                     (HestonExpansionEngineHestonExpansionFormula.Forde, 5.098078976337059),
                 })
        {
            AssertClose(quantLib, HestonExpansionEngine.Calculate(Put, Context, model, formula).NPV!.Value);
        }
    }

    [Fact]
    public void ThePdfEngineReadsEachCurveAtItsInstant()
    {
        // markets whose carry to the expiry is 0, where QuantLib's integration window is nanoql's (HR7 (vii)): r 5%
        // settling T+2 beside q 5%·363/365 today, QuantLib's restated market as above; and r 5% on Act/360 beside
        // q 5%·365/360 on Act/365, QuantLib's restated market q 5% on Act/360; the window's value, the price less the tails
        // nanoql adds beyond it (the code review's C2-B1-2)
        HestonModel settling = Model(SettlingLater(), new FlatForward(Today, 0.05 * 363 / 365, A365));
        AssertClose(6.97794400855235, Window(AnalyticPDFHestonEngine.Calculate(Put, Context, settling))); // as given 6.949557770801946
        HestonModel counted = Model(new FlatForward(Today, 0.05, A360), new FlatForward(Today, 0.05 * 365 / 360, A365));
        AssertClose(7.017333211748751, Window(AnalyticPDFHestonEngine.Calculate(Put, Context, counted))); // as given 7.043405619957796
    }

    [Fact]
    public void TheTimeDependentModelRunsOnTheModelsClock()
    {
        // theta 0.04, 0.06 and 0.05 and sigma 0.4, 0.3 and 0.5 on (0, 0.25], (0.25, 0.5] and after; r 5% settling T+2
        // beside q 2% today, QuantLib's restated market as above; r 5% on Act/360 beside q 2% on Act/365, QuantLib's
        // restated market q 2%·360/365 on Act/360
        PiecewiseTimeDependentHestonModel settling = TimeDependentModel(SettlingLater(), new FlatForward(Today, 0.02, A365));
        AssertClose(6.393924882350376, AnalyticPTDHestonEngine.Calculate(Put, Context, settling).NPV!.Value); // as given 6.373333602164614
        PiecewiseTimeDependentHestonModel counted = TimeDependentModel(new FlatForward(Today, 0.05, A360), new FlatForward(Today, 0.02, A365));
        AssertClose(6.4080373263704615, AnalyticPTDHestonEngine.Calculate(Put, Context, counted).NPV!.Value); // as given 6.40803038208859
    }

    [Fact]
    public void TheRoughEngineRunsFromTheEarliestDate()
    {
        // H = 0.1; r 5% settling T+2 beside q 2% today, QuantLib's restated market as above
        var engine = new AnalyticRoughHestonEngine(
            new RoughHestonModel(Model(SettlingLater(), new FlatForward(Today, 0.02, A365)).Process, 0.1), 128, 256);
        AssertClose(5.898995737413316, engine.Calculate(Put, Context).NPV!.Value); // as given 5.8795142496245525
    }

    [Fact]
    public void TheBatesEnginesRunFromTheEarliestDate()
    {
        // lambda 1, nu -0.1, delta 0.1, and the det-jump double-exponential model's defaults; r 5% settling T+2 beside
        // q 2% today, QuantLib's restated market as above, for the det-jump engine the risk-free rate 5%·363/365
        YieldTermStructure riskFree = SettlingLater();
        YieldTermStructure dividends = new FlatForward(Today, 0.02, A365);
        var bates = new BatesModel(new BatesProcess(riskFree, dividends, 100.0, 0.04, 1.5, 0.04, 0.4, -0.6, 1.0, -0.1, 0.1));
        AssertClose(7.683450545859166, BatesEngine.Calculate(Put, Context, bates).NPV!.Value); // as given 7.660010467625547
        var detJump = new BatesDoubleExpDetJumpModel(Model(riskFree, dividends).Process);
        AssertClose(6.117917385671632, BatesDoubleExpDetJumpEngine.Calculate(Put, Context, detJump).NPV!.Value);
    }

    [Fact]
    public void TheSurfaceAndTheDensityCalculatorReadEachCurveAtItsInstant()
    {
        // the surface's volatilities at a year and strikes 90, 100 and 110; r 5% settling T+2 beside q 2% today,
        // QuantLib's restated market as above (as given 0.21008581881346028, 0.18983564182269333, 0.17268356712378327)
        var settling = new HestonBlackVolSurface(
            Model(SettlingLater(), new FlatForward(Today, 0.02, A365)), AnalyticHestonEngineComplexLogFormula.AngledContour, FourierIntegration.GaussLaguerre(164));
        AssertVolatilities([0.20997785869951663, 0.1897790257021915, 0.17267468410879397], settling);

        // r 5% on Act/360 beside q 2% on Act/365, QuantLib's restated market q 2%·360/365 on Act/360 (as given
        // 0.20989021980668782, 0.18982067918415982, 0.1728096473999378)
        HestonModel counted = Model(new FlatForward(Today, 0.05, A360), new FlatForward(Today, 0.02, A365));
        var surface = new HestonBlackVolSurface(counted, AnalyticHestonEngineComplexLogFormula.AngledContour, FourierIntegration.GaussLaguerre(164));
        AssertVolatilities([0.20994269888567646, 0.18987316730980458, 0.17285458301154064], surface);

        // the density calculator on the same market at t = 1: the cdf at ln 90, ln 100 and ln 110, the density at ln 100
        // (as given 0.22922360321442198, 0.3991077948058286, 0.6206930828015621 and 2.0156912607710398)
        var calculator = new HestonRNDCalculator(counted.Process);
        AssertClose(0.22888458697902148, calculator.Cdf(Math.Log(90.0), 1.0));
        AssertClose(0.398555843816401, calculator.Cdf(Math.Log(100.0), 1.0));
        AssertClose(0.6200027740015094, calculator.Cdf(Math.Log(110.0), 1.0));
        AssertClose(2.013551702256968, calculator.Pdf(Math.Log(100.0), 1.0));
    }

    // r 5% on Act/365 from T+2
    private static FlatForward SettlingLater() => new(Today + 2, 0.05, A365);

    // v0 = theta 0.04, kappa 1.5, sigma 0.4, rho -0.6, spot 100
    private static HestonModel Model(YieldTermStructure riskFree, YieldTermStructure dividends) =>
        new(new HestonProcess(riskFree, dividends, 100.0, 0.04, 1.5, 0.04, 0.4, -0.6));

    private static PiecewiseTimeDependentHestonModel TimeDependentModel(YieldTermStructure riskFree, YieldTermStructure dividends)
    {
        double[] times = [0.25, 0.5];
        Parameter theta = new PiecewiseConstantParameter(times, new PositiveConstraint()).SetParam(0, 0.04).SetParam(1, 0.06).SetParam(2, 0.05);
        Parameter sigma = new PiecewiseConstantParameter(times, new PositiveConstraint()).SetParam(0, 0.4).SetParam(1, 0.3).SetParam(2, 0.5);
        return new PiecewiseTimeDependentHestonModel(
            riskFree,
            dividends,
            100.0,
            0.04,
            theta,
            new ConstantParameter(1.5, new PositiveConstraint()),
            sigma,
            new ConstantParameter(-0.6, new BoundaryConstraint(-1.0, 1.0)),
            new TimeGrid([0.25, 0.5, 0.75, 1.0, 1.5, 2.0]));
    }

    private static void AssertVolatilities(double[] expected, HestonBlackVolSurface surface)
    {
        double[] strikes = [90.0, 100.0, 110.0];
        for (int i = 0; i < strikes.Length; ++i)
        {
            AssertClose(expected[i], surface.BlackVol(Today + 365, strikes[i]));
        }
    }

    private static double Window(OneAssetOptionResults results) =>
        results.NPV!.Value - (results.AdditionalResults.TryGetValue("tailsValue", out object? tails) ? (double)tails : 0.0);

    private static void AssertClose(double expected, double calculated) =>
        Assert.True(
            Math.Abs(calculated - expected) <= 1e-11 * Math.Abs(expected),
            $"expected {expected}, calculated {calculated}");
}
