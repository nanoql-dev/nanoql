// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The discounter's bracket (fixed). QuantLib's <c>MarketModelDiscounter</c> takes the first rate time at or after the
/// payment as its period's start (<c>discounter.cpp:31</c>, <c>lower_bound</c>), so a payment strictly inside a period
/// but the last is extrapolated from the next period, against the class's own names (<c>before_</c>,
/// <c>beforeWeight_</c>) and its weight's [0, 1] form: on forwards rising 0.5% a period, in the last bond, a payment at
/// 1.25 is worth 1.10935174 bonds and one at 0.75 1.12738126, each 1.2e-3 from its own period's log-linear
/// interpolation, the next period's line carried back. nanoql takes the last rate time at or before the payment,
/// clamped to the periods as QuantLib clamps. Checks: the bracketing bonds' log-linear interpolation, its weight in [0,
/// 1]; and QuantLib's bits on a rate time, before the first and after the last, and in the last period.
/// </summary>
public class MarketModelDiscounterBracketTests
{
    private static readonly MarketModelProductMarket Market = new(6, 0.5, 0.5, 0.03, 0.005);

    [Theory]
    [InlineData(1.25, 1, 1.1093517435016012)]
    [InlineData(0.75, 0, 1.1273812594376544)]
    public void APaymentInsideAPeriodInterpolatesItsOwnPeriod(double paymentTime, int before, double quantLib)
    {
        LMMCurveState state = State();
        double nanoql = new MarketModelDiscounter(paymentTime, Market.RateTimes).NumeraireBonds(state, Market.N);

        double weight = 1.0 - ((paymentTime - Market.RateTimes[before]) / (Market.RateTimes[before + 1] - Market.RateTimes[before]));
        Assert.InRange(weight, 0.0, 1.0);
        double pre = state.DiscountRatio(before, Market.N), post = state.DiscountRatio(before + 1, Market.N);
        Assert.Equal(Math.Pow(pre, weight) * Math.Pow(post, 1.0 - weight), nanoql);
        Assert.InRange(nanoql, post, pre);

        // QuantLib's, extrapolated from the next period
        Assert.InRange(Math.Abs((quantLib / nanoql) - 1.0), 1.0e-3, 1.5e-3);
    }

    [Theory]
    [InlineData(3.25, 1.0136567466356647)]
    [InlineData(0.25, 1.1428853496741824)]
    [InlineData(4.0, 0.97323600973236002)]
    [InlineData(1.5, 1.098421903125)]
    [InlineData(3.0, 1.0275000000000001)]
    [InlineData(3.5, 1.0)]
    public void APaymentOnARateTimeOrOutsideThePeriodsIsQuantLibs(double paymentTime, double quantLib) =>
        Assert.Equal(quantLib, new MarketModelDiscounter(paymentTime, Market.RateTimes).NumeraireBonds(State(), Market.N));

    [Fact]
    public void ThePathwiseDiscounterInterpolatesItsOwnPeriodToo()
    {
        // MarketModelPathwiseDiscounter's bracket, pathwisediscounter.cpp:32: a payment at 0.75 on six semi-annual
        // rates from 0.5, forwards 3% rising 0.1% a period, the step's discount factors from the forwards; QuantLib's
        // factors extrapolate the next period's
        var market = new MarketModelProductMarket(6);
        var discounts = new Matrix(2, 7, 1.0);
        for (int j = 1; j <= 6; ++j)
        {
            discounts[1, j] = discounts[1, j - 1] / (1.0 + (0.5 * market.Forwards[j - 1]));
        }

        double[] factors = new double[7];
        new MarketModelPathwiseDiscounter(0.75, market.RateTimes).GetFactors(new Matrix(2, 6, 0.0), discounts, 1, factors);

        // its own period's log-linear interpolation, half way, and its derivative in the period's rate
        double df = discounts[1, 0] * Math.Pow(discounts[1, 1] / discounts[1, 0], 0.5);
        Assert.Equal(df, factors[0]);
        Assert.Equal(-df * 0.5 * discounts[1, 1] / discounts[1, 0] * 0.5, factors[1]);
        Assert.Equal([0.0, 0.0, 0.0, 0.0, 0.0], factors[2..]);

        double[] quantLib = [0.99282778252382275, -0.48907772538119354, 0.24441845950857277, 0, 0, 0, 0];
        // QuantLib's: 2.4e-4 from the interpolation, and a derivative in the next period's rate
        Assert.True(Math.Abs(quantLib[0] - df) > 2e-4 && quantLib[2] != 0.0, $"{quantLib[0]} against {df}");
    }

    private static LMMCurveState State()
    {
        var state = new LMMCurveState(Market.RateTimes);
        state.SetOnForwardRates(Market.Forwards);
        return state;
    }
}
