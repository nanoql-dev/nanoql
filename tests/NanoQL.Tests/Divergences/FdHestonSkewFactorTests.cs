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
/// The Heston variance mesher's skew factor bounded at σ_m/κ = 2. QuantLib's factor, max(1, σ_m/κ)^1.5
/// (<c>fdmhestonvariancemesher.cpp:122-128</c>), widens the equity mesher at small κ until its prices leave QuantLib's
/// own analytic engine, refining the grid not bringing them back: an at-the-money put (v₀ = θ 0.04, ρ −0.7, a year, r
/// 3%, q 1%) prices 4.8106 for 3.7045 at σ 1.0 and κ 0.1, 26.48 for 3.590 at κ 0.02 (15.69 on 200 × 200 × 100) and
/// 48.52 for 6.2144 at σ 0.3 and κ 1e-8 (QuantLib-Python 1.44). The bounded factor, min(2, max(1, σ_m/κ))^1.5, brings
/// them within the grid's own error of the analytic engine, an American put converges as the grid is refined, and
/// wherever σ_m/κ ≤ 2, every QuantLib case among them, QuantLib's engine stands, the Kahl-Jaeckel ratio at the cap
/// itself.
/// </summary>
public class FdHestonSkewFactorTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Theory]
    [InlineData(1.0, 0.1, 3.704517465500574, 0.01)] // QuantLib's FD 4.810605997655163
    [InlineData(1.0, 0.02, 3.5900864653982194, 0.03)] // 26.481888707748027
    [InlineData(0.3, 1e-8, 6.214399859103596, 0.005)] // 48.51828870886835
    public void LargeRatiosPriceWithinTheGridsErrorOfTheAnalyticEngine(double sigma, double kappa, double analytic, double tolerance)
    {
        // QuantLib's analytic engine, adaptive Gauss-Lobatto at 1e-12; QuantLib's default grid, 100 x 100 x 50
        double fd = FdHestonVanillaEngine.Calculate(EuropeanPut(), Context, Model(sigma, kappa)).NPV!.Value;
        Assert.True(Math.Abs(fd - analytic) < tolerance * analytic, $"FD {fd} against the analytic engine's {analytic}");
    }

    [Fact]
    public void AnAmericanPutConvergesUnderRefinement()
    {
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new AmericanExercise(Today, Today + new Period(1, TimeUnit.Years)));
        HestonModel model = Model(1.0, 0.1);

        // the equity grid refined, 100, 200 and 400 points: 3.9206, 3.9653 and 3.9764, the steps falling by a quarter;
        // the full refinement to 400 x 400 x 200 3.9825
        double coarse = FdHestonVanillaEngine.Calculate(put, Context, model, 100, 100, 50).NPV!.Value;
        double middle = FdHestonVanillaEngine.Calculate(put, Context, model, 100, 200, 50).NPV!.Value;
        double fine = FdHestonVanillaEngine.Calculate(put, Context, model, 100, 400, 50).NPV!.Value;
        Assert.True(Math.Abs(fine - middle) < 0.5 * Math.Abs(middle - coarse), $"{coarse}, {middle}, {fine}");
        Assert.True(Math.Abs(fine - middle) < 5e-3 * fine, $"{coarse}, {middle}, {fine}");
    }

    [Theory]
    [InlineData(2.0, 1.0, 3.1453101585707093)] // the Kahl-Jaeckel ratio, 2.0, at the cap
    [InlineData(0.8, 1.16, 5.245720482781934)] // testFdVanillaVsCached's 0.69
    public void AtRatiosUpToTwoQuantLibsEngineStands(double sigma, double kappa, double quantLib)
    {
        // QuantLib's FD engine on the wheel, 100 x 100 x 50
        double fd = FdHestonVanillaEngine.Calculate(EuropeanPut(), Context, Model(sigma, kappa)).NPV!.Value;
        Assert.True(Math.Abs(fd - quantLib) <= 1e-10 * quantLib, $"FD {fd} against QuantLib's {quantLib}");
    }

    private static VanillaOption EuropeanPut() =>
        new(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + new Period(1, TimeUnit.Years)));

    // v0 = theta 0.04, rho -0.7, r 3%, q 1%, spot 100
    private static HestonModel Model(double sigma, double kappa) =>
        new(new HestonProcess(
            new FlatForward(Today, 0.03, A365), new FlatForward(Today, 0.01, A365), 100.0, 0.04, kappa, 0.04, sigma, -0.7));
}
