// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="CmsMarket"/>'s quotes and weights of other sizes. QuantLib checks the first row of bid and ask spreads
/// only, reading it unchecked when there is none (<c>cmsmarket.cpp:63-69</c>), and reads every row's two columns per
/// index (<c>:80</c>); its weighted errors read the weights unchecked (<c>:207</c>, <c>:218</c>), its calibration
/// checking them when it is built: each an undefined read past a row's or a matrix's end. nanoql refuses each on
/// entry, the quotes before the first read.
/// </summary>
public class CmsMarketInputRefusalTests
{
    [Fact]
    public void AnEmptyOrRaggedQuoteTableIsRefused()
    {
        var e = Assert.Throws<ArgumentException>(() => Market([]));
        Assert.Equal("bidAskSpreads", e.ParamName);
        Assert.StartsWith("2*nSwapIndexes_ (2) != bidAskSpreads columns() (0)", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => Market([[0.01, 0.011], [0.012, 0.013], [0.014, 0.015, 0.016], [0.016, 0.017]]));
        Assert.Equal("bidAskSpreads", e.ParamName);
        Assert.StartsWith("2*nSwapIndexes_ (2) != bidAskSpreads columns() (3) in row 2", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WeightsOfAnotherShapeAreRefused()
    {
        CmsMarket market = Market([[0.01, 0.011], [0.012, 0.013], [0.014, 0.015], [0.016, 0.017]]);
        foreach (Func<Matrix, object> error in new Func<Matrix, object>[]
                 {
                     w => market.WeightedSpreadError(w), w => market.WeightedSpotNpvError(w), w => market.WeightedFwdNpvError(w),
                     w => market.WeightedSpreadErrors(w), w => market.WeightedSpotNpvErrors(w), w => market.WeightedFwdNpvErrors(w),
                 })
        {
            var e = Assert.Throws<ArgumentException>(() => error(new Matrix(3, 1, 1.0)));
            Assert.Equal("w", e.ParamName);
            Assert.StartsWith(
                "weights (3x1) must be a row per swap length (4) and a column per swap index (1)", e.Message, StringComparison.Ordinal);
        }
    }

    // The calibration vectors' market on the given quotes.
    private static CmsMarket Market(double[][] bidAskSpreads)
    {
        VectorCase c = VectorFile.Load("termstructures/volatility/swaption/cmsmarketcalibration").Cases[0];
        EvaluationContext context = c.Context();
        SwapIndex index = SwaptionInputs.SwapIndexOf(c.Input("index"));
        SwaptionVolatilityStructure atm = CubeInputs.AtmOf(c.Input("cube").GetProperty("atm"), context);
        return new CmsMarket(
            CubeInputs.Tenors(c.Input("swapLengths")),
            [index],
            index.IborIndex,
            bidAskSpreads,
            [CmsMarketVectorTests.PricerOf(c.Input("pricer"), atm)],
            index.ForwardingTermStructure!,
            context);
    }
}
