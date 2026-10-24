// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="HestonProcess.Pdf"/>, the joint density of the log spot and the variance (a run find of S-van's C3, ruled
/// by the user on 2026-10-24: fixed). QuantLib's (<c>hestonprocess.cpp:300-334</c>) integrates over the integrated
/// variance up to twice a Cornish-Fisher quantile whose skew and kurtosis are finite differences of the characteristic
/// function at a step of 1e-2: they rest on its last bits (one ulp on v moves QuantLib's value at κ 2, a quarter of a
/// year, from −5.27 to 9.31), and the cut-off can fall where the integrand is still a fifth of its peak; and its
/// 128-point Gauss-Laguerre rule stops near u = 500, where at short times the characteristic function hasn't decayed.
/// nanoql inverts the log spot's characteristic function given the variance, from the integrated variance's. The
/// checks: the log spot's marginal, the density integrated over the variance, is the log spot's density from Heston's
/// characteristic function as QuantLib's analytic Heston engine computes it (<see cref="AnalyticHestonEngine.ChF"/>),
/// inverted here, on a process meeting Feller's condition and one far from it, from under three weeks to a year; and
/// where QuantLib's range holds the integrand, QuantLib's value [executed on the C++ harness and QuantLib's own
/// functions: <c>scratchpad\c3\heston_harness\</c>, <c>scratchpad\c3\probe\heston_pdf.cpp</c>; r and q on flat
/// Actual/365 (Fixed) curves from 15 October 2026].
/// </summary>
public class HestonPdfTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    // (r, q, v0, κ, θ, σ, ρ): the third with 2κθ/σ² = 0.25, far from Feller's condition
    private static readonly (double R, double Q, double V0, double Kappa, double Theta, double Sigma, double Rho)[] Processes =
        [
            (0.05, 0.02, 0.04, 1.5, 0.04, 0.3, -0.7),
            (0.03, 0.0, 0.09, 2.0, 0.06, 0.5, 0.3),
            (0.0, 0.01, 0.02, 0.5, 0.03, 0.2, 0.0),
            (0.03, 0.0, 0.04, 2.0, 0.04, 0.8, -0.7),
        ];

    [Theory]
    [InlineData(0, 0.25, 100.0, double.NaN)] // QuantLib's NaN
    [InlineData(0, 0.002, 99.0, double.NaN)] // under a day out: the Bessel ratio's terms overflow unweighted
    [InlineData(1, 0.25, 100.0, -6.242593166865453)] // QuantLib's negative density
    [InlineData(2, 0.25, 80.0, -188.238818614485)]
    [InlineData(0, 1.0, 80.0, 7.3627154951217575)] // QuantLib's range cut short, 10% low
    [InlineData(1, 2.0, 120.0, 5.756762708863868)]
    public void WhereQuantLibsDensityFailsItIsPositive(int process, double t, double spot, double quantLib)
    {
        // at v = θ, QuantLib's values recorded
        _ = quantLib;
        double value = Process(process).Pdf(Math.Log(spot), Processes[process].Theta, t);
        Assert.True(value > 0.0 && double.IsFinite(value), $"{value}");
    }

    [Theory]
    [InlineData(0, 0.05, 90.0)]
    [InlineData(0, 0.05, 110.0)]
    [InlineData(0, 0.25, 100.0)]
    [InlineData(0, 1.0, 90.0)]
    [InlineData(0, 1.0, 110.0)]
    [InlineData(0, 1.0, 60.0)] // the tails
    [InlineData(0, 1.0, 160.0)]
    [InlineData(3, 0.05, 100.0)]
    [InlineData(3, 0.25, 90.0)]
    [InlineData(3, 0.25, 110.0)]
    [InlineData(3, 1.0, 100.0)]
    [InlineData(3, 1.0, 60.0)]
    [InlineData(3, 1.0, 160.0)]
    public void TheLogSpotsMarginalIsTheCharacteristicFunctionsDensity(int process, double t, double spot)
    {
        // the joint density integrated over v, against the inversion of QuantLib's characteristic function
        (_, _, double v0, double kappa, double theta, double sigma, _) = Processes[process];
        HestonProcess p = Process(process);
        double x = Math.Log(spot);

        // v = w^power takes the density's v^ν at 0, ν = 2κθ/σ² − 1, to w³; v up to 48θ + 24v₀, past the variance's
        // tail given the far spots
        double nu = (2 * kappa * theta / (sigma * sigma)) - 1;
        double power = 4.0 / (nu + 1);
        double wHi = Math.Pow((48.0 * theta) + (24.0 * v0), 1.0 / power);
        double marginal = Integrate(w => p.Pdf(x, Math.Pow(w, power), t) * power * Math.Pow(w, power - 1), 0.0, wHi, 32);

        double density = CharacteristicFunctionDensity(p, x, t);
        Assert.True(Math.Abs((marginal / density) - 1.0) <= 1e-8, $"{marginal} against {density}");
    }

    [Theory]
    [InlineData(0, 2.0, 80.0, 0.5, 7.68425881454564)]
    [InlineData(0, 2.0, 80.0, 1.0, 8.370798519893174)]
    [InlineData(0, 2.0, 100.0, 1.0, 17.541230338797703)]
    [InlineData(0, 2.0, 120.0, 1.5, 7.448936679617928)]
    [InlineData(1, 2.0, 100.0, 1.0, 7.344028488570205)]
    public void WhereQuantLibsRangeHoldsTheIntegrandItsValueStands(int process, double t, double spot, double m, double quantLib)
    {
        // two years out, where QuantLib's cut-off falls past the integrand's mass: within 1e-4 of QuantLib's value
        double value = Process(process).Pdf(Math.Log(spot), Processes[process].Theta * m, t);
        Assert.True(Math.Abs((value / quantLib) - 1.0) <= 1e-4, $"{value} against QuantLib's {quantLib}");
    }

    [Fact]
    public void WhereTheVariancesDensityIsZeroTheDensityIsZero()
    {
        // under an hour out, five times θ: the variance's density underflows to 0
        Assert.Equal(0.0, Process(0).Pdf(Math.Log(100.0), 5 * Processes[0].Theta, 1e-4));
    }

    private static HestonProcess Process(int i)
    {
        (double r, double q, double v0, double kappa, double theta, double sigma, double rho) = Processes[i];
        DayCounter dc = new Actual365Fixed();
        return new HestonProcess(
            new FlatForward(Today, r, dc), new FlatForward(Today, q, dc), 100.0, v0, kappa, theta, sigma, rho);
    }

    // the log spot's density: (1/π)∫ Re[e^{−iu(x − ln F)}·φ(u)] du over u ≥ 0, φ the analytic Heston engine's
    // characteristic function of ln(S/F), by 32-point Gauss-Legendre panels of width 2 until it has decayed
    private static double CharacteristicFunctionDensity(HestonProcess p, double x, double t)
    {
        var engine = new AnalyticHestonEngine(new HestonModel(p));
        double y = x - Math.Log(p.S0 * p.DividendYield.Discount(t) / p.RiskFreeRate.Discount(t));
        var rule = new GaussLegendreIntegration(32);
        double sum = 0.0;
        for (double lo = 0.0; lo < 1e5; lo += 2.0)
        {
            double from = lo;
            sum += rule.Evaluate(s =>
            {
                double u = from + 1.0 + s;
                return (Complex.Exp(new Complex(0.0, -u * y)) * engine.ChF(new Complex(u, 0.0), t)).Real;
            });
            if (Complex.Abs(engine.ChF(new Complex(lo + 2.0, 0.0), t)) < 1e-17)
            {
                break;
            }
        }

        return sum / Math.PI;
    }

    // composite Gauss-Legendre: panels of 16 nodes
    private static double Integrate(Func<double, double> f, double a, double b, int panels)
    {
        var rule = new GaussLegendreIntegration(16);
        double width = (b - a) / panels, sum = 0.0;
        for (int i = 0; i < panels; i++)
        {
            double lo = a + (i * width);
            sum += 0.5 * width * rule.Evaluate(u => f(lo + (0.5 * width * (u + 1))));
        }

        return sum;
    }
}
