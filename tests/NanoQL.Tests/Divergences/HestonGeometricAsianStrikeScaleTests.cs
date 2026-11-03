// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Divergences.HestonGeometricAsianMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Heston geometric Asian engine across strikes and spots (fixed). On the damped contour, a strip chosen by width
/// alone sends a deep out-of-the-money option on the other side through parity from a large in-the-money integral (no
/// end under the cap: puts at K/F 0.01 and 0.001, calls at K/F 30 to 1,000); the tolerance at scale 1 asks a small
/// price at spot 1e4 for 5e-14 of the forward (no end at spot 1e4 and 1e6); the halfway α leaves the integrand on the
/// forward's scale at large spots; a single breakpoint set accepts a panel whose two rules alias the oscillation and
/// agree by chance (1.88 tolerances off at spot 1e4); and distinct dates on one 30/360 time put Phi(1, 0) at its
/// one-date 0/0. nanoql runs the out-of-the-money option on its own strip where that strip is at least 0.5 wide and on
/// the middle strip where it isn't, the other by parity; Lord and Kahl's α; the tolerance at max(the price, the lower
/// bound, D·F_G/100), every spot computing spot 100's contract scaled; two breakpoint sets that must agree, a third on
/// disagreement; F_G exact where every fixing time is one. Checks, at the tolerance on the engine's scale: QuantLib's
/// <c>AnalyticHestonEngine</c> at 1e-15 at one fixing, itself checked against QUADPACK on the route's contour at two α,
/// QUADPACK the reference where they part by more than half the tolerance (the K/F 1e5 call, 11.8 tolerances off
/// QuantLib); spot 100's price scaled; QUADPACK elsewhere. Markets: r 5%, q 0, v₀ = θ 0.04 unless given.
/// </summary>
public class HestonGeometricAsianStrikeScaleTests
{
    private const double F1 = 105.12710963760242, F10 = 164.87212707001282, F30 = 448.1689070338065;

    // a year, vol of vol 0.5, ρ −0.5, κ 2, one fixing (r12a's market)
    [Fact]
    public void AtSpotAMillionTheTwiceForwardCallIsQuantLibs() =>
        Check(1e6, 0.04, 2.0, 0.5, -0.5, 365, OptionType.Call, 2102542.1927520484, F1 * 1e4, 13.20026242376941);

    [Fact]
    public void AtSpotAMillionTheThriceForwardCallIsQuantLibs() =>
        Check(1e6, 0.04, 2.0, 0.5, -0.5, 365, OptionType.Call, 3153813.2891280726, F1 * 1e4, 0.051089429478586766);

    [Fact]
    public void AtSpotTenMillionTheThriceForwardCallIsSpotOneHundredsScaled()
    {
        double hundred = Price(Today, Market(Today, 100.0, 0.04, 2.0, 0.04, 0.5, -0.5), OptionType.Call, 315.38132891280725, [365], 365);
        double large = Price(Today, Market(Today, 1e7, 0.04, 2.0, 0.04, 0.5, -0.5), OptionType.Call, 31538132.891280726, [365], 365);
        AssertR7(hundred * 1e5, large, OptionType.Call, 31538132.891280726, D(365), F1 * 1e5);
        AssertR7(0.5108942947611742, large, OptionType.Call, 31538132.891280726, D(365), F1 * 1e5);
    }

    // ten years, vol of vol 1, ρ −0.9, κ 0.5: the deep put on its own strip
    [Fact]
    public void TheDeepPutIsQuantLibs() =>
        Check(100.0, 0.04, 0.5, 1.0, -0.9, 3650, OptionType.Put, 0.16487212707001284, F10, 0.00041646778375190486);

    // thirty years, vol of vol 2, ρ 0.9, κ 0.5: the call strip closed, the deep call on the middle one
    [Fact]
    public void TheThousandTimesForwardCallIsQuantLibs() =>
        Check(100.0, 0.04, 0.5, 2.0, 0.9, 10950, OptionType.Call, 448168.9070338065, F30, 31.890053593698905);

    // thirty years, vol of vol 3, ρ 0.9, κ 0.01: past 100,000 evaluations, under the cap of 1e6
    [Fact]
    public void TheThreeHundredTimesForwardCallIsQuantLibs() =>
        Check(100.0, 0.04, 0.01, 3.0, 0.9, 10950, OptionType.Call, 134450.67211014195, F30, 3.0292455238952365);

    [Fact]
    public void TheTailAtSpotTenThousandIsQuadpacks()
    {
        // a month of six fixings at variance 1e-6 (κ 2, vol of vol 0.1, ρ −0.5), the call at F_G(1 + 1e-2): Lord and
        // Kahl's α 685 on its own strip
        HestonProcess process = Market(Today, 1e4, 1e-6, 2.0, 1e-6, 0.1, -0.5);
        double price = Price(Today, process, OptionType.Call, 10124.241306294616, Days(6, 5), 30);
        AssertR7(4.1192796127926484e-08, price, OptionType.Call, 10124.241306294616, D(30), 10024.001293361007);
    }

