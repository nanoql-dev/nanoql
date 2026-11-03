// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using static NanoQL.Tests.Divergences.HestonGeometricAsianMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Heston geometric Asian engine's Fourier cutoff (fixed). QuantLib integrates on (0, ξ_max], ξ_max 100, by
/// Gauss-Legendre at 128 points (<c>analytic_discr_geom_av_price_heston.cpp:48-55</c>, <c>:62</c>), cut before the
/// integrand decays at short maturities and small variance: on a short-dated market (r 5%, q 0, κ 2, ρ −0.5, six
/// fixings), ten days at v₀ = θ 0.0025 and vol of vol 0.1 price 0.11843188016798013 and thirty days
/// 0.37302951787635213, where the integrals converge to 0.24347 and 0.50345, and QuantLib's Monte Carlo geometric
/// Heston engine gives 0.24341 ± 0.0005 and 0.50309 ± 0.0009; the ten-day call at 200 at v₀ 0.04 and vol of vol 0.5
/// prices −0.06101169378323358. nanoql cuts where Phi's modulus has decayed to 1e-14 of its value on the contour's
/// axis, or at ξ_max where that is later. Checks: QuantLib's Monte Carlo engine within three standard errors, QUADPACK
/// on two contours at the tolerance on the engine's scale, and the integral run to four times its decay point.
/// </summary>
public class HestonGeometricAsianCutoffTests
{
    private static readonly int[] TenDays = [1, 3, 5, 6, 8, 10];

    [Fact]
    public void TheShortMarketReachesItsIntegral()
    {
        double price = Price(P9Today, Market(P9Today, 100.0, 0.0025, 2.0, 0.0025, 0.1, -0.5), OptionType.Call, 100.0, TenDays, 10);
        AssertR7(0.24347239604964227, price, OptionType.Call, 100.0, Math.Exp(-0.05 * 10 / 365.0), 100.07479043803887);
        Assert.True(Math.Abs(price - 0.24340768090085027) <= 3.0 * 0.0004968737566801213);
    }

    [Fact]
    public void TheSmallVarianceMarketReachesItsIntegral()
    {
        double price = Price(P9Today, Market(P9Today, 100.0, 0.0025, 2.0, 0.0025, 0.1, -0.5), OptionType.Call, 100.0, Days(6, 5), 30);
        AssertR7(0.5034511753738897, price, OptionType.Call, 100.0, Math.Exp(-0.05 * 30 / 365.0), 100.23834766937331);
        Assert.True(Math.Abs(price - 0.5030882715421305) <= 3.0 * 0.0009392976394836598);
    }

    [Fact]
    public void TheDeepCallIsItsIntegralToFourTimesTheDecayPoint()
    {
        // the integral to 4 × its decay point is −5.9e-132; QUADPACK on the route's contour 0 and on a second 1.1e-13
        double price = Price(P9Today, Market(P9Today, 100.0, 0.04, 2.0, 0.04, 0.5, -0.5), OptionType.Call, 200.0, TenDays, 10);
        Assert.True(price >= 0.0);
        Assert.True(price <= 1e-9, $"{price:R}");
    }
}
