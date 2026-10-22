// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BarrierInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdBlackScholesBarrierEngine vectors,
/// <c>tests/vectors/pricingengines/barrier/fdblackscholesbarrierengine.json</c>:
/// <see cref="FdBlackScholesBarrierEngine"/>'s NPV, delta, gamma and theta for the four barrier types with and without
/// a rebate on five processes, and on other grids, damping steps, every named scheme, the local volatility and cash
/// dividends; an expired option and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdBlackScholesBarrierEngineVectorTests
{
    [Fact]
    public void FdBlackScholesBarrierEngineVector()
    {
        var file = VectorFile.Load("pricingengines/barrier/fdblackscholesbarrierengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckFdBarrierPricing(check, c, rebate: false);
        }

        check.Complete();
    }
}
