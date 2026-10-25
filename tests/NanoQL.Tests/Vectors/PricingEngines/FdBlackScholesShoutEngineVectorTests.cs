// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.VanillaFdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdBlackScholesShoutEngine vectors, <c>tests/vectors/pricingengines/vanilla/fdblackscholesshoutengine.json</c>:
/// <see cref="FdBlackScholesShoutEngine"/>'s NPV, delta, gamma and theta for shout calls and puts, their windows from
/// today and later, on four processes and testFDShoutNPV's market and grid, other damping steps and schemes, a dividend
/// before the window, and an expired option.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdBlackScholesShoutEngineVectorTests
{
    [Fact]
    public void FdBlackScholesShoutEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/fdblackscholesshoutengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CheckPricing(check, c, FdShout);
        }

        check.Complete();
    }
}
