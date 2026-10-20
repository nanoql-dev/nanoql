// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="GreeksFunctions"/>' harness vectors, <c>tests/vectors/cxx/pricingengines/greeks.json</c>: the
/// Black-Scholes theta of eight processes over flat forwards and a constant volatility, at three (value, delta, gamma)
/// triples each, and the theta per day of seven thetas.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GreeksCxxVectorTests
{
    [Fact]
    public void GreeksCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/greeks");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("thetas", out _))
            {
                check.Output(c, "thetaPerDay", c.Doubles("thetas").Select(t => (object?)GreeksFunctions.DefaultThetaPerDay(t)).ToArray());
                continue;
            }

            Date reference = c.DateInput("referenceDate");
            DayCounter dc = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
            var process = new BlackScholesMertonProcess(
                c.Double("spot"),
                new FlatForward(reference, c.Double("dividendYield"), dc),
                new FlatForward(reference, c.Double("riskFreeRate"), dc),
                new BlackConstantVol(reference, new NullCalendar(), c.Double("volatility"), dc));
            double[][] triples = [.. c.Input("triples").EnumerateArray().Select(Doubles)];
            check.Output(
                c,
                "theta",
                triples.Select(t => (object?)GreeksFunctions.BlackScholesTheta(process, t[0], t[1], t[2])).ToArray());
        }

        check.Complete();
    }
}
