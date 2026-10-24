// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CEVRNDCalculator vectors, <c>tests/vectors/methods/finitedifferences/utilities/cevrndcalculator.json</c>: the
/// mass at zero, the density, the cumulative distribution and its inverse of CEV forwards over elasticities on both
/// sides of 1, at four times; the inverse at the listed (t, p) points.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CEVRNDCalculatorVectorTests
{
    [Fact]
    public void CEVRNDCalculatorVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/utilities/cevrndcalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var calculator = new CEVRNDCalculator(c.Double("f0"), c.Double("alpha"), c.Double("beta"));
            double[] t = c.Doubles("t"), x = c.Doubles("x");
            double[][] inverse =
                [.. c.Input("invcdfPoints").EnumerateArray().Select(e => e.EnumerateArray().Select(d => d.GetDouble()).ToArray())];
            check.Output(c, "massAtZero", (object?[])[.. t.Select(s => Lazy(() => calculator.MassAtZero(s)))]);
            check.Output(c, "pdf", (object?[])[.. t.SelectMany(s => x.Select(f => Lazy(() => calculator.Pdf(f, s))))]);
            check.Output(c, "cdf", (object?[])[.. t.SelectMany(s => x.Select(f => Lazy(() => calculator.Cdf(f, s))))]);
            check.Output(c, "invcdf", (object?[])[.. inverse.Select(tp => Lazy(() => calculator.InvCdf(tp[1], tp[0])))]);
        }

        check.Complete();
    }

    private static object? Lazy(Func<object?> f) => f;
}
