// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's AmericanPayoffAtExpiry vectors, <c>tests/vectors/cxx/pricingengines/americanpayoffatexpiry.json</c>:
/// the value of cash-or-nothing and asset-or-nothing payoffs at expiry, knock-in and knock-out, over 400 random
/// markets, and the refusals; the inputs nanoql fixes (a variance below ε) and those #2855 changes are left out of the
/// file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AmericanPayoffAtExpiryCxxVectorTests
{
    [Fact]
    public void AmericanPayoffAtExpiryCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/americanpayoffatexpiry");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            check.Output(c, "value", () => new AmericanPayoffAtExpiry(
                c.Double("spot"),
                c.Double("discount"),
                c.Double("dividendDiscount"),
                c.Double("variance"),
                AmericanPayoffAtHitCxxVectorTests.PayoffOf(c.Input("payoff")),
                c.Bool("knockIn")).Value());
        }

        check.Complete();
    }
}
