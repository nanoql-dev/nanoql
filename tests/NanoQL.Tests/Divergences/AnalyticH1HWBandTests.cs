// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// H1HW where its fit has no decaying exponential. The engine fits E[√v_t] ≈ a + b·e^(−ct), b = √v₀ − a, c = −log((Λ(1)
/// − a)/b) (<c>analytich1hwengine.cpp:103-134</c>): where the ratio isn't positive c is NaN and so is the price, at
/// ρ_Sr = 0 too, where H1HW is the analytic Heston-Hull-White engine; where it exceeds 1, c &lt; 0 and the fit grows
/// exponentially, on QuantLib's own ten-year parameters at v₀/θ 0.15 a call 1.70 below its FD Heston-Hull-White engine
/// (QuantLib 1.44). nanoql takes the fit's limits there: at or below 0, and where b = 0, c = +∞, the b terms dropped;
/// at or above 1, c = 0; QuantLib's fit and bits in between.
/// </summary>
public class AnalyticH1HWBandTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.04, A365);

    private static readonly VanillaOption Call =
        new(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));

    [Fact]
    public void AtNoEquityRateCorrelationH1HWIsTheAnalyticHestonHullWhiteEngine()
    {
        // κ 1.5, θ 0.04, γ 0.3: the ratio not positive from v₀ = a² to v₀/θ 0.95, QuantLib's price NaN there
        var hullWhite = new HullWhite(RiskFree, 0.1, 0.01);
        foreach (double v0 in new[] { 0.006, 0.02, 0.0325, 0.036, 0.038, 0.04, 0.06 })
        {
            var model = new HestonModel(Process(v0));
            Assert.Equal(
                AnalyticHestonHullWhiteEngine.Calculate(Call, Context, model, hullWhite, 144).NPV,
                AnalyticH1HWEngine.Calculate(Call, Context, model, hullWhite, 0.0, 144).NPV);
        }
    }

    [Fact]
    public void ThePriceIsContinuousAcrossTheBandsEdges()
    {
        // the edges in v₀: the ratio at 1 and b = 0 at v₀ = a², by QuantLib's Λ(1) on the model's parameters
        const double kappa = 1.5, theta = 0.04, gamma = 0.3;
        double a = Math.Sqrt(theta - (gamma * gamma / (8.0 * kappa)));
        foreach (double edge in new[] { Root(v0 => LambdaApprox(v0, kappa, theta, gamma) - Math.Sqrt(v0), 1e-4, 0.2), a * a })
        {
            double below = Price(edge * (1 - 1e-9)), above = Price(edge * (1 + 1e-9));
            Assert.True(Math.Abs(above - below) <= 1e-7 * above, $"v0 {edge}: {below} below, {above} above");
        }

        // at the ratio 0 QuantLib's c = -log(ratio) reaches the c = +infinity limit only as 1/log(1/ratio): the step to
        // the limit falls as v₀ nears the edge, 9e-5 relative at a relative distance of 1e-9
        double zero = Root(v0 => LambdaApprox(v0, kappa, theta, gamma) - a, 1e-4, 0.2);
        double limit = Price(zero * (1 - 1e-9));
        double[] steps = [.. new[] { 1e-3, 1e-6, 1e-9, 1e-12 }.Select(e => Math.Abs(Price(zero * (1 + e)) - limit))];
        for (int i = 1; i < steps.Length; ++i)
        {
            Assert.True(steps[i] < steps[i - 1], $"steps {string.Join(", ", steps)}");
        }

        Assert.True(steps[^1] <= 1e-4 * limit, $"steps {string.Join(", ", steps)}");
    }

    [Fact]
    public void OnQuantLibsTenYearParametersH1HWHoldsTheFiniteDifferenceEngine()
    {
        // QuantLib's hybridhestonhullwhiteprocess test's H1-HW case at v₀/θ 0.15, its vol of variance 0.3: QuantLib's
        // FD Heston-Hull-White engine 32.471049627116756, its H1HW 30.774986404609777, 1.70 below; inside the decaying
        // band H1HW is 0.118 below the FD engine at v₀ 0.02 and 0.107 at 0.025, 0.040 at 0.04
        var today = new Date(15, July, 2012);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure r = new FlatForward(today, 0.02, A365);
        var model = new HestonModel(new HestonProcess(r, new FlatForward(today, 0.0, A365), 100.0, 0.0075, 0.3, 0.05, 0.3, -0.3));
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(new Date(13, July, 2022)));
        double h1hw = AnalyticH1HWEngine.Calculate(call, context, model, new HullWhite(r, 0.01, 0.01), 0.6, 144).NPV!.Value;
        Assert.True(Math.Abs(h1hw - 32.471049627116756) <= 0.4, $"H1HW {h1hw}");
    }

    private static double Price(double v0) =>
        AnalyticH1HWEngine.Calculate(Call, Context, new HestonModel(Process(v0)), new HullWhite(RiskFree, 0.1, 0.01), 0.6, 144).NPV!.Value;

    private static HestonProcess Process(double v0) =>
        new(RiskFree, new FlatForward(Today, 0.02, A365), 100.0, v0, 1.5, 0.04, 0.3, -0.7);

    // QuantLib's LambdaApprox(1) (analytich1hwengine.cpp)
    private static double LambdaApprox(double v0, double kappa, double theta, double gamma)
    {
        double d = 4.0 * kappa * theta / (gamma * gamma);
        double c = gamma * gamma / (4 * kappa) * (1.0 - Math.Exp(-kappa));
        double lambda = 4.0 * kappa * v0 * Math.Exp(-kappa) / (gamma * gamma * (1.0 - Math.Exp(-kappa)));
        return Math.Sqrt((c * (lambda - 1.0)) + (c * d * (1.0 + (1.0 / (2.0 * (d + lambda))))));
    }

    // bisection for the single sign change of f on [lo, hi]
    private static double Root(Func<double, double> f, double lo, double hi)
    {
        double flo = f(lo);
        Assert.True(flo * f(hi) < 0.0, "no sign change");
        for (int i = 0; i < 200 && hi - lo > 1e-15 * hi; ++i)
        {
            double mid = 0.5 * (lo + hi);
            double fmid = f(mid);
            if (fmid * flo > 0.0)
            {
                (lo, flo) = (mid, fmid);
            }
            else
            {
                hi = mid;
            }
        }

        return 0.5 * (lo + hi);
    }
}
