// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.BarrierInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdBlackScholesRebateEngine vectors,
/// <c>tests/vectors/pricingengines/barrier/fdblackscholesrebateengine.json</c>:
/// <see cref="FdBlackScholesRebateEngine"/>'s value of a rebate, with its delta, gamma and theta, for the four barrier
/// types on five processes, and on other grids, schemes, the local volatility and cash dividends; an expired option and
/// the refusal of an American exercise.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdBlackScholesRebateEngineVectorTests
{
    [Fact]
    public void FdBlackScholesRebateEngineVector()
    {
        var file = VectorFile.Load("pricingengines/barrier/fdblackscholesrebateengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckFdBarrierPricing(check, c, rebate: true);
        }

        check.Complete();
    }
}
