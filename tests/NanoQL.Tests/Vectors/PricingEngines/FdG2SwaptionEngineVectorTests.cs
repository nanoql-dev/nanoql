// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="FdG2SwaptionEngine"/>'s vectors, <c>tests/vectors/pricingengines/swaption/fdg2swaptionengine.json</c>:
/// European and Bermudan swaptions, a floating spread, an overnight-indexed swap and an expired one, on two G2 models
/// at two grids under six schemes; and the refusals of a forwarding curve on another day counter and of a past exercise
/// date.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdG2SwaptionEngineVectorTests
{
    [Fact]
    public void FdG2SwaptionEngineVector()
    {
        var file = VectorFile.Load("pricingengines/swaption/fdg2swaptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            check.Output(c, "NPV", c.Input("engines").EnumerateArray().Select(e => (Func<object?>)(() =>
            {
                Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
                return FdHullWhiteSwaptionEngineVectorTests.Priced(swaption, context, e).NPV;
            })));
        }

        check.Complete();
    }
}
