// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// GARCH's cost gradient (the S-bar plan's BR5 (xlii), fixed under R8). QuantLib's <c>Garch11CostFunction::gradient</c>
/// and <c>valueAndGradient</c> take ∂σ²ₜ/∂θ as its direct term (1, u²ₜ₋₁, σ²ₜ₋₁), dropping β·∂σ²ₜ₋₁/∂θ
/// (<c>garch.cpp:95-138</c>), so against central differences of its own value it runs at 0.13 to 0.80 times the true
/// gradient and a gradient method stops short of the optimum: L-BFGS-B from QuantLib's start at cost 0.46017717 where
/// the exact gradient reaches Nelder-Mead's optimum, 0.46008564 (the planner's BP30). QuantLib's own paths, the Simplex
/// and the ACF fit, never read it, so its cases don't move. nanoql's gradient is the recursive one, ∂σ²ₜ/∂θ = (1,
/// u²ₜ₋₁, σ²ₜ₋₁) + β ∂σ²ₜ₋₁/∂θ. Checks, on QuantLib's own calibration series: the conjugate gradient from
/// (ω, α, β) = (0.2, 0.2, 0.2) ends where the cost's central differences vanish, and at the Simplex's optimum.
/// </summary>
public class GarchGradientTests
{
    private static readonly (ImmutableArray<double> R2, double Mean) Squared = Garch11.ToR2(GarchSeries.Values);

    [Fact]
    public void TheConjugateGradientEndsWhereTheCostIsStationary()
    {
        Garch11Calibration cg = ConjugateGradient();
        double[] values = GarchSeries.Values;
        const double h = 1e-6;
        double gw = (Garch11.CostFunction(values, cg.Alpha, cg.Beta, cg.Omega + h) - Garch11.CostFunction(values, cg.Alpha, cg.Beta, cg.Omega - h)) / (2 * h);
        double ga = (Garch11.CostFunction(values, cg.Alpha + h, cg.Beta, cg.Omega) - Garch11.CostFunction(values, cg.Alpha - h, cg.Beta, cg.Omega)) / (2 * h);
        double gb = (Garch11.CostFunction(values, cg.Alpha, cg.Beta + h, cg.Omega) - Garch11.CostFunction(values, cg.Alpha, cg.Beta - h, cg.Omega)) / (2 * h);
        Assert.True(Math.Abs(gw) < 1e-5 && Math.Abs(ga) < 1e-5 && Math.Abs(gb) < 1e-5, $"{gw} {ga} {gb}");
    }

    [Fact]
    public void TheConjugateGradientReachesTheSimplexOptimum()
    {
        Garch11Calibration cg = ConjugateGradient();
        Garch11Calibration simplex = Garch11.CalibrateR2(Garch11Mode.BestOfTwo, Squared.R2.AsSpan(), Squared.Mean);
        double[] values = GarchSeries.Values;
        Assert.Equal(
            Garch11.CostFunction(values, simplex.Alpha, simplex.Beta, simplex.Omega), Garch11.CostFunction(values, cg.Alpha, cg.Beta, cg.Omega), 1e-9);
        Assert.Equal(simplex.Alpha, cg.Alpha, 1e-4);
        Assert.Equal(simplex.Beta, cg.Beta, 1e-4);
        Assert.Equal(simplex.Omega, cg.Omega, 1e-4);
    }

    private static Garch11Calibration ConjugateGradient() =>
        Garch11.CalibrateR2(Squared.R2.AsSpan(), new ConjugateGradient(), new EndCriteria(1000, 100, 1e-10, 1e-10, 1e-10), [0.2, 0.2, 0.2]);
}
