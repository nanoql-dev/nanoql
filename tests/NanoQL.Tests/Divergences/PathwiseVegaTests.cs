// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The pathwise vega engines: the delta engine's recursion, the pseudo-root Jacobian's drift sum and displacement, and
/// the Jacobian's one-step discount factors, which QuantLib's engines take at the step's end
/// (<c>pathwiseaccountingengine.cpp:494</c>, <c>:910</c>) where its own test takes them at the start
/// (<c>marketmodel.cpp:2899-2900</c>). On caplets at the forwards, FlatVol 20%, 3 factors, the money-market measure,
/// the Mersenne twister (seed 42), 1,023 paths, one bump a step (factor 0 of every alive rate): six semi-annual rates
/// at displacements 0 and 2%, six unequal periods at 2%, and the semi-annual rates paid 0.2 before their periods' ends:
/// nanoql's vegas, from both engines, are the same-path differences of the price in the pseudo-roots; QuantLib's are
/// off. Checks: same-path central differences of the price, the pseudo-roots bumped along the bump; the two engines
/// against each other.
/// </summary>
public class PathwiseVegaTests
{
    private const int Paths = 1023;

    // QuantLib's vegas from its inner engine
    private static readonly double[] QuantLibVegas =
        [0.0053763768279585225, 0.0079641373482339829, 0.009588273437900913, 0.010371034010451278, 0.011112985154909499, 0.012938568215462127];

    private static readonly double[] QuantLibVegasDisplaced =
        [0.0089579671778841578, 0.013041889693683351, 0.015205503546996796, 0.016042043401666612, 0.016782247436713551, 0.019428271914923717];

    private static readonly double[] QuantLibVegasUnequal =
        [0.0090434062109990606, 0.006384961949581055, 0.023130564105354412, 0.007942613478292242, 0.020094786358211471, 0.015314946576804158];

    [Theory]
    [InlineData("semi-annual", 0.0)]
    [InlineData("semi-annual", 0.02)]
    [InlineData("unequal", 0.02)]
    [InlineData("paid inside", 0.0)]
    public void TheVegasAreThePricesSamePathDifferences(string grid, double displacement)
    {
        PathwiseGrid g = PathwiseGrid.Of(grid);
        int n = g.Forwards.Length;
        MarketModel model = g.FlatVol(g.Forwards, displacement);
        Matrix[][] bumps = [.. Enumerable.Range(0, n).Select(l => new[] { Bump(n, l) })];
        var caplets = new MarketModelPathwiseMultiCaplet(g.RateTimes, g.Accruals, g.PaymentTimes, g.Forwards);
        int[] numeraires = [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(g.Evolution)];

        var inner = new PathwiseVegasAccountingEngine(Evolver(model, numeraires), caplets, model, bumps, 0.95);
        var outer = new PathwiseVegasOuterAccountingEngine(Evolver(model, numeraires), caplets, model, bumps, 0.95);
        double[] innerMeans = new double[inner.NumberOfResults], innerErrors = new double[inner.NumberOfResults];
        double[] outerMeans = new double[outer.NumberOfResults], outerErrors = new double[outer.NumberOfResults];
        inner.MultiplePathValues(innerMeans, innerErrors, Paths);
        outer.MultiplePathValues(outerMeans, outerErrors, Paths);

        const double epsilon = 1e-7;
        ImmutableArray<double> up = Prices(g, model, epsilon, displacement, numeraires, caplets);
        ImmutableArray<double> down = Prices(g, model, -epsilon, displacement, numeraires, caplets);
        double[]? quantLib = grid switch
        {
            "semi-annual" => displacement == 0.0 ? QuantLibVegas : QuantLibVegasDisplaced,
            "unequal" => QuantLibVegasUnequal,
            _ => null,
        };
        for (int i = 0; i < n; ++i)
        {
            double difference = (up[i] - down[i]) / (2 * epsilon);
            double vega = innerMeans[(i * (n + 2)) + n + 1];
            Assert.True(Math.Abs(vega - difference) <= 1e-9, $"caplet {i}: {vega} against {difference}");
            Assert.True(
                Math.Abs(outerMeans[(i * (n + 2)) + n + 1] - vega) <= 1e-15,
                $"caplet {i}: outer {outerMeans[(i * (n + 2)) + n + 1]} against inner {vega}");

            if (quantLib is not null)
            {
                // QuantLib's, off by its recursion, Jacobian and discount factors: 1.0e-5 to 5.6e-5 relative at d 0, 1.4e-5
                // to 5.3% at d 2% on both grids, where nanoql's differ by 7e-11
                double off = Math.Abs(quantLib[i] - difference) / Math.Abs(difference);
                Assert.True(off > (displacement == 0.0 ? 5e-6 : 1e-5), $"QuantLib's caplet {i}: {quantLib[i]} against {difference}");
            }
        }
    }

    // One bump a step: factor 0 of every alive rate.
    private static Matrix Bump(int n, int step)
    {
        var b = new Matrix(n, 3, 0.0);
        for (int k = step; k < n; ++k)
        {
            b[k, 0] = 1.0;
        }

        return b;
    }

    // The price on the pseudo-roots bumped by epsilon along each step's bump, on the same paths.
    private static ImmutableArray<double> Prices(
        PathwiseGrid g, MarketModel model, double epsilon, double displacement, int[] numeraires, MarketModelPathwiseMultiProduct product)
    {
        int n = g.Forwards.Length;
        Matrix[] roots = [.. Enumerable.Range(0, model.NumberOfSteps).Select(l => model.PseudoRoot(l) + (epsilon * Bump(n, l)))];
        var bumped = new PseudoRootFacade(roots, g.RateTimes, g.Forwards, [.. Enumerable.Repeat(displacement, n)]);
        var engine = new PathwiseAccountingEngine(Evolver(bumped, numeraires), product, bumped, 0.95);
        var stats = new SequenceStatisticsInc(product.NumberOfProducts * (n + 1));
        engine.MultiplePathValues(stats, Paths);
        return stats.Mean();
    }

    private static LogNormalFwdRateEuler Evolver(MarketModel model, int[] numeraires) =>
        new(model, new MTBrownianGeneratorFactory(42), numeraires);
}
