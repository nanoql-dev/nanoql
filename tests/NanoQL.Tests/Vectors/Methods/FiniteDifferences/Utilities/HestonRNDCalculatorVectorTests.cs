// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The HestonRNDCalculator vectors, <c>tests/vectors/methods/finitedifferences/utilities/hestonrndcalculator.json</c>: the
/// density, distribution and inverse of the log spot under two Heston models on flat and zero curves at two accuracies,
/// every structure on one clock.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HestonRNDCalculatorVectorTests
{
    [Fact]
    public void HestonRNDCalculatorVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/utilities/hestonrndcalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("calculator");
            var calculator = new HestonRNDCalculator(
                HestonProcessVectorTests.HestonProcessOf(c.Input("process")),
                spec.GetProperty("eps").GetDouble(),
                spec.GetProperty("maxIterations").GetInt32());
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
