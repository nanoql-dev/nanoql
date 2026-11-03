// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Processes;
using static NanoQL.Tests.Divergences.HestonGeometricAsianMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Heston geometric Asian engine at small and long parameters (fixed). QuantLib's Phi adds κ²θT/σ² and subtracts
/// (2κθ/σ²)·Σ ln Fᵢ, and its ω̃ subtracts 2F̃/(σ²F) from κ/σ², terms of order 1/σ² that nearly cancel at small vol of
/// vol (<c>analytic_discr_geom_av_price_heston.cpp:165</c>, <c>:187</c>, <c>:197</c>): on a year of six fixings its
/// formula in double is 1.0e-9 off its own value in 30 digits at vol of vol 1e-3 and 1.5e-7 at 1e-4; its cosh and sinh
/// pass 709 before the cut at 30 years and vol of vol 2, where a cut short of the overflow leaves the price moving with
/// α by up to 12%; QuantLib's undamped integrand never ends on NanoQL's rule at small variance near the forward; and
/// the deterministic variance's (1 − e^(−κt))/κ is 1.1e-7 off at κ 1e-9. nanoql takes the stable form, ω̃ through tanh
/// and term3 − term4 through log1p, a cubic in σ below 1e-4 through the Black-Scholes limit (its variance by expm1) and
/// the stable form at 1e-4, 2e-4 and 3e-4, and the damped contour. Checks, at the tolerance on the engine's scale:
/// QuantLib's formula in 40 digits (with its limits), QUADPACK on the route's contour and on a second, and the price
/// the same across two strips. Markets: r 5%, q 0; a year is 360 days of six fixings every 60, v₀ = θ 0.04, κ 2, ρ
/// −0.5, unless given.
/// </summary>
public class HestonGeometricAsianSmallParameterTests
{
    private static readonly int[] Year = Days(6, 60);

    [Fact]
    public void AtVolOfVolAThousandthTheStableFormIsTheFormula()
    {
        HestonProcess process = Market(Today, 100.0, 0.04, 2.0, 0.04, 1e-3, -0.5);
        double price = Price(Today, process, OptionType.Call, 110.0, Year, 360);
        AssertR7(2.4143763335691440656, price, OptionType.Call, 110.0, D(360), 102.59008860489129);
    }

    [Fact]
    public void AtVolOfVolTenToTheMinusSevenTheBridgeIsTheFormula()
    {
        HestonProcess process = Market(Today, 100.0, 0.04, 2.0, 0.04, 1e-7, -0.5);
        double price = Price(Today, process, OptionType.Call, 100.0, Year, 360);
        AssertR7(6.2854738952622468828, price, OptionType.Call, 100.0, D(360), 102.59005516827024);
    }

    [Fact]
    public void ADayAtVarianceAMillionthIsPricedOnTheDampedContour()
    {
        // a day of six fixings (the short-dated market's model: κ 2, vol of vol 0.1, ρ −0.5) at v₀ = θ 1e-6, the call
        // at F_G(1 + 5e-5), restated on six days
        const double c = 6.0;
        HestonProcess process = Market(Today, 100.0, 1e-6 / c, 2.0 / c, 1e-6 / c, 0.1 / c, -0.5, 0.05 / c);
        double price = Price(Today, process, OptionType.Call, 100.01299156421916, Days(6, 1), 6);
        AssertR7(6.852928747749062e-05, price, OptionType.Call, 100.01299156421916, Math.Exp(-0.05 / 365.0), 100.00799116466092);
    }

    [Fact]
    public void ThirtyYearsAtVolOfVolTwoReachTheCutPastTheOverflow()
    {
        // six fixings every five years, vol of vol 2, ρ −0.9, κ 0.5: QuantLib's cosh and sinh overflow from ξ 169; the
        // call at 100 on the middle strip, at 300 on the call strip, each the same on a second contour
        HestonProcess process = Market(Today, 100.0, 0.04, 0.5, 0.04, 2.0, -0.9);
        const double forward = 232.16780427662573;
        double money = Price(Today, process, OptionType.Call, 100.0, Days(6, 1825), 10950);
        double wing = Price(Today, process, OptionType.Call, 300.0, Days(6, 1825), 10950);
        AssertR7(30.912774181537998, money, OptionType.Call, 100.0, D(10950), forward);
        AssertR7(30.912774181538023, money, OptionType.Call, 100.0, D(10950), forward);
        AssertR7(0.38487254049111147, wing, OptionType.Call, 300.0, D(10950), forward);
        AssertR7(0.38487254049103115, wing, OptionType.Call, 300.0, D(10950), forward);
    }

    [Fact]
    public void AtKappaTenToTheMinusNineTheBridgesVarianceIsExact()
    {
        // QuantLib's v₀ 0.09 and θ 0.0348, ρ −0.64, vol of vol 1e-5 on the cubic bridge
        HestonProcess process = Market(Today, 100.0, 0.09, 1e-9, 0.0348, 1e-5, -0.64);
        double price = Price(Today, process, OptionType.Call, 100.0, Year, 360);
        AssertR7(8.4961510189177705116, price, OptionType.Call, 100.0, D(360), 102.18099754050098);
    }

    private static double D(int days) => Math.Exp(-0.05 * days / 365.0);
}
