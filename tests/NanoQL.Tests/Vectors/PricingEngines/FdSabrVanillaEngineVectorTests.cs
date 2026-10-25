// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.VanillaFdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdSabrVanillaEngine vectors, <c>tests/vectors/pricingengines/vanilla/fdsabrvanillaengine.json</c>:
/// <see cref="FdSabrVanillaEngine"/>'s NPV, and its delta, gamma and theta, which it doesn't report, for European,
/// American and Bermudan calls and puts on four SABR forwards, other grids, damping steps, scaling, probabilities and
/// schemes, the refusals of β = 1 and ν below 0, and an expired option.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdSabrVanillaEngineVectorTests
{
    [Fact]
    public void FdSabrVanillaEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/fdsabrvanillaengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckPricing(check, c, FdSabr);
        }

        check.Complete();
    }
}
