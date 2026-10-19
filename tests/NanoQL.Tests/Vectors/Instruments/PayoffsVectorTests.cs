// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The payoff vectors, <c>tests/vectors/instruments/payoffs.json</c>: the plain-vanilla, percentage-strike,
/// asset-or-nothing, cash-or-nothing and gap payoffs of both option types over strikes, their option types, strikes
/// and values at prices from below zero to +∞ and NaN; and the floating-type payoff, its value at each price and strike
/// and its one-argument <see cref="FloatingTypePayoff.Evaluate(double)"/>, which refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PayoffsVectorTests
{
    [Fact]
    public void PayoffsVector()
    {
        var file = VectorFile.Load("instruments/payoffs");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] prices = VolatilityInputs.Reals(c.Input("prices"));
            switch (c.String("kind"))
            {
                case "striked":
                {
                    var payoff = (StrikedTypePayoff)OptionInputs.PayoffOf(c.Input("payoff"));
                    check.Output(c, "optionType", payoff.OptionType);
                    check.Output(c, "strike", payoff.Strike);
                    check.Output(c, "evaluate", prices.Select(p => (Func<object?>)(() => payoff.Evaluate(p))).ToArray());
                    break;
                }

                case "floating":
                {
                    var payoff = (FloatingTypePayoff)OptionInputs.PayoffOf(c.Input("payoff"));
                    check.Output(c, "optionType", payoff.OptionType);
                    check.Output(c, "evaluate", VolatilityInputs.Grid(c.Doubles("strikes"), prices, (k, p) => payoff.Evaluate(p, k)));
                    check.Output(c, "evaluatePrice", prices.Take(3).Select(p => (Func<object?>)(() => payoff.Evaluate(p))).ToArray());
                    break;
                }

                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }
}
