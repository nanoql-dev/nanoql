// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The fix QuantLib's master made to the exact Bachelier implied volatility after the pin, which nanoql takes as
/// written (R8, the upstream sweep): #2924 (<c>e61abd2f5</c>) returns 0 only where the time value isn't positive and
/// the price is close to its intrinsic value. QuantLib 1.44 returns 0 wherever the price is close to the intrinsic
/// value (<c>blackformula.cpp:919-920</c>), so a deep in-the-money price just above it, or a tiny real time value out
/// of the money, inverts to 0 in place of its volatility. Of the vectors' 600 Bachelier implied volatilities, 23 leave
/// 1.44's 0 (the plan's SWP9, F1-4), which the generator leaves out of the file. In 14 the time value is real and tiny,
/// and the fix recovers the generating volatility; in 9 a deep in-the-money price sits one ulp above its intrinsic
/// value after the division by a discount of 0.95, and the fix inverts that rounding into a volatility of about 0.09,
/// where the generating one is far smaller and 1.44 returns 0. At a discount of 1 those prices are exact, and the fix
/// returns 0 too. One of each kind is below, from the vectors' inputs. Two of the vectors' argument cases move too: an
/// infinite price after the division by the discount (a discount of 0, or an infinite price), which 1.44 finds close to
/// the intrinsic value and returns 0 for, and which the fix's positive time value inverts to NaN, as QuantLib's
/// master's arithmetic does. And a smile section's normal volatility, whose conversion inverts the section's price at a
/// discount of 1 (<c>smilesection.cpp:131-155</c>): 16 of the flat, SABR and ZABR sections' vectors leave 1.44's 0 (the
/// sweep's SWI3), one of them below.
/// </summary>
public class BlackFormulaUpstreamFixesTests
{
    [Fact]
    public void TestBachelierImpliedVolJustAboveIntrinsic()
    {
        // Upstream: QuantLib's master's case for #2924 (blackformula.cpp's testBachelierImpliedVolJustAboveIntrinsic)
        const double tte = 1.0;
        const double vol = 1.0;
        double stdDev = vol * Math.Sqrt(tte);

        int inverted = 0;
        for (double d = 7.0; d <= 7.6 + 1e-12; d += 0.05)
        {
            foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
            {
                const double forward = 0.0;
                double strike = type == OptionType.Call ? forward - d : forward + d;
                double intrinsic = d;
                double price = BlackFormulaFunctions.BachelierBlackFormula(type, strike, forward, stdDev);

                if (price > intrinsic)
                {
                    double impliedVol = BlackFormulaFunctions.BachelierBlackFormulaImpliedVol(type, strike, forward, tte, price);
                    Assert.True(
                        Math.Abs((impliedVol / vol) - 1.0) <= 0.02,
                        $"implied vol {impliedVol} for d = {d} differs from {vol} by more than 2%");
                    ++inverted;
                }

                Assert.Equal(0.0, BlackFormulaFunctions.BachelierBlackFormulaImpliedVol(type, strike, forward, tte, intrinsic));

                double belowIntrinsic = intrinsic * (1.0 - (4.0 * Epsilon));
                Assert.Equal(0.0, BlackFormulaFunctions.BachelierBlackFormulaImpliedVol(type, strike, forward, tte, belowIntrinsic));
            }
        }

        Assert.True(inverted > 0, "no deep in-the-money price above intrinsic was tested");
    }

    [Fact]
    public void TestBachelierImpliedVolJustAboveIntrinsicDiscounted()
    {
        // Upstream: QuantLib's master's case for #2924 (blackformula.cpp's
        // testBachelierImpliedVolJustAboveIntrinsicDiscounted)
        const double tte = 1.0;
        const double vol = 1.0;
        double stdDev = vol * Math.Sqrt(tte);

        foreach (double discount in new[] { 0.9, 0.95, 0.99 })
        {
            for (double d = 7.0; d <= 7.6 + 1e-12; d += 0.05)
            {
                foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
                {
                    const double forward = 0.0;
                    double strike = type == OptionType.Call ? forward - d : forward + d;
                    double price = BlackFormulaFunctions.BachelierBlackFormula(type, strike, forward, stdDev, discount);

                    double impliedVol = BlackFormulaFunctions.BachelierBlackFormulaImpliedVol(type, strike, forward, tte, price, discount);
                    Assert.True(
                        impliedVol == 0.0 || Math.Abs((impliedVol / vol) - 1.0) <= 0.03,
                        $"implied vol {impliedVol} for d = {d} and discount = {discount} differs from {vol} by more than 3%");
                }
            }
        }
    }

