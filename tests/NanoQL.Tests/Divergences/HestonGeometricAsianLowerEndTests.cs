// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using static NanoQL.Tests.Divergences.HestonGeometricAsianMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Heston geometric Asian engine's integral near ξ = 0 (fixed). QuantLib maps ξ ∈ [−1, 1] to (0.5 + 1e-8 +
/// 0.5ξ)·ξ_max (<c>analytic_discr_geom_av_price_heston.cpp:49</c>), so it leaves out [0, 1e-8·ξ_max], where the real
/// integrand is finite and flat, and every price carries −D × the prefactor × 1e-8·ξ_max·f(0⁺)/π: on a one-year market
/// (r 5%, q 0, v₀ = θ 0.04, κ 2, vol of vol 0.5, ρ −0.5, six fixings every 60 days) the call at 200 prices
/// −1.8234540127607532e-05 and the call at 100 6.189850115518784, 2.3e-7 low. nanoql's damped contour is regular at 0
/// and runs from it. Checks, at the tolerance on the engine's scale: QuantLib's undamped integrand by QUADPACK from
/// 1e-12·ξ_max with the left-out stretch as its rectangle, the deep call not negative; and the starts at 1e-9·ξ_max and
/// 1e-12·ξ_max agreeing on the money.
/// </summary>
public class HestonGeometricAsianLowerEndTests
{
    private const double Forward = 102.60829539746186;
    private static readonly double Discount = Math.Exp(-0.05 * 360 / 365.0);

    [Fact]
    public void TheDeepCallIsTheIntegralFromNearZero()
    {
        double price = Price(P9Today, Market(P9Today, 100.0, 0.04, 2.0, 0.04, 0.5, -0.5), OptionType.Call, 200.0, Days(6, 60), 360);
        Assert.True(price >= 0.0);
        AssertR7(1.6947296338739039e-06, price, OptionType.Call, 200.0, Discount, Forward);
    }

    [Fact]
    public void TheMoneyIsTheIntegralFromEitherStart()
    {
        double price = Price(P9Today, Market(P9Today, 100.0, 0.04, 2.0, 0.04, 0.5, -0.5), OptionType.Call, 100.0, Days(6, 60), 360);
        AssertR7(6.189850344237091, price, OptionType.Call, 100.0, Discount, Forward);
        AssertR7(6.18985034423709, price, OptionType.Call, 100.0, Discount, Forward);
    }
}
