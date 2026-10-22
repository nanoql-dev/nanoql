// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Quotes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The DeltaVolQuote vectors, <c>tests/vectors/quotes/deltavolquote.json</c>: <see cref="DeltaVolQuote"/>'s value,
/// maturity, delta type and ATM type for delta and ATM quotes under each convention, and a delta quote's delta; the
/// ATM quote's delta is left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DeltaVolQuoteVectorTests
{
    [Fact]
    public void DeltaVolQuoteVector()
    {
        var file = VectorFile.Load("quotes/deltavolquote");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("quote");
            var deltaType = (DeltaVolQuoteDeltaType)spec.GetProperty("deltaType").GetInt32();
            double vol = spec.GetProperty("vol").GetDouble(), maturity = spec.GetProperty("maturity").GetDouble();
            DeltaVolQuote quote = spec.GetProperty("kind").GetString() == "delta"
                ? new DeltaVolQuote(spec.GetProperty("delta").GetDouble(), vol, maturity, deltaType)
                : new DeltaVolQuote(
                    vol, deltaType, maturity, (DeltaVolQuoteAtmType)spec.GetProperty("atmType").GetInt32());
            check.Output(c, "value", quote.Value);
            check.Output(c, "maturity", quote.Maturity);
            check.Output(c, "deltaType", quote.DeltaType);
            check.Output(c, "atmType", quote.AtmType);
            if (c.Expected.TryGetProperty("delta", out _))
            {
                check.Output(c, "delta", quote.Delta);
            }
        }

        check.Complete();
    }
}
