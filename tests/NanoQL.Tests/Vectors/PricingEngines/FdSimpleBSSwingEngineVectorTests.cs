// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.VanillaFdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdSimpleBSSwingEngine vectors, <c>tests/vectors/pricingengines/vanilla/fdsimplebsswingengine.json</c>:
/// <see cref="FdSimpleBSSwingEngine"/>'s NPV, delta, gamma and theta, and <see cref="VanillaSwingOption"/>'s expiry, for
/// swing options on forward and plain-vanilla payoffs over monthly and weekly dates and every number of rights, the
/// refusals of the rights' checks, an expired option, and <see cref="VanillaForwardPayoff"/>'s values.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdSimpleBSSwingEngineVectorTests
{
    [Fact]
    public void FdSimpleBSSwingEngineVector()
    {
        var file = VectorFile.Load("pricingengines/vanilla/fdsimplebsswingengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Expected.TryGetProperty("values", out _))
            {
                var payoff = new VanillaForwardPayoff(
                    (OptionType)c.Input("payoff").GetProperty("optionType").GetInt32(),
                    c.Input("payoff").GetProperty("strike").GetDouble());
                check.Output(c, "values", c.Input("prices").EnumerateArray().Select(p => (object?)payoff.Evaluate(p.GetDouble())).ToArray());
                continue;
            }

            check.Output(c, "isExpired", SwingOptionOf(c.Input("pricing")).IsExpired(c.Context()));
            CheckPricing(check, c, FdSwing);
        }

        check.Complete();
    }
}
