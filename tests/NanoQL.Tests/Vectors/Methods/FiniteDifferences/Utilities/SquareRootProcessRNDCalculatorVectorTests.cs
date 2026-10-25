// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The SquareRootProcessRNDCalculator vectors,
/// <c>tests/vectors/methods/finitedifferences/utilities/squarerootprocessrndcalculator.json</c>: the square-root
/// process's density, distribution and inverse at three times, and its stationary density, distribution and inverse,
/// on four parameter sets whose stationary product stays finite.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SquareRootProcessRNDCalculatorVectorTests
{
    [Fact]
    public void SquareRootProcessRNDCalculatorVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/utilities/squarerootprocessrndcalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var calculator = new SquareRootProcessRNDCalculator(
                c.Double("v0"), c.Double("kappa"), c.Double("theta"), c.Double("sigma"));
            double[] times = c.Doubles("times"), values = c.Doubles("values"), probabilities = c.Doubles("probabilities");
            check.Output(
                c,
                "pdf",
                (object?[])[.. times.Select(t => (object?)values.Select(x => EquityInputs.Lazy(() => calculator.Pdf(x, t))).ToArray())]);
            check.Output(
                c,
                "cdf",
                (object?[])[.. times.Select(t => (object?)values.Select(x => EquityInputs.Lazy(() => calculator.Cdf(x, t))).ToArray())]);
            check.Output(
                c,
                "invcdf",
                (object?[])[.. times.Select(t => (object?)probabilities.Select(p => EquityInputs.Lazy(() => calculator.InvCdf(p, t))).ToArray())]);
            check.Output(c, "stationaryPdf", (object?[])[.. values.Select(x => EquityInputs.Lazy(() => calculator.StationaryPdf(x)))]);
            check.Output(c, "stationaryCdf", (object?[])[.. values.Select(x => EquityInputs.Lazy(() => calculator.StationaryCdf(x)))]);
            check.Output(
                c, "stationaryInvCdf", (object?[])[.. probabilities.Select(p => EquityInputs.Lazy(() => calculator.StationaryInvCdf(p)))]);
        }

        check.Complete();
    }
}
