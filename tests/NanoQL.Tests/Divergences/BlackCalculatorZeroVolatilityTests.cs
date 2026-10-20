// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="BlackCalculator"/>'s Greeks at zero volatility. QuantLib's zero-volatility branches of
/// <c>delta</c>, <c>deltaForward</c> and <c>strikeSensitivity</c> tell a call from a put by <c>alpha_ &gt;= 0</c>
/// (<c>blackcalculator.cpp:214-228</c>, <c>:256-270</c>, <c>:438-452</c>), but alpha is 0 for an out-of-the-money put
/// and for every cash-or-nothing payoff, which then get a call's Greeks: a put struck at 90 on a forward of 100 reads a
/// forward delta of 1 and a delta of 1.0204 at a spot of 98, though its value is 0 at every nearby spot; a
/// cash-or-nothing call struck at 100 on a forward of 110 reads 0.95, though its value doesn't move. The theta, theta
/// per day, elasticity and forward elasticity read the deltas, and the rho and dividend rho the forward delta
/// (<c>:287-310</c>, <c>:360-369</c>, <c>:395-418</c>), so QuantLib's put reads an elasticity of 1.8e308, a rho of 100
/// and a dividend rho of −100. nanoql computes the three from alpha and beta as the nonzero branch does, its
/// derivative terms zero, so each of the nine reads its limit: the delta is QuantLib's own value's difference in the
/// spot, and every result of a plain vanilla call is QuantLib's, but at a standard deviation of exactly ε, where
/// QuantLib's constructor computes d2 (<c>:76</c>) and its Greeks take the zero-volatility branch: there an
/// at-the-money call's strike sensitivity reads N(−ε/2) where QuantLib's reads ½, 1 ulp apart.
/// </summary>
public class BlackCalculatorZeroVolatilityTests
{
    [Fact]
    public void AnOutOfTheMoneyPutsGreeksAreItsValuesLimits()
    {
        var put = new BlackCalculator(new PlainVanillaPayoff(OptionType.Put, 90.0), 100.0, 0.0, 1.0);

        // QuantLib's own check: its value at the spot moved either way is 0, so its delta is 0.
        Assert.Equal(0.0, put.Value);
        Assert.Equal(0.0, put.DeltaForward);
        Assert.Equal(0.0, put.Delta(98.0));
        Assert.Equal(0.0, put.StrikeSensitivity);
        Assert.Equal(0.0, put.Elasticity(98.0));
        Assert.Equal(0.0, put.ElasticityForward);
        Assert.Equal(0.0, put.Theta(98.0, 1.0));
        Assert.Equal(0.0, put.ThetaPerDay(98.0, 1.0));
        Assert.Equal(0.0, put.Rho(1.0));
        Assert.Equal(0.0, put.DividendRho(1.0));
        Assert.Equal(new BlackCalculator(OptionType.Put, 90.0, 100.0 * 98.01 / 98.0, 0.0).Value, put.Value);
    }

    [Fact]
    public void ACashOrNothingCallsGreeksAreItsValuesLimits()
    {
        var cash = new BlackCalculator(new CashOrNothingPayoff(OptionType.Call, 100.0, 10.0), 110.0, 0.0, 0.95);
        var up = new BlackCalculator(new CashOrNothingPayoff(OptionType.Call, 100.0, 10.0), 110.0 * 1.001, 0.0, 0.95);

        // QuantLib's own check: its value doesn't move with the spot or the strike.
        Assert.Equal(9.5, cash.Value);
        Assert.Equal(cash.Value, up.Value);
        Assert.Equal(0.0, cash.DeltaForward);
        Assert.Equal(0.0, cash.Delta(110.0));
        Assert.Equal(0.0, cash.StrikeSensitivity);
        Assert.Equal(0.0, cash.Elasticity(110.0));
        Assert.Equal(0.0, cash.ElasticityForward);

        // with no delta, the theta is the discounting's alone, and the rho the value's discounting
        Assert.Equal(-Math.Log(0.95) * 9.5, cash.Theta(110.0, 1.0), 14);
        Assert.Equal(-Math.Log(0.95) * 9.5 / 365.0, cash.ThetaPerDay(110.0, 1.0), 14);
        Assert.Equal(-9.5, cash.Rho(1.0));
        Assert.Equal(0.0, cash.DividendRho(1.0));
    }
}