    [Fact]
    public void ATinyRealTimeValueInvertsToItsVolatility()
    {
        // the vectors' call on a forward of 0.002, strike 0.075, two years at a volatility of 0.0035355 (a standard
        // deviation of 0.005), discount 1: its price 4.76e-52 is a real time value above an intrinsic value of 0, where
        // 1.44 returns 0
        const double price = 4.764898321733149e-52;
        double impliedVol = BlackFormulaFunctions.BachelierBlackFormulaImpliedVol(OptionType.Call, 0.075, 0.002, 2.0, price);
        Assert.Equal(0.005 / Math.Sqrt(2.0), impliedVol, 1e-6);
    }

    [Fact]
    public void AnInTheMoneyPriceOneUlpAboveIntrinsicInvertsItsRounding()
    {
        // the vectors' call on a forward of 1, strike 0.01, two years at a volatility of 7.07e-5, discount 0.95: the
        // price 0.9405 divided by the discount is 0.9900000000000001, one ulp above the intrinsic value 0.99, and the
        // fix inverts that rounding, where 1.44 returns 0
        const double price = 0.9405;
        Assert.Equal(0.9900000000000001, price / 0.95);
        double impliedVol = BlackFormulaFunctions.BachelierBlackFormulaImpliedVol(OptionType.Call, 0.01, 1.0, 2.0, price, 0.95);
        Assert.InRange(impliedVol, 0.087, 0.091);

        // at a discount of 1 the price is exact, and the fix returns 0 as 1.44 does
        Assert.Equal(0.0, BlackFormulaFunctions.BachelierBlackFormulaImpliedVol(OptionType.Call, 0.01, 1.0, 2.0, 0.99));
    }

    [Theory]
    [InlineData(0.021, 0.0)]
    [InlineData(double.PositiveInfinity, 0.9)]
    public void AnInfinitePriceIsNoLongerZero(double price, double discount)
    {
        // the vectors' arguments: a call struck at 0.03 on a forward of 0.05, a year; 1.44 returns 0
        Assert.True(double.IsNaN(BlackFormulaFunctions.BachelierBlackFormulaImpliedVol(OptionType.Call, 0.03, 0.05, 1.0, price, discount)));
    }

    [Fact]
    public void ASmileSectionsTinyPremiumConvertsToItsNormalVolatility()
    {
        // the flat section vectors' 33% lognormal section at an ATM level of 4.5%, a tenth of a year: its put at
        // 0.01125 prices 2.489e-44, a real time value, and its normal volatility, 0 in 1.44, now inverts that price
        var section = new FlatSmileSection(0.1, 0.33, new Actual360(), 0.045);
        double premium = section.OptionPrice(0.01125, OptionType.Put);
        Assert.Equal(2.489153415902617e-44, premium);
        double normal = section.Volatility(0.01125, VolatilityType.Normal, 0.0);
        Assert.Equal(BlackFormulaFunctions.BachelierBlackFormulaImpliedVol(OptionType.Put, 0.01125, 0.045, 0.1, premium), normal);
        Assert.True(normal > 0.0);
        double repriced = BlackFormulaFunctions.BachelierBlackFormula(OptionType.Put, 0.01125, 0.045, normal * Math.Sqrt(0.1));
        Assert.Equal(premium, repriced, premium * 1e-6);
    }

    // QL_EPSILON
    private const double Epsilon = 2.220446049250313e-16;
}
