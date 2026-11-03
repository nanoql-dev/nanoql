// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Heston geometric Asian engine's refusal at vanishing variance near the forward (by its cut alone): where the
/// switch to the intrinsic doesn't fire and the damped contour's cut, where <c>Phi</c> decays, lies past ξ 1e7, the
/// engine refuses before the contour runs, with "variance too small for the Heston geometric engine near the forward".
/// The market: r 5%, q 0, κ 2, vol of vol 0.1, ρ −0.5, v₀ = θ. At spot 0.01, a month of six fixings at variance 1e-8,
/// the strike 5e-5 below the forward, the cut is 4.25e8: refused. On the cut's other side, spot 100, a day at variance
/// 1e-6 with one fixing, the strike 5e-5 above the forward, the cut 4.9e6: priced, within the vector tolerance of
/// QUADPACK on the route's contour, where QuantLib's <c>AnalyticHestonEngine</c> doesn't converge (a second contour
/// agreeing to 8e-17).
/// </summary>
public class HestonGeometricAsianRefusalTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void PastTheCutTheEngineRefuses()
    {
        HestonProcess process = Market(0.01, 1e-8);
        Date[] fixings = [.. Enumerable.Range(1, 6).Select(i => Today + (5 * i))];
        var option = new DiscreteAveragingAsianOption(
            AverageType.Geometric, 1.0, 0, fixings, new PlainVanillaPayoff(OptionType.Call, 0.01002350015926445), new EuropeanExercise(Today + 30));
        NanoQLException e = Assert.Throws<NanoQLException>(() => AnalyticDiscreteGeometricAveragePriceAsianHestonEngine.Calculate(option, Context, process));
        Assert.Equal("variance too small for the Heston geometric engine near the forward", e.Message);
    }

    [Fact]
    public void OnTheCutsOtherSideTheEnginePrices()
    {
        HestonProcess process = Market(100.0, 1e-6);
        double forward = 100.0 / process.RiskFreeRate.Discount(Today + 1);
        var option = new DiscreteAveragingAsianOption(
            AverageType.Geometric, 1.0, 0, [Today + 1], new PlainVanillaPayoff(OptionType.Call, forward * (1 + 5e-5)), new EuropeanExercise(Today + 1));
        double price = AnalyticDiscreteGeometricAveragePriceAsianHestonEngine.Calculate(option, Context, process).NPV!.Value;
        double scale = Math.Max(0.0002210102732734068, process.RiskFreeRate.Discount(Today + 1) * forward / 100.0);
        Assert.True(Math.Abs(price - 0.0002210102732734068) <= 1e-10 * scale, $"{price:R}");
    }

    private static HestonProcess Market(double spot, double variance) =>
        new(new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.0, A365), spot, variance, 2.0, variance, 0.1, -0.5);
}
