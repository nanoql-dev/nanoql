// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// C3's documented behaviours and refusals (the S-xeq plan's XER5 (xxvii); the run's R3-1): the no-arbitrage SABR
/// model's forward adjustment failing at extreme parameters, as QuantLib's header documents, read off
/// <see cref="NoArbSabrModel.NumericalForward"/>; the abcd curve refusing times outside its fitted tenors, as QuantLib's
/// adjustment factor does; the no-arbitrage SABR factory's traits, QuantLib's <c>global</c>; the structures' refusals
/// with QuantLib's texts, and the SABR surface's refusal of fewer rows than tenors, where QuantLib reads past them.
/// </summary>
public class ExperimentalVolatilityConventionTests
{
    private static readonly Date Today = new(15, Month.May, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void AtExtremeParametersTheForwardAdjustmentFailsAsDocumented()
    {
        // T 30, ρ 0.9, ν 0.8, σ_I 0.3: the numerical forward 0.0157 off and the call flat in strike, 0.0143261 from K 0
        // to 0.12 (QuantLib's noarbsabr.hpp:41-47; the planner's XEP20)
        double f = 0.03, beta = 0.5, alpha = 0.3 / Math.Pow(f, beta - 1.0);
        var model = new NoArbSabrModel(30.0, f, alpha, beta, 0.8, 0.9);
        Assert.Equal(-0.01567, model.NumericalForward - model.Forward, 1e-5);
        Assert.Equal(model.OptionPrice(0.0), model.OptionPrice(0.12), 1e-7);
        Assert.Equal(0.0143261, model.OptionPrice(0.06), 1e-7);
    }

    [Fact]
    public void TheNoArbitrageSabrFactoryHasQuantLibsTraits()
    {
        // QuantLib's NoArbSabr::global; one point is the fewest the fit takes, as XABR's
        Assert.Equal((true, 1), Traits<NoArbSabr>());
        var factory = new NoArbSabr(5.0, 0.03, null, 0.5, null, null, false, true, false, false);
        factory.CheckPoints(1);
        Assert.StartsWith(
            "not enough points to interpolate: at least 1 required, 0 provided",
            Assert.Throws<ArgumentException>(() => factory.CheckPoints(0)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheAbcdCurveRefusesTimesOutsideItsFittedTenors()
    {
        // QuantLib's k(t) interpolates the adjustment factors linearly between the fitted option times, unextrapolated
        Period[] tenors =
            [new(1, TimeUnit.Months), new(3, TimeUnit.Months), new(6, TimeUnit.Months), new(1, TimeUnit.Years), new(2, TimeUnit.Years)];
        AbcdAtmVolCurve curve = AbcdAtmVolCurve.Create(0, new TARGET(), tenors, [0.30, 0.28, 0.25, 0.22, 0.20], Context);
        Assert.True(double.IsFinite(curve.AtmVol(0.5)));
        Assert.StartsWith("interpolation range is", Assert.Throws<NanoQLException>(() => curve.AtmVol(0.05)).Message);
    }

    [Fact]
    public void TheStructuresRefuseWithQuantLibsTexts()
    {
        Assert.Equal(
            "empty option tenor vector",
            Assert.Throws<NanoQLException>(() => AbcdAtmVolCurve.Create(0, new TARGET(), [], [], Context)).Message);
        Assert.Equal(
            "mismatch between number of option tenors (2) and number of volatilities (1)",
            Assert.Throws<NanoQLException>(
                () => AbcdAtmVolCurve.Create(0, new TARGET(), [new(1, TimeUnit.Months), new(1, TimeUnit.Years)], [0.2], Context)).Message);
        (SabrVolSurface surface, Euribor index) = Divergences.SabrVolSurfaceFixingTests.Surface();
        Assert.Equal(
            "too few strikes (1)",
            Assert.Throws<NanoQLException>(
                () => new SabrVolSurface(index, surface.AtmCurve, [new(1, TimeUnit.Months)], [0.0], [[0.0]], Context)).Message);

        // QuantLib reads a row per option tenor unchecked, past the rows given: refused here
        Assert.Equal(
            "mismatch between number of option tenors (5) and number of rows (4)",
            Assert.Throws<NanoQLException>(
                () => new SabrVolSurface(index, surface.AtmCurve, surface.OptionTenors, [-0.01, 0.0, 0.01], [.. Enumerable.Repeat(new[] { 0.01, 0.0, -0.005 }, 4)], Context)).Message);
        Assert.Equal(
            "at least 2 surfaces are needed",
            Assert.Throws<NanoQLException>(() => new VolatilityCube([surface], [])).Message);
        AbcdAtmVolCurve spot = AbcdAtmVolCurve.Create(2, new TARGET(), surface.OptionTenors, [0.30, 0.28, 0.25, 0.22, 0.20], Context);
        var later = new SabrVolSurface(index, spot, surface.OptionTenors, [-0.01, 0.0, 0.01], [.. surface.OptionTenors.Select(_ => new[] { 0.01, 0.0, -0.005 })], Context);
        Assert.Equal(
            "different reference dates",
            Assert.Throws<NanoQLException>(() => new VolatilityCube([surface, later], [])).Message);
        Assert.Equal(
            "shift (0.01) must be zero, other shifts are not implemented yet",
            Assert.Throws<ArgumentException>(() => new NoArbSabrSmileSection(1.0, 0.03, [0.05, 0.5, 0.3, 0.0], 0.01)).Message.Split(" (Parameter")[0]);
        Assert.StartsWith(
            "beta (1) out of bounds",
            Assert.Throws<ArgumentException>(() => new NoArbSabrModel(1.0, 0.03, 0.05, 1.0, 0.3, 0.0)).Message);
    }

    private static (bool Global, int RequiredPoints) Traits<TInterpolator>()
        where TInterpolator : struct, IInterpolator =>
        (TInterpolator.Global, TInterpolator.RequiredPoints);
}
