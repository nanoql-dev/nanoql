// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using static NanoQL.Tests.Divergences.HestonGeometricAsianMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Heston geometric Asian engine at a vanishing square root (fixed). QuantLib's <c>F</c> tests |κ² − 2σ²| &lt; 1e-8
/// for the root √(κ² − 2z₁σ²), dropping z₁, and returns 1 + ½(κ − z₂σ²) without τ
/// (<c>analytic_discr_geom_av_price_heston.cpp:77-87</c>): on r 5%, q 0, v₀ = θ 0.04, κ 1, ρ −0.5, twelve fixings every
/// 30 days, the call at 100 prices −7.9379393764773996 at vol of vol 0.70710678 and −7.937939339862728 at √½, where
/// 0.7071068 gives 5.567081837356833. nanoql takes the limit 1 + ½(κ − z₂σ²)τ where |κ² − 2z₁σ²| is below 1e-8, and the
/// forms' limits at γ = 0. Check: QUADPACK on the route's contour either side and at the point, at the tolerance on the
/// engine's scale; QuantLib's own cases, at vol of vol 0.39 to 1, are unchanged (the test suite's).
/// </summary>
public class HestonGeometricAsianSingularityTests
{
    [Fact]
    public void TheVanishingRootIsContinuous()
    {
        int[] fixings = Days(12, 30);
        double discount = Math.Exp(-0.05 * 360 / 365.0);
        foreach ((double sigma, double expected) in new[]
        {
            (0.70710678, 5.567291941126994), (0.7071067811865476, 5.567291939981185), (0.7071068, 5.567291921815026),
        })
        {
            double price = Price(P9Today, Market(P9Today, 100.0, 0.04, 1.0, 0.04, sigma, -0.5), OptionType.Call, 100.0, fixings, 360);
            AssertR7(expected, price, OptionType.Call, 100.0, discount, 102.40466348140865);
        }
    }
}
