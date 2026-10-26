// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using static NanoQL.Tests.Divergences.Fd2dMismatchedTimesTests;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The basket core's members no vector or QuantLib case reads: a basket payoff's name and description, its base
/// payoff's (QuantLib's <c>BasketPayoff</c>); an expired multi-asset option's results, the value and every Greek 0
/// (QuantLib's <c>MultiAssetOption::setupExpired</c>); the spline's grid, the one given; and Halley's solver from a guess
/// and a step over delegates, the root its bracketed form finds.
/// </summary>
public class BasketCoreMemberTests
{
    [Fact]
    public void ABasketPayoffReadsItsBasePayoffsNameAndDescription()
    {
        var call = new PlainVanillaPayoff(OptionType.Call, 100.0);
        var basket = new MinBasketPayoff(call);
        Assert.Equal(call.Name, basket.Name);
        Assert.Equal(call.Description, basket.Description);
        Assert.Equal("Vanilla Call, 100 strike", basket.Description);
    }

    [Fact]
    public void AnExpiredOptionsValueAndGreeksAreZero()
    {
        // expiring the day before the evaluation date
        var context = new EvaluationContext { EvaluationDate = new(16, Time.Month.October, 2026) };
        var option = new BasketOption(AveragePut(), new EuropeanExercise(context.EvaluationDate - 1));
        MultiAssetOptionResults r = ChoiBasketEngine.Calculate(
            option,
            context,
            [Process(100.0, Flat(0.02), Flat(0.05), Vol(0.25)), Process(95.0, Flat(0.03), Flat(0.05), Vol(0.3))],
            new Matrix(new[,] { { 1.0, 0.5 }, { 0.5, 1.0 } }));
        Assert.Equal(0.0, r.NPV);
        Assert.Equal(0.0, r.ErrorEstimate);
        Assert.Equal(0.0, r.Delta);
        Assert.Equal(0.0, r.Gamma);
        Assert.Equal(0.0, r.Theta);
        Assert.Equal(0.0, r.Vega);
        Assert.Equal(0.0, r.Rho);
        Assert.Equal(0.0, r.DividendRho);
    }

    [Fact]
    public void TheSplinesGridIsTheOneGiven()
    {
        double[] x = [0.0, 1.0, 2.0, 3.0], y = [-1.0, 0.0, 0.5, 2.0];
        var spline = new MultiCubicSpline([[.. x], [.. y]], new double[16]);
        Assert.Equal(x, spline.Grid[0]);
        Assert.Equal(y, spline.Grid[1]);
    }

    [Fact]
    public void HalleyFromAStepFindsTheBracketedRoot()
    {
        var halley = new Halley();
        double fromStep = halley.Solve(x => (x * x * x) - 2.0, x => 3.0 * x * x, x => 6.0 * x, 1e-12, 1.0, 0.1);
        double bracketed = halley.Solve(x => (x * x * x) - 2.0, x => 3.0 * x * x, x => 6.0 * x, 1e-12, 1.0, 0.0, 3.0);
        Assert.Equal(Math.Cbrt(2.0), fromStep, 1e-12);
        Assert.Equal(bracketed, fromStep, 1e-12);
    }
}
