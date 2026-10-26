// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Stock vectors, <c>tests/vectors/instruments/stock.json</c>: <see cref="Stock"/>'s NPV, its error estimate and
/// valuation date, which it doesn't provide, and its expiry.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StockVectorTests
{
    [Fact]
    public void StockVector()
    {
        var file = VectorFile.Load("instruments/stock");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var stock = new Stock(c.Input("stock").GetProperty("quote").GetDouble());
            InstrumentResults r = stock.Calculate(context);
            check.Output(c, "NPV", r.NPV);
            check.Output(c, "errorEstimate", r.ErrorEstimate);
            check.Output(c, "valuationDate", r.ValuationDate?.SerialNumber);
            check.Output(c, "isExpired", stock.IsExpired(context));
        }

        check.Complete();
    }
}
