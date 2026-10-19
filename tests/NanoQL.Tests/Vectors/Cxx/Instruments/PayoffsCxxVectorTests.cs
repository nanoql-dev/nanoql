// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's payoff vectors, <c>tests/vectors/cxx/instruments/payoffs.json</c>: every payoff's
/// <see cref="Payoff.Name"/> and <see cref="Payoff.Description"/>, whose numbers QuantLib writes in a C++ stream's
/// default format, over strikes in each of its forms; <see cref="CashOrNothingPayoff.CashPayoff"/> and
/// <see cref="GapPayoff.SecondStrike"/>; and every member of <see cref="NullPayoff"/>, <see cref="SuperFundPayoff"/>
/// and <see cref="SuperSharePayoff"/>, which the wheel doesn't build as QuantLib declares them, their constructors'
/// refusals among them. A payoff its constructor refuses gives a throw for each of its outputs.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PayoffsCxxVectorTests
{
    [Fact]
    public void PayoffsCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/payoffs");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] specs = [.. c.Input("payoffs").EnumerateArray()];
            Func<object?>[] Each(Func<Payoff, object?> member) =>
                specs.Select(s => (Func<object?>)(() => member(OptionInputs.PayoffOf(s)))).ToArray();

            check.Output(c, "name", Each(p => p.Name));
            check.Output(c, "description", Each(p => p.Description));
            if (c.Expected.TryGetProperty("cashPayoff", out _))
            {
                check.Output(c, "cashPayoff", Each(p => p is SuperSharePayoff s ? s.CashPayoff : ((CashOrNothingPayoff)p).CashPayoff));
            }

            if (c.Expected.TryGetProperty("secondStrike", out _))
            {
                check.Output(c, "secondStrike", Each(p => p switch
                {
                    GapPayoff g => g.SecondStrike,
                    SuperFundPayoff f => f.SecondStrike,
                    _ => ((SuperSharePayoff)p).SecondStrike,
                }));
            }

            if (c.Expected.TryGetProperty("optionType", out _))
            {
                check.Output(c, "optionType", Each(p => ((TypePayoff)p).OptionType));
                check.Output(c, "strike", Each(p => ((StrikedTypePayoff)p).Strike));
            }

            if (c.Expected.TryGetProperty("evaluate", out _))
            {
                double[] prices = VolatilityInputs.Reals(c.Input("prices"));
                check.Output(c, "evaluate", specs.Select(s => prices.Select(price =>
                    (Func<object?>)(() => OptionInputs.PayoffOf(s).Evaluate(price))).ToArray()).ToArray());
            }
        }

        check.Complete();
    }
}
