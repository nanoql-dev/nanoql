// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdBlackScholesVanillaEngine vectors,
/// <c>tests/vectors/pricingengines/vanilla/fdblackscholesvanillaengine.json</c>:
/// <see cref="FdBlackScholesVanillaEngine"/>'s NPV, delta, gamma and theta for European, American and Bermudan calls
/// and puts on five processes, and on other payoffs, grids, damping steps, every named scheme, the local volatility, a
/// quanto helper and cash dividends under the spot model; and an expired option.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdBlackScholesVanillaEngineVectorTests
{
    [Fact]
    public void FdBlackScholesVanillaEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/fdblackscholesvanillaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckFdPricing(check, c);
        }

        check.Complete();
    }
}
