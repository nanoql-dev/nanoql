// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using static NanoQL.Tests.Divergences.HestonGeometricAsianMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Heston geometric Asian engine's switch to the intrinsic value (the S-bar plan's BR5 (xxxiv), Revision 12, BR12,
/// fixed under R8). Without it the quadrature reaches its cap where the integrand oscillates over thousands of periods
/// before it decays (an hour at v₀ 0.0025 at the calls at 120 and 200), and at v₀ = θ = 0 the integrand overflows
/// (the planner's BP46). nanoql prices the point mass at v₀ = θ = 0, and the discounted intrinsic where a Chernoff
/// bound on the extrinsic value, from Phi at real s up to the moment explosion, is at or below 1e-12 × max(the
/// discounted intrinsic, D·F_G/100); a switch on the strike's distance in standard deviations of ln G would price the
/// one-year call at 50 at its intrinsic, 2.4e-3 off (BP47). Checks: the bound's value 0 at the hour (the S-bar run's
/// <c>c3_heston_div_refs.py</c>), the point mass's own value, and QUADPACK on two contours at R7 on BR12's scale.
/// P9's market: r 5%, q 0, κ 2, ρ −0.5, six fixings.
/// </summary>
public class HestonGeometricAsianIntrinsicTests
{
    [Fact]
    public void TheHoursDeepCallsAreTheirIntrinsic()
    {
        // an hour of six fixings at v₀ = θ 0.0025, vol of vol 0.1, restated on six days (everything over 144)
        const double c = 144.0;
        var process = Market(P9Today, 100.0, 0.0025 / c, 2.0 / c, 0.0025 / c, 0.1 / c, -0.5, 0.05 / c);
        Assert.Equal(0.0, Price(P9Today, process, OptionType.Call, 120.0, Days(6, 1), 6));
        Assert.Equal(0.0, Price(P9Today, process, OptionType.Call, 200.0, Days(6, 1), 6));
    }

    [Fact]
    public void ZeroVarianceIsThePointMass()
    {
        // a year of six fixings at v₀ = θ = 0, vol of vol 0.5: G is its forward S·exp(Σ r tᵢ/6)
        var process = Market(P9Today, 100.0, 0.0, 2.0, 0.0, 0.5, -0.5);
        int[] fixings = Days(6, 60);
        double forward = 100.0 * Math.Exp(fixings.Sum(d => 0.05 * d / 365.0) / 6.0);
        double discount = Math.Exp(-0.05 * 360 / 365.0);
        Assert.Equal(0.0, Price(P9Today, process, OptionType.Call, forward, fixings, 360), 1e-12);
        Assert.Equal(0.0, Price(P9Today, process, OptionType.Put, forward, fixings, 360), 1e-12);
        Assert.Equal(discount * 0.01 * forward, Price(P9Today, process, OptionType.Call, 0.99 * forward, fixings, 360), 1e-12);
        Assert.Equal(discount * 0.01 * forward, Price(P9Today, process, OptionType.Put, 1.01 * forward, fixings, 360), 1e-12);
    }

    [Fact]
    public void TheYearsDeepCallKeepsItsExtrinsicValue()
    {
        // a year at v₀ = θ 0.04, vol of vol 0.5: the call at 50, its extrinsic 2.3e-3
        const double forward = 102.60829539746186;
        double discount = Math.Exp(-0.05 * 360 / 365.0);
        double price = Price(P9Today, Market(P9Today, 100.0, 0.04, 2.0, 0.04, 0.5, -0.5), OptionType.Call, 50.0, Days(6, 60), 360);
        AssertR7(50.079117308895356, price, OptionType.Call, 50.0, discount, forward);
        AssertR7(50.07911730889537, price, OptionType.Call, 50.0, discount, forward);
        Assert.True(price - (discount * (forward - 50.0)) > 2e-3);
    }
}
