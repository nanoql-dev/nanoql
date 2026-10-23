// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The abcd function vectors, <c>tests/vectors/math/abcdmathfunction.json</c>: on nine coefficient sets, the value,
/// derivative and primitive at nine times, definite integrals, the coefficients and the rolling-window coefficients,
/// the long-term value, the maximum's location and value where 1.44 agrees with nanoql's fixes, and validate's
/// refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AbcdMathFunctionVectorTests
{
    [Fact]
    public void AbcdMathFunctionVector()
    {
        var file = VectorFile.Load("math/abcdmathfunction");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] abcd = c.Doubles("abcd");
            if (c.Expected.TryGetProperty("construct", out _))
            {
                check.Output(c, "construct", () => new AbcdMathFunction(abcd));
                continue;
            }

            var f = new AbcdMathFunction(abcd);
            double[] times = c.Doubles("times");
            double[][] intervals = Pairs(c.Input("intervals"));
            double[][] windows = Pairs(c.Input("windows"));
            check.Output(c, "value", times.Select(f.Evaluate).ToArray());
            check.Output(c, "derivative", times.Select(f.Derivative).ToArray());
            check.Output(c, "primitive", times.Select(f.Primitive).ToArray());
            check.Output(c, "definiteIntegral", intervals.Select(i => f.DefiniteIntegral(i[0], i[1])).ToArray());
            check.Output(c, "coefficients", f.Coefficients);
            check.Output(c, "derivativeCoefficients", f.DerivativeCoefficients);
            check.Output(c, "definiteIntegralCoefficients", windows.Select(w => (object?)f.DefiniteIntegralCoefficients(w[0], w[1])).ToArray());
            check.Output(c, "definiteDerivativeCoefficients", windows.Select(w => (object?)f.DefiniteDerivativeCoefficients(w[0], w[1])).ToArray());
            check.Output(c, "longTermValue", f.LongTermValue());
            if (c.Expected.TryGetProperty("maximumLocation", out _))
            {
                check.Output(c, "maximumLocation", f.MaximumLocation());
            }

            if (c.Expected.TryGetProperty("maximumValue", out _))
            {
                check.Output(c, "maximumValue", f.MaximumValue());
            }
        }

        check.Complete();
    }

    private static double[][] Pairs(JsonElement e) =>
        [.. e.EnumerateArray().Select(p => p.EnumerateArray().Select(x => x.GetDouble()).ToArray())];
}
