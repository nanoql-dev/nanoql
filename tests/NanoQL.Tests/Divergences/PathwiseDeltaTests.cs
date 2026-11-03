// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The pathwise delta engine's recursion and layout. QuantLib's Euler-step Jacobian reads the drift's derivative at the
/// step's end, where the Euler drift is taken at its start (<c>pathwiseaccountingengine.cpp:167</c>), and drops the
/// displacement from the rates' ratios and the drift term (<c>:169-170</c>, <c>:285</c>); its results put product i's
/// deltas at (i + 1)·P + j, which overwrites another product's when P ≠ R (<c>:306</c>). On caplets at the forwards,
/// FlatVol 20%, 3 factors, the money-market measure, the Mersenne twister (seed 42), 1,023 paths: six semi-annual rates
/// at displacements 0 and 2%, six unequal periods at 2%, and the semi-annual rates paid 0.2 before their periods' ends,
/// which takes the discounter's bracket through the engine: nanoql's deltas are the same-path differences of the
/// engine's price, whose bits are QuantLib's where the payments are on rate times; QuantLib's are off by its recursion.
/// Checks: same-path central differences of the price; each product alone.
/// </summary>
public class PathwiseDeltaTests
{
    private const int Paths = 1023;

    // QuantLib's deltas of caplet 3 to rates 0 to 3
    private static readonly double[] QuantLibCaplet3 = [-0.00077168531756829787, -0.00077276517792356045, -0.00074448587202238857, 0.25462252006226355];
    private static readonly double[] QuantLibCaplet3Displaced = [-0.0013499844272610462, -0.0014737376614757625, -0.0015347909139734781, 0.2822468172566247];
    private static readonly double[] QuantLibCaplet3Unequal = [-0.00061715914390902005, -0.00034298880723417042, -0.0010305442656196337, 0.13636036580869937];

    [Theory]
    [InlineData("semi-annual", 0.0)]
    [InlineData("semi-annual", 0.02)]
    [InlineData("unequal", 0.02)]
    [InlineData("paid inside", 0.0)]
    public void TheDeltasAreThePricesSamePathDifferences(string grid, double displacement)
    {
        PathwiseGrid g = PathwiseGrid.Of(grid);
        var caplets = new MarketModelPathwiseMultiCaplet(g.RateTimes, g.Accruals, g.PaymentTimes, g.Forwards);
        int n = g.Forwards.Length;
        ImmutableArray<double> values = Run(g.FlatVol(g.Forwards, displacement), caplets);
        double[]? quantLib = grid switch
        {
            "semi-annual" => displacement == 0.0 ? QuantLibCaplet3 : QuantLibCaplet3Displaced,
            "unequal" => QuantLibCaplet3Unequal,
            _ => null,
        };

        const double h = 1e-7;
        for (int j = 0; j < n; ++j)
        {
            double[] up = [.. g.Forwards], down = [.. g.Forwards];
            up[j] += h;
            down[j] -= h;
            ImmutableArray<double> priceUp = Run(g.FlatVol(up, displacement), caplets), priceDown = Run(g.FlatVol(down, displacement), caplets);
            for (int i = 0; i < n; ++i)
            {
                double difference = (priceUp[i] - priceDown[i]) / (2 * h);
                double delta = values[n + (i * n) + j];
                Assert.True(Math.Abs(delta - difference) <= 1e-9, $"caplet {i}, rate {j}: {delta} against {difference}");
                if (quantLib is not null && i == 3 && j <= 3)
                {
                    // QuantLib's, off by its recursion: 1.7e-4 to 6.8e-4 relative on the off-diagonals at d 0, 4.6% to
                    // 28% everywhere at d 2% on both grids, where nanoql's differ by 7e-11
                    double off = Math.Abs(quantLib[j] - difference) / Math.Abs(difference);
                    Assert.True(
                        displacement == 0.0 ? j == 3 || off > 1e-4 : off > 0.04,
                        $"QuantLib's caplet {i}, rate {j}: {quantLib[j]} against {difference}");
                }
            }
        }
    }

    [Fact]
    public void TwoCapsOnNineRatesEachHoldTheirOwnDeltas()
    {
        var market = new MarketModelProductMarket(9);
        var evolution = new EvolutionDescription(market.RateTimes, market.RateTimes[..^1]);
        MarketModel model = market.FlatVol(evolution, 3);
        (int, int)[] both = [(0, 4), (4, 9)];
        var caps = new MarketModelPathwiseMultiDeflatedCap(market.RateTimes, market.Accruals, market.PaymentTimes, 0.032, both);
        ImmutableArray<double> together = Run(model, caps);

        for (int i = 0; i < 2; ++i)
        {
            var alone = new MarketModelPathwiseMultiDeflatedCap(market.RateTimes, market.Accruals, market.PaymentTimes, 0.032, both.AsSpan(i, 1));
            ImmutableArray<double> own = Run(model, alone);
            Assert.Equal(own[0], together[i]);
            for (int j = 0; j < 9; ++j)
            {
                // product i's deltas at P + i·R + j
                Assert.Equal(own[1 + j], together[2 + (i * 9) + j]);
            }
        }

        // QuantLib's (i + 1)·P + j: cap 0's deltas 2 and 3 read cap 1's deltas 0 and 1, which overwrote them
        Assert.True(together[2 + 2] > 0.25 && together[2 + 3] > 0.25);
        const double quantLibCap0Delta2 = -0.0074611333048811412, quantLibCap0Delta3 = -0.00747359794362448;
        Assert.True(quantLibCap0Delta2 < 0.0 && quantLibCap0Delta3 < 0.0);
    }

    private static ImmutableArray<double> Run(MarketModel model, MarketModelPathwiseMultiProduct product)
    {
        int[] numeraires = [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(model.Evolution)];
        var engine = new PathwiseAccountingEngine(
            new LogNormalFwdRateEuler(model, new MTBrownianGeneratorFactory(42), numeraires), product, model, 0.95);
        var stats = new SequenceStatisticsInc(product.NumberOfProducts * (model.NumberOfRates + 1));
        engine.MultiplePathValues(stats, Paths);
        return stats.Mean();
    }
}
