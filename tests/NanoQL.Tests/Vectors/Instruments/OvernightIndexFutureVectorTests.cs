// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The overnight-index future vectors, <c>tests/vectors/instruments/overnightindexfuture.json</c>: compounded and
/// averaged futures on SOFR-style and ESTR-style indexes, forward-starting, from today and seasoned, with a convexity
/// adjustment, maturing on a holiday and expired, their NPVs, adjustments and expiry; and their refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OvernightIndexFutureVectorTests
{
    [Fact]
    public void OvernightIndexFutureVector()
    {
        var file = VectorFile.Load("instruments/overnightindexfuture");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            OvernightIndex index = OvernightInputs.IndexOf(c.Input("index"));
            var future = new OvernightIndexFuture(
                index,
                c.DateInput("valueDate"),
                c.DateInput("maturityDate"),
                c.Double("convexityAdjustment"),
                (RateAveragingType)c.Int("averaging"));

            // overnightindexfuture.py leaves the NPV out where QuantLib's compounded future skips today's published
            // fixing on its value date.
            if (c.Expected.TryGetProperty("NPV", out _))
            {
                check.Output(c, "NPV", () => future.Calculate(context).NPV);
            }

            check.Output(c, "convexityAdjustment", future.ConvexityAdjustment);
            check.Output(c, "isExpired", future.IsExpired(context));
        }

        check.Complete();
    }
}
