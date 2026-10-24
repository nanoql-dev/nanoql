// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The overnight-index future's harness vectors, <c>tests/vectors/cxx/instruments/overnightindexfuture.json</c>: the
/// members the wheel hides, the index's name, the value and maturity dates and the convexity adjustment.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OvernightIndexFutureCxxVectorTests
{
    [Fact]
    public void OvernightIndexFutureCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/overnightindexfuture");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("arguments").EnumerateArray()];
            var future = new OvernightIndexFuture(
                OvernightInputs.IndexOf(a[0]),
                new Date(a[1].GetInt32()),
                new Date(a[2].GetInt32()),
                a[3].GetDouble(),
                (RateAveragingType)a[4].GetInt32());
            check.Output(c, "indexName", future.OvernightIndex.Name);
            check.Output(c, "valueDate", future.ValueDate);
            check.Output(c, "maturityDate", future.MaturityDate);
            check.Output(c, "convexityAdjustment", future.ConvexityAdjustment);
        }

        check.Complete();
    }
}
