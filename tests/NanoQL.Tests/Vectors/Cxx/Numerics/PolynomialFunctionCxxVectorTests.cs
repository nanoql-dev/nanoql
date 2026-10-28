// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's polynomial vectors, <c>tests/vectors/cxx/math/polynomialmathfunction.json</c>:
/// <see cref="PolynomialFunction"/>'s values, derivatives, primitives, definite integrals and coefficients for 1 to 10
/// coefficients, an empty window's coefficients refused, and <see cref="PascalTriangle.Get"/>'s rows 0 to 34. The
/// wheel exposes neither.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PolynomialFunctionCxxVectorTests
{
    [Fact]
    public void PolynomialFunctionCxxVector()
    {
        var file = VectorFile.Load("cxx/math/polynomialmathfunction");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Expected.TryGetProperty("row", out _))
            {
                check.Output(
                    c, "row", PascalTriangle.Get(c.Int("order")).Select(b => b.ToString(CultureInfo.InvariantCulture)).ToArray());
                continue;
            }

            var f = new PolynomialFunction(c.Doubles("coefficients"));
            (double T1, double T2)[] windows = [.. c.Input("windows").EnumerateArray().Select(w => (w[0].GetDouble(), w[1].GetDouble()))];
            if (c.Expected.TryGetProperty("window", out _))
            {
                check.Output(c, "window", () =>
                {
                    foreach ((double t1, double t2) in windows)
                    {
                        f.DefiniteIntegralCoefficients(t1, t2);
                        f.DefiniteDerivativeCoefficients(t1, t2);
                    }

                    return null;
                });
                continue;
            }

            double[] ts = c.Doubles("t");
            check.Output(c, "order", f.Order);
            check.Output(c, "value", ts.Select(f.Evaluate).ToArray());
            check.Output(c, "derivative", ts.Select(f.Derivative).ToArray());
            check.Output(c, "primitive", ts.Select(f.Primitive).ToArray());
            check.Output(c, "definiteIntegral", windows.Select(w => f.DefiniteIntegral(w.T1, w.T2)).ToArray());
            check.Output(c, "coefficients", f.Coefficients().ToArray());
            check.Output(c, "derivativeCoefficients", f.DerivativeCoefficients().ToArray());
            check.Output(c, "primitiveCoefficients", f.PrimitiveCoefficients().ToArray());
            check.Output(
                c, "definiteIntegralCoefficients", windows.Select(w => (object?)f.DefiniteIntegralCoefficients(w.T1, w.T2).ToArray()).ToArray());
            check.Output(
                c, "definiteDerivativeCoefficients", windows.Select(w => (object?)f.DefiniteDerivativeCoefficients(w.T1, w.T2).ToArray()).ToArray());
        }

        check.Complete();
    }
}
