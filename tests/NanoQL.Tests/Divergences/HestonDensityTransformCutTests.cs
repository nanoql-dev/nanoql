// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Where the Heston density's Fourier integral stops (R8; found by the S-hes code review, C2-B1-1). QuantLib's density
/// and distribution integrate the characteristic function after the substitution u = e^{−cx}, returning 0 below
/// u = QL_EPSILON (<c>hestonrndcalculator.cpp:59-86</c>), so the integral stops at x = −ln ε/c, with
/// c = min(10, max(1e-4, √(1 − ρ²)/σ))·(v₀ + κθt). At a small vol of variance over long terms, and at short terms, the
/// characteristic function, about e^{−Vx²/2} with V the expected integrated variance, is still large there: QuantLib's
/// PDF engine prices a twenty-year call at twice the forward at 6.81 where its analytic engine gives 0.97. nanoql caps
/// c at √(−ln ε·V/2), so the cut falls where e^{−Vx²/2} is below QL_EPSILON, V the PDF engine's window variance; where
/// QuantLib's constant already cuts below it, QuantLib's constant and bits. The check: QuantLib's analytic engine for the
/// prices, its strike derivative for the distribution, and QuantLib's density where the cap is idle.
/// </summary>
public class HestonDensityTransformCutTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void WhereQuantLibsCutComesEarlyThePdfEngineMatchesTheAnalyticEngine()
    {
        // v0 = theta 0.01, kappa 5, sigma 0.1, rho 0.5, r = q = 3%, twenty years: QuantLib 1.44's PDF engine prices
        // 34.004253651467685, 15.956030456988453 and 6.813958198513366 for the calls, 0.34597305311381255,
        // 9.789855766476068 and 55.63850344441907 for the puts; its analytic engine at 192 points [executed on the wheel]
        HestonModel model = Model(0.01, 5.0, 0.1, 0.5);
        foreach ((double strike, double call, double put) in new[]
                 {
                     (50.0, 27.856497802243645, 0.4159159975423214),
                     (100.0, 9.731172221025615, 9.731172221025615),
                     (200.0, 0.9721212420540035, 55.85328485145665),
                 })
        {
            foreach ((OptionType type, double analytic) in new[] { (OptionType.Call, call), (OptionType.Put, put) })
            {
                var option = new VanillaOption(new PlainVanillaPayoff(type, strike), new EuropeanExercise(Today + 7300));
                Assert.Equal(analytic, AnalyticHestonEngine.Calculate(option, Context, model, 192).NPV!.Value, 1e-12);
                double pdf = AnalyticPDFHestonEngine.Calculate(option, Context, model).NPV!.Value;
                Assert.True(Math.Abs((pdf / analytic) - 1.0) < 1e-6, $"{type} {strike}: {pdf} against {analytic}");
            }
        }
    }

    [Fact]
    public void WhereQuantLibsCutComesEarlyTheDistributionMatchesTheAnalyticDigital()
    {
        // the same model at ten years: QuantLib 1.44's distribution is 0.017119486599951883, 0.5679509415571753 and
        // 0.9883307473830997 at 50, 100 and 200 [executed on the wheel]; the analytic engine's digital
        // 1 + dC/dK / D_r, the derivative by central differences of 1e-3 K
        HestonModel model = Model(0.01, 5.0, 0.1, 0.5);
        Date maturity = Today + 3650;
        double t = Curve.TimeFromReference(maturity);
        var calculator = new HestonRNDCalculator(model.Process);
        foreach (double strike in new[] { 50.0, 100.0, 200.0 })
        {
            double h = 1e-3 * strike;
            double up = Price(model, strike + h, maturity), down = Price(model, strike - h, maturity);
            double digital = 1.0 + ((up - down) / (2.0 * h) / Curve.Discount(maturity));
            Assert.Equal(digital, calculator.Cdf(Math.Log(strike), t), 1e-6);
        }
    }

    [Fact]
    public void WhereQuantLibsCutComesLateTheDensityIsQuantLibs()
    {
        // v0 = theta 0.04, kappa 1.5, sigma 0.3, rho -0.5 at twenty years, where QuantLib's constant already cuts
        // below QL_EPSILON: QuantLib 1.44's density [executed on the wheel], within its complex functions' last bits
        var calculator = new HestonRNDCalculator(Model(0.04, 1.5, 0.3, -0.5).Process);
        double t = Curve.TimeFromReference(Today + 7300);
        foreach ((double spot, double quantLib) in new[] { (60.0, 0.4169636439861203), (100.0, 0.42110792454399076), (160.0, 0.31101667660133536) })
        {
            Assert.Equal(quantLib, calculator.Pdf(Math.Log(spot), t), 1e-14);
        }
    }

    // v0 = theta, r = q = 3%, spot 100
    private static HestonModel Model(double v0, double kappa, double sigma, double rho) =>
        new(new HestonProcess(Curve, Curve, 100.0, v0, kappa, v0, sigma, rho));

    private static double Price(HestonModel model, double strike, Date maturity) =>
        AnalyticHestonEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturity)), Context, model, 192).NPV!.Value;
}
