// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BSMRNDCalculator vectors, <c>tests/vectors/methods/finitedifferences/utilities/bsmrndcalculator.json</c>: the
/// density, distribution and inverse of the log spot under Black-Scholes-Merton processes with a constant volatility or
/// a variance curve, every structure on one clock.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BSMRNDCalculatorVectorTests
{
    [Fact]
    public void BSMRNDCalculatorVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/utilities/bsmrndcalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var calculator = new BSMRNDCalculator(ProcessOf(c.Input("process"), c.Context()));
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            double[] times = c.Doubles("times"), probabilities = c.Doubles("probabilities");
            check.Output(c, "pdf", (object?[])[.. points.Select(p => Lazy(() => calculator.Pdf(p[1], p[0])))]);
            check.Output(c, "cdf", (object?[])[.. points.Select(p => Lazy(() => calculator.Cdf(p[1], p[0])))]);
            check.Output(
                c,
                "invcdf",
                (object?[])[.. times.Select(t => (object?)probabilities.Select(p => Lazy(() => calculator.InvCdf(p, t))).ToArray())]);
        }

        check.Complete();
    }
}