    [Fact]
    public void TwoDatesOnOneThirtyThreeSixtyTimeTakeTheExactForward()
    {
        // 30E/360: the 30th and 31st of October are one time, so the average is the spot then
        DayCounter dc = new Thirty360(Thirty360Convention.European);
        var process = new HestonProcess(new FlatForward(Today, 0.05, dc), new FlatForward(Today, 0.0, dc), 100.0, 0.04, 0.5, 0.04, 1.0, -0.9);
        var context = new EvaluationContext { EvaluationDate = Today };
        Date first = new(30, Month.October, 2015), second = new(31, Month.October, 2015);
        double Priced(Date[] dates) => AnalyticDiscreteGeometricAveragePriceAsianHestonEngine.Calculate(
            new DiscreteAveragingAsianOption(AverageType.Geometric, 1.0, 0, dates, new PlainVanillaPayoff(OptionType.Call, 105.0), new EuropeanExercise(second)),
            context,
            process).NPV!.Value;
        double pair = Priced([first, second]);
        double one = Priced([second]);
        Assert.True(double.IsFinite(pair));
        double t = dc.YearFraction(Today, second);
        AssertR7(one, pair, OptionType.Call, 105.0, Math.Exp(-0.05 * t), 100.0 * Math.Exp(0.05 * t));
    }

    [Fact]
    public void TheMoneyIsPricedWhereTheCallStripIsEmpty() =>
        Check(100.0, 0.04, 0.5, 2.0, 0.9, 10950, OptionType.Call, F30, F30, 34.112469353594406);

    // ten years, vol of vol 1, ρ 0.9, κ 0.5, variance 1e-3: one breakpoint set accepts an aliased panel
    [Fact]
    public void TheAliasedCallAtSpotOneHundredIsQuantLibs() =>
        Check(100.0, 1e-3, 0.5, 1.0, 0.9, 3650, OptionType.Call, 247.30819060501923, F10, 0.482037197727226);

    [Fact]
    public void TheAliasedCallAtSpotTenThousandIsQuantLibs() =>
        Check(1e4, 1e-3, 0.5, 1.0, 0.9, 3650, OptionType.Call, 24730.819060501926, F10 * 100.0, 48.20371977272231);

    // the same at variance 1e-4: at the tolerance's scale 1 the contour reached its cap
    [Fact]
    public void TheSmallVarianceCallAtSpotTenThousandIsQuantLibs() =>
        Check(1e4, 1e-4, 0.5, 1.0, 0.9, 3650, OptionType.Call, 24730.819060501926, F10 * 100.0, 4.837422034235834);

    [Fact]
    public void TheSmallVarianceCallAtSpotAMillionIsQuantLibs() =>
        Check(1e6, 1e-4, 0.5, 1.0, 0.9, 3650, OptionType.Call, 2473081.906050192, F10 * 1e4, 483.7422034235543);

    // thirty years, vol of vol 3, ρ 0, κ 0.01, variance 1e-3: the put at K/F 0.1, 757,095 evaluations
    [Fact]
    public void TheSmallVariancePutAtSpotTenThousandIsQuantLibs() =>
        Check(1e4, 1e-3, 0.01, 3.0, 0.0, 10950, OptionType.Put, 4481.689070338065, F30 * 100.0, 0.14556257128264669);

    [Fact]
    public void TheSmallVariancePutAtSpotAMillionIsQuantLibs() =>
        Check(1e6, 1e-3, 0.01, 3.0, 0.0, 10950, OptionType.Put, 448168.9070338065, F30 * 1e4, 14.556257128297139);

    [Fact]
    public void TheHundredThousandTimesForwardCallIsQuadpacks()
    {
        // thirty years, vol of vol 3, ρ 0, κ 0.01: QuantLib's engine 11.8 times the tolerance off the route's two
        // contours, which agree within 0.1 times it
        HestonProcess process = Market(Today, 100.0, 0.04, 0.01, 0.04, 3.0, 0.0);
        double price = Price(Today, process, OptionType.Call, 44816890.703380644, [10950], 10950);
        AssertR7(0.27072958889490906, price, OptionType.Call, 44816890.703380644, D(10950), F30);
        AssertR7(0.2707295888849046, price, OptionType.Call, 44816890.703380644, D(10950), F30);
    }

    private static void Check(
        double spot, double variance, double kappa, double sigma, double rho, int days, OptionType type, double strike, double forward, double expected)
    {
        double price = Price(Today, Market(Today, spot, variance, kappa, variance, sigma, rho), type, strike, [days], days);
        AssertR7(expected, price, type, strike, D(days), forward);
    }

    private static double D(int days) => Math.Exp(-0.05 * days / 365.0);
}
