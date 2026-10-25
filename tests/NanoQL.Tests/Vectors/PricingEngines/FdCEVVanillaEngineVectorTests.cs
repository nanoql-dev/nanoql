// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.VanillaFdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdCEVVanillaEngine vectors, <c>tests/vectors/pricingengines/vanilla/fdcevvanillaengine.json</c>:
/// <see cref="FdCEVVanillaEngine"/>'s NPV, delta, gamma and theta for European, American and Bermudan calls and puts on
/// CEV forwards over elasticities on both sides of 1 and below 0, other grids, scaling factors, every named scheme, a
/// cash-or-nothing payoff, and an expired option.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdCEVVanillaEngineVectorTests
{
    [Fact]
    public void FdCEVVanillaEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/fdcevvanillaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckPricing(check, c, FdCev);
        }

        check.Complete();
    }
}
