// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The PerformanceOptionPathPricer vectors, <c>tests/vectors/cxx/pricingengines/cliquet/mcperformanceengine.json</c>:
/// <see cref="PerformanceOptionPathPricer"/>'s value on fixed paths and its refusal of a path whose length isn't the
/// discounts' and one more, against QuantLib's through the C++ harness.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MCPerformanceEngineCxxVectorTests
{
    [Fact]
    public void MCPerformanceEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/cliquet/mcperformanceengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] values = c.Doubles("values");
            var path = new MonteCarloPath(new NanoQL.TimeGrid(1.0, values.Length - 1), new Vector(values));
            var pricer = new PerformanceOptionPathPricer(
                (OptionType)c.Int("optionType"), c.Double("strike"), c.Doubles("discounts"));
            if (c.Expected.TryGetProperty("error", out _))
            {
                check.Output(c, "error", () => Message(() => pricer.Evaluate(path)));
                continue;
            }

            check.Output(c, "value", () => pricer.Evaluate(path));
        }

        check.Complete();
    }

    // The refusal's message, QuantLib's text: an argument exception's without the parameter's name .NET appends.
    private static string Message(Action act)
    {
        try
        {
            act();
        }
        catch (ArgumentException e) when (e.ParamName is not null)
        {
            return e.Message.Replace($" (Parameter '{e.ParamName}')", string.Empty, StringComparison.Ordinal);
        }
        catch (Exception e) when (e is ArgumentException or NanoQLException)
        {
            return e.Message;
        }

        return "no refusal";
    }
}
