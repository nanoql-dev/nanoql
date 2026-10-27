// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Quotes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The vectors of the quotes on an index, <c>tests/vectors/quotes/futuresconvadjustmentquote.json</c> and
/// <c>lastfixingquote.json</c>: <see cref="FuturesConvAdjustmentQuote"/>'s convexity adjustment and inspectors by date
/// and by IMM code, and <see cref="LastFixingQuote"/>'s value and reference date on the case's stored fixings, each
/// built under the case's context.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IndexQuoteVectorTests
{
    [Fact]
    public void FuturesConvAdjustmentQuoteVector()
    {
        var file = VectorFile.Load("quotes/futuresconvadjustmentquote");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            IborIndex index = IndexInputs.IborIndexOf(c.Input("index"));
            double price = c.Double("futuresPrice"), vol = c.Double("volatility"), mr = c.Double("meanReversion");
            FuturesConvAdjustmentQuote q = c.String("form") == "date"
                ? new FuturesConvAdjustmentQuote(index, c.DateInput("futuresDate"), price, vol, mr, context)
                : new FuturesConvAdjustmentQuote(index, c.String("immCode"), price, vol, mr, context);
            check.Output(c, "value", q.Value);
            check.Output(c, "futuresValue", q.FuturesValue);
            check.Output(c, "volatility", q.Volatility);
            check.Output(c, "meanReversion", q.MeanReversion);
            check.Output(c, "immDate", q.ImmDate);
        }

        check.Complete();
    }

    [Fact]
    public void LastFixingQuoteVector()
    {
        var file = VectorFile.Load("quotes/lastfixingquote");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            IborIndex index = IndexInputs.IborIndexOf(c.Input("index"));
            check.Output(c, "value", () => new LastFixingQuote(index, context).Value);
            check.Output(c, "referenceDate", () => new LastFixingQuote(index, context).ReferenceDate);
        }

        check.Complete();
    }
}
