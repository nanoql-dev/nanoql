// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.CommodityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's commodity index vectors, <c>tests/vectors/cxx/experimental/commodities/commodityindex.json</c>:
/// <see cref="CommodityIndex"/>'s name, last quote date, emptiness, lot, fixing dates, stored fixings and forward
/// prices, the case's fixings in the context.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CommodityIndexCxxVectorTests
{
    [Fact]
    public void CommodityIndexCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/commodities/commodityindex");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CommodityIndex index = IndexOf(c.Input("index"), context);
            Date[] dates = Dates(c.Input("dates"));

            check.Output(c, "name", index.Name);
            check.Output(c, "text", index.ToString());
            check.Output(c, "lastQuoteDate", index.LastQuoteDate(context));
            check.Output(c, "empty", index.Empty(context));
            check.Output(c, "forwardCurveEmpty", index.ForwardCurveEmpty);
            check.Output(c, "lotQuantity", index.LotQuantity);
            check.Output(c, "validFixingDate", dates.Select(index.IsValidFixingDate).ToArray());
            check.Output(c, "fixing", dates.Select(d => (Func<object?>)(() => index.Fixing(d, context))).ToArray());
            check.Output(
                c, "forwardPrice", dates.Select(d => (Func<object?>)(() => index.ForwardPrice(d, context))).ToArray());
        }

        check.Complete();
    }
}
