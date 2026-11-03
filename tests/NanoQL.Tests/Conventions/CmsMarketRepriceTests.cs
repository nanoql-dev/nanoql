// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="CmsMarket.Reprice"/>: QuantLib's <c>reprice</c> sets the market's pricers' volatility and reversion in
/// place, so every leg holding those pricers reprices with it; nanoql's returns a new market priced on copies of the
/// pricers, and the receiver, its pricers and its values stand.
/// </summary>
public class CmsMarketRepriceTests
{
    [Fact]
    public void RepriceReturnsANewMarket()
    {
        (CmsMarket market, _, SwaptionVolatilityCube cube, EvaluationContext context) = Market();
        double[] before = CubeInputs.Flat(market.Browse());
        CmsMarket repriced = market.Reprice(cube, 0.01, context);
        Assert.NotSame(market, repriced);
        Assert.Equal(before, CubeInputs.Flat(market.Browse()));
        Assert.NotEqual(before, CubeInputs.Flat(repriced.Browse()));
    }

    [Fact]
    public void TheReceiversPricersStand()
    {
        // repriced on its own volatility with no reversion given, a market keeps its pricers' reversion, 0 here
        (CmsMarket market, SwaptionVolatilityStructure atm, SwaptionVolatilityCube cube, EvaluationContext context) = Market();
        double[] own = CubeInputs.Flat(market.ImpliedCmsSpreads());
        _ = market.Reprice(cube, 0.03, context);
        Assert.Equal(own, CubeInputs.Flat(market.ImpliedCmsSpreads()));
        Assert.Equal(own, CubeInputs.Flat(market.Reprice(atm, null, context).ImpliedCmsSpreads()));
        Assert.NotEqual(own, CubeInputs.Flat(market.Reprice(atm, 0.03, context).ImpliedCmsSpreads()));
    }

    // The calibration vectors' market, its linear TSR pricer (which reads the reversion) on the at-the-money matrix,
    // the matrix and their cube.
    private static (CmsMarket Market, SwaptionVolatilityStructure Atm, SwaptionVolatilityCube Cube, EvaluationContext Context) Market()
    {
        VectorCase c = VectorFile.Load("termstructures/volatility/swaption/cmsmarketcalibration").Cases[0];
        EvaluationContext context = c.Context();
        SwapIndex index = SwaptionInputs.SwapIndexOf(c.Input("index"));
        SwaptionVolatilityStructure atm = CubeInputs.AtmOf(c.Input("cube").GetProperty("atm"), context);
        var market = new CmsMarket(
            CubeInputs.Tenors(c.Input("swapLengths")),
            [index],
            index.IborIndex,
            CubeInputs.Rows(c.Input("bidAskSpreads")),
            [new LinearTsrPricer(atm, 0.0)],
            index.ForwardingTermStructure!,
            context);
        return (market, atm, CubeInputs.CubeOf(c.Input("cube"), context), context);
    }
}
