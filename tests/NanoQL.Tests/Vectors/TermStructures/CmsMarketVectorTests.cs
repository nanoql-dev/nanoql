// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CMS market's vectors, <c>tests/vectors/termstructures/volatility/swaption/cmsmarket.json</c>: a market of four
/// swap lengths over two swap indexes, on the analytic and numeric Hagan pricers and the linear TSR pricer, and two
/// markets repriced; its browse table, implied spreads, spread errors and weighted errors.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CmsMarketVectorTests
{
    [Fact]
    public void CmsMarketVector()
    {
        var file = VectorFile.Load("termstructures/volatility/swaption/cmsmarket");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CmsMarket market = MarketOf(c, context);
            Matrix w = MatrixOf(c.Input("weights"));
            check.Output(c, "browse", CubeInputs.Flat(market.Browse()));
            check.Output(c, "impliedCmsSpreads", CubeInputs.Flat(market.ImpliedCmsSpreads()));
            check.Output(c, "spreadErrors", CubeInputs.Flat(market.SpreadErrors()));
            check.Output(c, "weightedSpreadError", market.WeightedSpreadError(w));
            check.Output(c, "weightedSpotNpvError", market.WeightedSpotNpvError(w));
            check.Output(c, "weightedFwdNpvError", market.WeightedFwdNpvError(w));
            check.Output(c, "weightedSpreadErrors", market.WeightedSpreadErrors(w).ToArray());
            check.Output(c, "weightedSpotNpvErrors", market.WeightedSpotNpvErrors(w).ToArray());
            check.Output(c, "weightedFwdNpvErrors", market.WeightedFwdNpvErrors(w).ToArray());
        }

        check.Complete();
    }

    /// <summary>The market a case describes, repriced when its <c>"reprice"</c> says.</summary>
    /// <param name="c">The case.</param>
    /// <param name="context">The case's context.</param>
    /// <returns>The market.</returns>
    internal static CmsMarket MarketOf(VectorCase c, EvaluationContext context)
    {
        SwaptionVolatilityStructure vol = CubeInputs.AtmOf(c.Input("volatility"), context);
        SwapIndex[] indexes = [.. c.Input("indexes").EnumerateArray().Select(SwaptionInputs.SwapIndexOf)];
        JsonElement pricer = c.Input("pricer");
        CmsCouponPricer[] pricers = [.. indexes.Select(_ => PricerOf(pricer, vol))];
        var market = new CmsMarket(
            CubeInputs.Tenors(c.Input("swapLengths")),
            indexes,
            indexes[0].IborIndex,
            CubeInputs.Rows(c.Input("bidAskSpreads")),
            pricers,
            indexes[0].ForwardingTermStructure!,
            context);
        JsonElement reprice = c.Input("reprice");
        if (reprice.ValueKind == JsonValueKind.Null)
        {
            return market;
        }

        JsonElement m = reprice.GetProperty("meanReversion");
        return market.Reprice(
            CubeInputs.AtmOf(reprice.GetProperty("volatility"), context),
            m.ValueKind == JsonValueKind.Null ? null : m.GetDouble(),
            context);
    }

    /// <summary>A pricer spec's pricer: {"type": "analytic" | "numeric" | "tsr", "model", "meanReversion"}.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="vol">The volatility.</param>
    /// <returns>The pricer.</returns>
    internal static CmsCouponPricer PricerOf(JsonElement spec, SwaptionVolatilityStructure vol) =>
        spec.GetProperty("type").GetString() == "tsr"
            ? new LinearTsrPricer(vol, spec.GetProperty("meanReversion").GetDouble())
            : ConundrumPricerVectorTests.PricerOf(spec, vol);

    /// <summary>A matrix input's matrix.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The matrix.</returns>
    internal static Matrix MatrixOf(JsonElement rows)
    {
        double[][] r = CubeInputs.Rows(rows);
        var m = new Matrix(r.Length, r[0].Length);
        for (int i = 0; i < r.Length; i++)
        {
            for (int j = 0; j < r[i].Length; j++)
            {
                m[i, j] = r[i][j];
            }
        }

        return m;
    }
}
