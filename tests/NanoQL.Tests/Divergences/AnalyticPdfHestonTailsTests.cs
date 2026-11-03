// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The analytic PDF Heston engine's tails beyond its window. QuantLib integrates the payoff against the density over ln
/// F ± 8√V (<c>analyticpdfhestonengine.cpp:54-65</c>) and drops the mass beyond: on its own markets a half-year put at
/// 0.7F 7% low and at 0.5F 53% low (the far-from-Feller set), at a vol of variance of 1 and correlation −0.9 a
/// five-year put at half the forward 18% low (QuantLib-Python 1.44); its own test checks calls, whose right tail is
/// thin there. nanoql adds the tails' values where they're worth more than the integrations' accuracy: the payoff a
/// line a + bS between its strikes, a piece's value is a times its probability plus bF times its probability under the
/// share measure, both from the spot's distribution at the window's edges and the strikes; QuantLib's price where they
/// aren't. The window's value is QuantLib's to the bit, the price less the additional result <c>tailsValue</c>. The
/// check: QuantLib's analytic engine.
/// </summary>
public class AnalyticPdfHestonTailsTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AFatLeftTailIsAdded()
    {
        // zero carry, where QuantLib's window is nanoql's: QuantLib's PDF engine and its analytic engine, on the wheel
        HestonModel model = FatLeft();
        (int Days, double Strike, double QuantLib, double Analytic)[] puts =
        [
            (365, 50.0, 0.860670333289312, 0.9857127522709248), (365, 70.0, 2.319324719588261, 2.5008697914824545),
            (365, 100.0, 7.181108282454663, 7.447398975865861), (365, 140.0, 38.46008168005591, 38.839372301189925),
            (1825, 50.0, 2.3317051849291968, 2.8530034587005884), (1825, 70.0, 4.4964438217283895, 5.226761164700512),
            (1825, 100.0, 10.24210993508242, 11.285960054659613), (1825, 140.0, 33.263801551854456, 34.72569324323011),
        ];
        foreach ((int days, double strike, double quantLib, double analytic) in puts)
        {
            OneAssetOptionResults results = Price(model, new PlainVanillaPayoff(OptionType.Put, strike), days);
            AssertWindowIsQuantLibs(quantLib, results);
            AssertNear(analytic, results.NPV!.Value);
        }
    }

    [Fact]
    public void OnQuantLibsVectorMarketsThePricesAreTheAnalyticEngines()
    {
        // the vector markets (tools/vectors/gen/processes/hestonprocess.py's equity and far-from-Feller sets, flat 4%):
        // the far-from-Feller half-year put at 0.7F, QuantLib's PDF engine 0.10625734876109928 for 0.11426930888190326
        foreach (HestonModel model in new[] { Model(0.04, 100.0, 0.04, 1.5, 0.04, 0.3, -0.7), Model(0.04, 120.0, 0.02, 3.0, 0.03, 0.9, -0.2) })
        {
            double s0 = model.Process.S0;
            foreach (int days in new[] { 182, 730 })
            {
                foreach (double moneyness in new[] { 0.5, 0.7, 0.9, 1.0, 1.1, 1.4, 2.0 })
                {
                    foreach (OptionType type in new[] { OptionType.Put, OptionType.Call })
                    {
                        var payoff = new PlainVanillaPayoff(type, moneyness * s0);
                        double analytic = AnalyticHestonEngine.Calculate(Option(payoff, days), Context, model, 192).NPV!.Value;
                        AssertNear(analytic, Price(model, payoff, days).NPV!.Value);
                    }
                }
            }
        }

        AssertWindowIsQuantLibs(
            0.10625734876109928, Price(Model(0.04, 120.0, 0.02, 3.0, 0.03, 0.9, -0.2), new PlainVanillaPayoff(OptionType.Put, 84.0), 182));
    }

    [Fact]
    public void DigitalAndAssetPutsTakeTheirTails()
    {
        // a cash-or-nothing put against the analytic engine's put spread, an asset-or-nothing put by S 1{S < K} =
        // K 1{S < K} - (K - S)+; QuantLib's PDF engine, on the wheel
        HestonModel model = FatLeft();
        (int Days, double Strike, double Cash, double Asset)[] cases =
        [
            (365, 50.0, 0.05128053923848205, 1.7033738122953634), (365, 100.0, 0.27932904205449205, 20.751803547906512),
            (1825, 50.0, 0.08708098930086086, 2.022343219679602), (1825, 100.0, 0.27738728499272486, 17.496615932086158),
        ];
        foreach ((int days, double strike, double cashQuantLib, double assetQuantLib) in cases)
        {
            const double Eps = 0.01;
            double put = Analytic(model, strike, days);
            double cash = (Analytic(model, strike + Eps, days) - Analytic(model, strike - Eps, days)) / (2.0 * Eps);
            OneAssetOptionResults digital = Price(model, new CashOrNothingPayoff(OptionType.Put, strike, 1.0), days);
            AssertWindowIsQuantLibs(cashQuantLib, digital);
            AssertNear(cash, digital.NPV!.Value);
            OneAssetOptionResults asset = Price(model, new AssetOrNothingPayoff(OptionType.Put, strike), days);
            AssertWindowIsQuantLibs(assetQuantLib, asset);
            AssertNear((strike * cash) - put, asset.NPV!.Value);
        }
    }

    [Fact]
    public void AStrikeBeyondTheWindowSplitsItsTail()
    {
        // the year's window starts at 0.091F: puts and cash-or-nothing puts struck at 0.05F and 0.08F, whose payoffs
        // change their line inside the tail
        HestonModel model = FatLeft();
        foreach (double strike in new[] { 5.0, 8.0 })
        {
            const double Eps = 0.01;
            AssertNear(Analytic(model, strike, 365), Price(model, new PlainVanillaPayoff(OptionType.Put, strike), 365).NPV!.Value);
            double cash = (Analytic(model, strike + Eps, 365) - Analytic(model, strike - Eps, 365)) / (2.0 * Eps);
            AssertNear(cash, Price(model, new CashOrNothingPayoff(OptionType.Put, strike, 1.0), 365).NPV!.Value);
        }
    }

    [Fact]
    public void WhereTheTailsAreWithinTheAccuracyQuantLibsPriceStands()
    {
        // the equity set's calls, whose right tail is thin: QuantLib's PDF engine, on the wheel
        HestonModel model = Model(0.04, 100.0, 0.04, 1.5, 0.04, 0.3, -0.7);
        foreach ((int days, double strike, double quantLib) in new[]
        {
            (182, 70.0, 29.549695817071495), (182, 100.0, 5.30941674967662), (365, 140.0, 0.0626553256775622),
        })
        {
            OneAssetOptionResults call = Price(model, new PlainVanillaPayoff(OptionType.Call, strike), days);
            Assert.Equal(quantLib, call.NPV!.Value, 1e-10 * Math.Max(quantLib, 1.0));
            Assert.False(call.AdditionalResults.ContainsKey("tailsValue"));
        }
    }

    [Fact]
    public void ATailBelowTheDistributionsResolutionOrWorthLessThanTheAccuracyIsLeftOut()
    {
        // a thin left tail's probabilities within the integrations' accuracy, though a deep put pays up to 1000 there;
        // and the equity set at a spot of 0.5, whose asset-or-nothing put's left tail is worth less than the accuracy
        // where the plain put's isn't: QuantLib's PDF engine, on the wheel
        HestonModel right = Model(0.03, 100.0, 0.09, 1.5, 0.09, 1.0, 0.9);
        HestonModel small = Model(0.04, 0.5, 0.04, 1.5, 0.04, 0.3, -0.7);
        foreach ((HestonModel model, StrikedTypePayoff payoff, int days, double quantLib) in new (HestonModel, StrikedTypePayoff, int, double)[]
        {
            (right, new PlainVanillaPayoff(OptionType.Put, 300.0), 365, 195.2371330870444),
            (right, new PlainVanillaPayoff(OptionType.Put, 1000.0), 365, 873.5265230091726),
            (small, new AssetOrNothingPayoff(OptionType.Put, 0.5), 182, 0.20242610163930938),
        })
        {
            OneAssetOptionResults results = Price(model, payoff, days);
            Assert.Equal(quantLib, results.NPV!.Value, 1e-10 * Math.Max(quantLib, 1.0));
            Assert.False(results.AdditionalResults.ContainsKey("tailsValue"));
        }

        OneAssetOptionResults plain = Price(small, new PlainVanillaPayoff(OptionType.Put, 0.5), 182);
        AssertWindowIsQuantLibs(0.02654570853185237, plain);
        Assert.True(plain.AdditionalResults.ContainsKey("tailsValue"));
    }

    [Fact]
    public void APayoffNotLinearOnATailWithMassIsRefused()
    {
        // S^2/100, refused where a tail's mass is beyond the accuracy; on thin tails the window's value
        var square = new SquarePayoff();
        NanoQLException e = Assert.Throws<NanoQLException>(() => Price(FatLeft(), square, 365));
        Assert.Equal("the payoff isn't linear on a tail outside the integration's window", e.Message);
        OneAssetOptionResults thin = Price(Thin(), square, 91);
        Assert.False(thin.AdditionalResults.ContainsKey("tailsValue"));
        Assert.True(thin.NPV > 100.0);
    }

    [Fact]
    public void TheShareMeasuresDistributionKeepsPutCallParity()
    {
        // E[S; S <= K] = F Q(K), so K P(K) - F Q(K) is the put over the discount, the analytic engine's, on fat tails of
        // both signs
        MethodInfo shareCdf = typeof(HestonRNDCalculator).GetMethod("ShareCdf", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (HestonModel model in new[] { FatLeft(), Model(0.03, 100.0, 0.09, 1.5, 0.09, 1.0, 0.9) })
        {
            var calculator = new HestonRNDCalculator(model.Process);
            foreach (int days in new[] { 91, 365, 1825 })
            {
                double t = days / 365.0, discount = model.Process.RiskFreeRate.Discount(t);
                foreach (double strike in new[] { 30.0, 50.0, 80.0, 100.0, 125.0, 200.0, 300.0 })
                {
                    double p = calculator.Cdf(Math.Log(strike), t);
                    double q = (double)shareCdf.Invoke(calculator, [Math.Log(strike), t])!;
                    double put = Analytic(model, strike, days) / discount;
                    Assert.True(Math.Abs((strike * p) - (100.0 * q) - put) <= 2e-5 * Math.Max(put, 1.0), $"{days}d K {strike}: {(strike * p) - (100.0 * q)} for {put}");
                }
            }
        }
    }

    private static void AssertWindowIsQuantLibs(double quantLib, OneAssetOptionResults results)
    {
        double tails = results.AdditionalResults.TryGetValue("tailsValue", out object? value) ? (double)value : 0.0;
        double window = results.NPV!.Value - tails;
        Assert.True(Math.Abs(window - quantLib) <= 1e-10 * Math.Max(Math.Abs(quantLib), 1.0), $"window {window} for QuantLib's {quantLib}");
    }

    private static void AssertNear(double expected, double actual) =>
        Assert.True(Math.Abs(actual - expected) <= 2e-5 * Math.Max(Math.Abs(expected), 1.0), $"{actual} for {expected}");

    private static double Analytic(HestonModel model, double strike, int days) =>
        AnalyticHestonEngine.Calculate(Option(new PlainVanillaPayoff(OptionType.Put, strike), days), Context, model, 192).NPV!.Value;

    private static OneAssetOptionResults Price(HestonModel model, StrikedTypePayoff payoff, int days) =>
        AnalyticPDFHestonEngine.Calculate(Option(payoff, days), Context, model);

    private static VanillaOption Option(StrikedTypePayoff payoff, int days) => new(payoff, new EuropeanExercise(Today + days));

    // v0 = theta 0.09, kappa 0.3, vol of variance 1, rho -0.9, at zero carry (r = q = 3%)
    private static HestonModel FatLeft() => Model(0.03, 100.0, 0.09, 0.3, 0.09, 1.0, -0.9);

    // v0 = theta 0.04, kappa 2, vol of variance 0.1, rho 0, at zero carry
    private static HestonModel Thin() => Model(0.03, 100.0, 0.04, 2.0, 0.04, 0.1, 0.0);

    private static HestonModel Model(double r, double s0, double v0, double kappa, double theta, double sigma, double rho)
    {
        var curve = new FlatForward(Today, r, A365);
        return new HestonModel(new HestonProcess(curve, curve, s0, v0, kappa, theta, sigma, rho));
    }

    private sealed class SquarePayoff() : StrikedTypePayoff(OptionType.Call, 100.0)
    {
        public override string Name => "Square";

        public override string Description => "Square";

        public override double Evaluate(double price) => price * price / 100.0;
    }
}
