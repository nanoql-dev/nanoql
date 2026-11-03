// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using static NanoQL.Tests.Divergences.Fd2dMismatchedTimesTests;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Choi's and the n-dimensional finite-difference basket engines where every volatility is 0. QuantLib's fails on paths
/// it doesn't intend: Choi's single-factor nodes take their intrinsic branch, which sets no root
/// (<c>singlefactorbsmbasketengine.cpp:186</c>), and its read of the node's "d" throws "invalid map&lt;K, T&gt; key"
/// (<c>choibasketengine.cpp:217</c>); the n-dimensional engine meshes every principal direction at √0
/// (<c>fdndimblackscholesvanillaengine.cpp:155-165</c>) and fails with "vector too long". At volatilities of 1e-8 both
/// give the deterministic value, the discounted payoff at the forward basket. Here Choi's nodes take the root's limit,
/// ±∞ by the side of the strike the forward basket is on, and the n-dimensional mesh is floored at QL_EPSILON²: the
/// deterministic value, the single-factor engine's intrinsic and QuantLib's engines at 1e-8 are the checks.
/// </summary>
public class BasketZeroVolatilitiesTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new(15, Time.Month.October, 2026) };

    // the forward basket's average put, discounted: 100 - (100e^0.03 + 95e^0.02)/2, at e^-0.05
    private static readonly double Deterministic =
        Math.Max(100.0 - (0.5 * ((100.0 * Math.Exp(0.03)) + (95.0 * Math.Exp(0.02)))), 0.0) * Math.Exp(-0.05);

    [Fact]
    public void ChoiPricesTheDeterministicValue()
    {
        var option = new BasketOption(AveragePut(), European());
        MultiAssetOptionResults zero = ChoiBasketEngine.Calculate(option, Context, Zero(), Rho(), 10.0, null, true);
        Assert.Equal(Deterministic, zero.NPV!.Value, 1e-12 * Deterministic);

        // the forward deltas' limit: the put in the money, -w_k discounted
        Assert.Equal(-0.5 * Math.Exp(-0.05), (double)zero.AdditionalResults["forwardDelta 0"], 1e-12);
        Assert.Equal(-0.5 * Math.Exp(-0.05), (double)zero.AdditionalResults["forwardDelta 1"], 1e-12);

        // the single-factor engine's intrinsic, and QuantLib's Choi at volatilities of 1e-8
        double singleFactor = SingleFactorBsmBasketEngine.Calculate(option, Context, Zero()).NPV!.Value;
        Assert.Equal(singleFactor, zero.NPV!.Value, 1e-12 * Deterministic);
        double tiny = ChoiBasketEngine.Calculate(option, Context, Tiny(), Rho()).NPV!.Value;
        Assert.Equal(tiny, zero.NPV!.Value, 1e-9 * Deterministic);
    }

    [Fact]
    public void TheNdimEnginePricesTheDeterministicValue()
    {
        var european = new BasketOption(AveragePut(), European());
        double zero = FdndimBlackScholesVanillaEngine.Calculate(european, Context, Zero(), Rho(), 31, 25).NPV!.Value;
        Assert.Equal(Deterministic, zero, 1e-9 * Deterministic);
        double tiny = FdndimBlackScholesVanillaEngine.Calculate(european, Context, Tiny(), Rho(), 31, 25).NPV!.Value;
        Assert.Equal(tiny, zero, 1e-9 * Deterministic);

        // the American's intrinsic, 100 - 97.5, as QuantLib's at 1e-8 (2.4999999999999716)
        var american = new BasketOption(AveragePut(), American());
        Assert.Equal(
            2.5, FdndimBlackScholesVanillaEngine.Calculate(american, Context, Zero(), Rho(), 31, 25).NPV!.Value, 1e-12);

        // one asset alone
        var one = new BasketOption(new AverageBasketPayoff(new PlainVanillaPayoff(OptionType.Put, 100.0), [1.0]), European());
        double alone = FdndimBlackScholesVanillaEngine.Calculate(one, Context, [Zero()[0]], new Matrix(1, 1, 1.0), 31, 25)
            .NPV!.Value;
        Assert.Equal(0.0, alone, 1e-12);
    }

    private static ImmutableArray<GeneralizedBlackScholesProcess> Zero() =>
        [Process(100.0, Flat(0.02), Flat(0.05), Vol(0.0)), Process(95.0, Flat(0.03), Flat(0.05), Vol(0.0))];

    private static ImmutableArray<GeneralizedBlackScholesProcess> Tiny() =>
        [Process(100.0, Flat(0.02), Flat(0.05), Vol(1e-8)), Process(95.0, Flat(0.03), Flat(0.05), Vol(1e-8))];

    private static Matrix Rho() => new(new[,] { { 1.0, 0.5 }, { 0.5, 1.0 } });
}
