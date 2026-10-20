// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's beta vectors, <c>tests/vectors/cxx/math/beta.json</c>: <see cref="BetaFunctions.BetaFunction"/>
/// over a grid and at non-positive arguments; <see cref="BetaFunctions.BetaContinuedFraction"/> and
/// <see cref="BetaFunctions.IncompleteBetaFunction"/> over shapes and points with the default accuracy and iteration
/// limit (a case's <c>accuracy</c> null), at explicit ones, and at the arguments QuantLib refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BetaCxxVectorTests
{
    [Fact]
    public void BetaCxxVector()
    {
        var file = VectorFile.Load("cxx/math/beta");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Expected.TryGetProperty("betaFunction", out _))
            {
                double[] z = c.Doubles("z"), w = c.Doubles("w");
                object?[] values = [.. Enumerable.Range(0, z.Length).Select(i =>
                    (object?)(Func<object?>)(() => BetaFunctions.BetaFunction(z[i], w[i])))];
                check.Output(c, "betaFunction", values);
                continue;
            }

            double[] a = c.Doubles("a"), b = c.Doubles("b"), x = c.Doubles("x");
            bool defaults = c.Input("accuracy").ValueKind == JsonValueKind.Null;
            double[] accuracy = defaults ? [] : c.Doubles("accuracy");
            int[] maxIteration = defaults ? [] : c.Ints("maxIteration");

            object?[] Outputs(Func<double, double, double, double, int, double> f) =>
                [.. Enumerable.Range(0, a.Length).Select(i => (object?)(Func<object?>)(() => defaults
                    ? f(a[i], b[i], x[i], 1e-16, 100)
                    : f(a[i], b[i], x[i], accuracy[i], maxIteration[i])))];

            if (c.Expected.TryGetProperty("betaContinuedFraction", out _))
            {
                check.Output(c, "betaContinuedFraction", Outputs(BetaFunctions.BetaContinuedFraction));
            }

            check.Output(c, "incompleteBetaFunction", Outputs(BetaFunctions.IncompleteBetaFunction));
        }

        check.Complete();
    }
}
