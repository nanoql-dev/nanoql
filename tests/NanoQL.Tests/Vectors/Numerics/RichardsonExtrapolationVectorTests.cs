// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Richardson extrapolation vectors, <c>tests/vectors/math/richardsonextrapolation.json</c>:
/// <see cref="RichardsonExtrapolation"/> of four functions from two step sizes, with the order given at four scaling
/// factors and with it unknown at three pairs (Brent's solve for it failing on one function), and the refusals of
/// scaling factors at or below 1, of s not below t and of the one-factor form with no order.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RichardsonExtrapolationVectorTests
{
    // the generator's functions, written as it writes them
    private static readonly Dictionary<string, Func<double, double>> Functions = new()
    {
        ["2 + 3h^2 - h^3"] = h => 2.0 + (3.0 * h * h) - (h * h * h),
        ["1/(1 - h)"] = h => 1.0 / (1.0 - h),
        ["(1 + h^4)/(1 + h^2)"] = h => (1.0 + (h * h * h * h)) / (1.0 + (h * h)),
        ["0.5 - 0.25h^3 + h^5"] = h => 0.5 - (0.25 * h * h * h) + (h * h * h * h * h),
    };

    [Fact]
    public void RichardsonExtrapolationVector()
    {
        var file = VectorFile.Load("math/richardsonextrapolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Func<double, double> f = Functions[c.String("function")];
            double h = c.Double("deltaH");
            var known = new RichardsonExtrapolation(f, h, c.Double("n"));
            var unknown = new RichardsonExtrapolation(f, h);
            if (c.Expected.TryGetProperty("value", out _))
            {
                double t = c.Double("t");
                Func<object?> value = c.Inputs.TryGetProperty("s", out _)
                    ? () => unknown.Evaluate(t, c.Double("s"))
                    : c.Inputs.TryGetProperty("unknown", out _) ? () => unknown.Evaluate(t) : () => known.Evaluate(t);
                check.Output(c, "value", value);
                continue;
            }

            check.Output(c, "known", c.Doubles("t").Select(t => known.Evaluate(t)).ToArray());
            int i = 0;
            foreach (System.Text.Json.JsonElement p in c.Input("pairs").EnumerateArray())
            {
                double t = p[0].GetDouble(), s = p[1].GetDouble();
                check.Output(c, $"unknown{i++}", () => unknown.Evaluate(t, s));
            }
        }

        check.Complete();
    }
}
