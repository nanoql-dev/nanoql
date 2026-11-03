// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Processes;
using static NanoQL.Tests.Divergences.HestonGeometricAsianMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Heston geometric Asian engine near the moment explosion (fixed). QuantLib integrates its undamped form on (0,
/// 100], so it neither finds where E[Gˢ] explodes nor needs to; the damped contour does, and a test of F's sign at each
/// interval's end admits contours past the explosion and prices the ten-year call at K = 1.5 F_G (vol of vol 1, ρ −0.9)
/// at 0, the call strip shrinks to nothing where s* nears 1 (no end under the cap), Phi(1, 0) is 0/0 for one fixing at
/// 30 years and vol of vol 2 (s* − 1 = 0 in double; ZeroDivisionError), and QuantLib's Phi is 0/0 at κ = 0 (sinh(0)/0,
/// NaN). nanoql takes the explosions by the closed-form first zero of the Riccati factors, runs the out-of-the-money
/// option on its own strip or Lewis's middle strip, takes F_G exact where every fixing time is one, the forms' limits
/// at γ = 0, and floors each price at its lower bound. Checks, at the tolerance on the engine's scale: QuantLib's
/// <c>AnalyticHestonEngine</c> at 1e-15 at one fixing; QUADPACK on two strips elsewhere; κ = 0 against κ 1e-12; and
/// spot 0.01 against spot 100 scaled, never below its floor. Markets: r 5%, q 0, v₀ = θ 0.04, κ 0.5 unless given.
/// </summary>
public class HestonGeometricAsianExplosionTests
{
    private const double F10 = 164.87212707001282, F30 = 448.1689070338065;

    [Fact]
    public void TheClosedFormTestKeepsTheCallItsExplosionAdmits()
    {
        // ten years, vol of vol 1, ρ −0.9, one fixing: s* − 1 = 9.3, the call strip; QuantLib's European
        HestonProcess process = Market(Today, 100.0, 0.04, 0.5, 0.04, 1.0, -0.9);
        double price = Price(Today, process, OptionType.Call, 247.30819060501923, [3650], 3650);
        AssertR7(0.11067681569005393, price, OptionType.Call, 247.30819060501923, D(3650), F10);
    }

    [Fact]
    public void TheTwoStripsAgreeAtThirtyYearsOfSixFixings()
    {
        // vol of vol 2, ρ 0, κ 2: s* 1.72; QUADPACK on the put strip and on the middle one
        HestonProcess process = Market(Today, 100.0, 0.04, 2.0, 0.04, 2.0, 0.0);
        double price = Price(Today, process, OptionType.Call, 100.0, Days(6, 1825), 10950);
        AssertR7(28.106098197012546, price, OptionType.Call, 100.0, D(10950), 218.6376499510635);
        AssertR7(28.10609819701254, price, OptionType.Call, 100.0, D(10950), 218.6376499510635);
    }

    [Fact]
    public void TheMiddleStripPricesTheMoneyWhereSStarNearsOne()
    {
        // ten years, vol of vol 2, ρ 0.9, one fixing: s* − 1 = 3.8e-6
        HestonProcess process = Market(Today, 100.0, 0.04, 0.5, 0.04, 2.0, 0.9);
        AssertR7(40.914187424202915, Price(Today, process, OptionType.Call, 100.0, [3650], 3650), OptionType.Call, 100.0, D(3650), F10);
    }

    [Fact]
    public void TheMiddleStripPricesTheWingWhereSStarNearsOne()
    {
        HestonProcess process = Market(Today, 100.0, 0.04, 0.5, 0.04, 2.0, 0.9);
        AssertR7(13.796831924034429, Price(Today, process, OptionType.Call, 200.0, [3650], 3650), OptionType.Call, 200.0, D(3650), F10);
    }

    [Fact]
    public void TheExactForwardPricesWhereSStarIsOne()
    {
        // thirty years, vol of vol 2, ρ 0.9, one fixing: s* − 1 = 0 in double, Phi(1, 0) 0/0
        HestonProcess process = Market(Today, 100.0, 0.04, 0.5, 0.04, 2.0, 0.9);
        AssertR7(78.5864344054471, Price(Today, process, OptionType.Call, 100.0, [10950], 10950), OptionType.Call, 100.0, D(10950), F30);
    }

    [Fact]
    public void BothNarrowStripsGiveWayToTheMiddleOne()
    {
        // thirty years, vol of vol 3, κ 0.01, ρ 0.9: s* − 1 = 0 and s₋ = −1.4e-3
        HestonProcess process = Market(Today, 100.0, 0.04, 0.01, 0.04, 3.0, 0.9);
        AssertR7(3.432841211446256, Price(Today, process, OptionType.Call, F30, [10950], 10950), OptionType.Call, F30, D(10950), F30);
    }

    [Fact]
    public void KappaZeroIsKappaToZero()
    {
        // ten years of annual fixings, vol of vol 1, ρ 0.9: QuantLib's Phi is 0/0 at κ = 0
        const double strike = 123.14412639131608;
        double atZero = Price(Today, Market(Today, 100.0, 0.04, 0.0, 0.04, 1.0, 0.9), OptionType.Call, strike, Days(10, 365), 3650);
        double nearZero = Price(Today, Market(Today, 100.0, 0.04, 1e-12, 0.04, 1.0, 0.9), OptionType.Call, strike, Days(10, 365), 3650);
        AssertR7(2.5905610070978553, atZero, OptionType.Call, strike, D(3650), strike);
        AssertR7(nearZero, atZero, OptionType.Call, strike, D(3650), strike);
    }

    [Fact]
    public void ASmallSpotIsSpotOneHundredScaledAndNeverBelowItsFloor()
    {
        // a month of six fixings at variance 1e-4 (κ 2, vol of vol 0.1, ρ −0.5), the call at F_G(1 + 1e-2)
        double small = Price(Today, Market(Today, 0.01, 1e-4, 2.0, 1e-4, 0.1, -0.5), OptionType.Call, 0.010124234643185397, Days(6, 5), 30);
        double hundred = Price(Today, Market(Today, 100.0, 1e-4, 2.0, 1e-4, 0.1, -0.5), OptionType.Call, 101.24234643185397, Days(6, 5), 30);
        Assert.True(small >= 0.0);
        AssertR7(6.25083186167453e-11, small, OptionType.Call, 0.010124234643185397, D(30), 0.010023994696223166);
        AssertR7(hundred * 1e-4, small, OptionType.Call, 0.010124234643185397, D(30), 0.010023994696223166);

        // a year of six fixings at variance 1e-4, the call at 1.2 × the spot's forward to the mean fixing time: the
        // contour comes out −1.5e-16, within the tolerance and below the floor
        double strike = 0.01 * Math.Exp(0.05 * 210.0 / 365.0) * 1.2;
        Assert.Equal(0.0, Price(Today, Market(Today, 0.01, 1e-4, 2.0, 1e-4, 0.1, -0.5), OptionType.Call, strike, Days(6, 60), 360));
    }

    private static double D(int days) => Math.Exp(-0.05 * days / 365.0);
}
