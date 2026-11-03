// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Longstaff-Schwartz basket engine's state scaled by the strike. QuantLib scales the assets' prices by 1/K
/// (<c>mcamericanbasketengine.cpp:48-50</c>, <c>:67-73</c>): at a zero strike, an exchange of the basket for nothing,
/// the scale is infinite and the price NaN, and at a negative strike the scale's sign swaps the minimum and the maximum
/// (an American maximum call 101.16 on the test's market where the European is 116.67). nanoql scales by 1/|K|, and by
/// 1 at a zero strike; a positive scale leaves the regression's polynomial span unchanged: QuantLib's N-dimensional
/// finite-difference engine at K ≤ 0, and QuantLib's bits at K &gt; 0, are the checks.
/// </summary>
public class AmericanBasketStrikeScalingTests
{
    private static readonly GeneralizedBlackScholesProcess P1 = Process(100.0, 0.02, 0.05, 0.25);
    private static readonly GeneralizedBlackScholesProcess P2 = Process(95.0, 0.03, 0.05, 0.3);

    [Fact]
    public void AZeroStrikeMeetsTheFiniteDifferenceEngine()
    {
        BasketOption option = MaxCall(0.0);
        double mc = Lsmc(option);
        double fd = Fdm(option);
        Assert.False(double.IsNaN(mc));
        Assert.Equal(fd, mc, 0.01 * fd);
    }

    [Fact]
    public void ANegativeStrikeKeepsTheMaximumAndMeetsTheFiniteDifferenceEngine()
    {
        BasketOption option = MaxCall(-10.0);
        double mc = Lsmc(option);
        double fd = Fdm(option);
        Assert.Equal(fd, mc, 0.01 * fd);
    }

    [Fact]
    public void APositiveStrikeKeepsQuantLibsBits()
    {
        // QuantLib's MCAmericanBasketEngine on the market through the wheel, 20 steps, antithetic, 2,000 samples, 500
        // calibration samples, seed 42, its calibration seed the wheel's narrowed null, 2147483647 (QuantLib-Python
        // 1.44)
        Assert.Equal(16.0913246926029, Lsmc(MaxCall(100.0), 2147483647));
        Assert.Equal(22.688465960395092, Lsmc(MaxCall(90.0), 2147483647));
    }

    private static BasketOption MaxCall(Real k) =>
        new(new MaxBasketPayoff(new PlainVanillaPayoff(OptionType.Call, k)), new AmericanExercise(Today, Today + 365));

    private static double Lsmc(BasketOption option, long? seedCalibration = null) =>
        MCAmericanBasketEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option,
            Context,
            new StochasticProcessArray([P1, P2], Rho(0.5)),
            20,
            null,
            false,
            true,
            2000,
            null,
            null,
            42,
            500,
            seedCalibration: seedCalibration).NPV!.Value;

    private static double Fdm(BasketOption option) =>
        FdndimBlackScholesVanillaEngine.Calculate(option, Context, Both(P1, P2), Rho(0.5), 50, 50).NPV!.Value;
}
