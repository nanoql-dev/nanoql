// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's AmericanPayoffAtHit vectors, <c>tests/vectors/cxx/pricingengines/americanpayoffathit.json</c>:
/// the value, delta, gamma and rho of cash-or-nothing and asset-or-nothing payoffs at the hit over 400 random markets,
/// and the refusals; the inputs nanoql fixes (λ² &lt; 0, a variance below ε) are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AmericanPayoffAtHitCxxVectorTests
{
    [Fact]
    public void AmericanPayoffAtHitCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/americanpayoffathit");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            AmericanPayoffAtHit Pricer() => new(
                c.Double("spot"), c.Double("discount"), c.Double("dividendDiscount"), c.Double("variance"), PayoffOf(c.Input("payoff")));
            if (c.Expected.TryGetProperty("value", out _))
            {
                check.Output(c, "value", () => Pricer().Value());
            }

            if (c.Expected.TryGetProperty("delta", out _))
            {
                AmericanPayoffAtHit p = Pricer();
                check.Output(c, "delta", p.Delta());
                check.Output(c, "gamma", p.Gamma());
            }

            if (c.Expected.TryGetProperty("rho", out _))
            {
                check.Output(c, "rho", () => Pricer().Rho(c.Double("maturity")));
            }
        }

        check.Complete();
    }

    // A cash-or-nothing or asset-or-nothing payoff from the harness's spec.
    internal static StrikedTypePayoff PayoffOf(JsonElement spec)
    {
        var type = (OptionType)spec.GetProperty("optionType").GetInt32();
        double strike = spec.GetProperty("strike").GetDouble();
        return spec.GetProperty("type").GetString() switch
        {
            "CashOrNothingPayoff" => new CashOrNothingPayoff(type, strike, spec.GetProperty("cashPayoff").GetDouble()),
            "AssetOrNothingPayoff" => new AssetOrNothingPayoff(type, strike),
            string s => throw new InvalidOperationException($"unknown payoff '{s}'"),
            null => throw new InvalidOperationException("no payoff type"),
        };
    }
}
