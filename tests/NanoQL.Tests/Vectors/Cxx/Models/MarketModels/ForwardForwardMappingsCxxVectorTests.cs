// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's forward-forward mapping vectors, <c>tests/vectors/cxx/models/marketmodels/forwardforwardmappings.json</c>:
/// <see cref="ForwardForwardMappings.RestrictCurveState"/>'s restricted states at several multipliers and offsets, and
/// its refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ForwardForwardMappingsCxxVectorTests
{
    [Fact]
    public void ForwardForwardMappingsCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/forwardforwardmappings");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var cs = new LMMCurveState(c.Doubles("rateTimes"));
            cs.SetOnForwardRates(c.Doubles("forwards"));
            int multiplier = c.Int("multiplier"), offset = c.Int("offset");
            if (c.String("call") == "refused")
            {
                check.Output(c, "restriction", Lazy(() => ForwardForwardMappings.RestrictCurveState(cs, multiplier, offset)));
                continue;
            }

            LMMCurveState r = ForwardForwardMappings.RestrictCurveState(cs, multiplier, offset);
            int n = r.NumberOfRates;
            check.Output(c, "rateTimes", r.RateTimes);
            check.Output(c, "forwardRates", r.ForwardRates().ToArray());
            check.Output(c, "discountRatios", Enumerable.Range(0, n + 1).Select(i => r.DiscountRatio(i, n)).ToArray());
        }

        check.Complete();
    }
}
