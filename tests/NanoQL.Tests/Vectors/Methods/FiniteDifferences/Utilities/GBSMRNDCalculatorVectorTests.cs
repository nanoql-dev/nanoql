// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The GBSMRNDCalculator vectors, <c>tests/vectors/methods/finitedifferences/utilities/gbsmrndcalculator.json</c>:
/// Breeden and Litzenberger's density, distribution and inverse under Black-Scholes-Merton processes with constant,
/// curve and surface volatilities, every structure on one clock; the inverse at the points the stability filter kept.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GBSMRNDCalculatorVectorTests
{
    [Fact]
    public void GBSMRNDCalculatorVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/utilities/gbsmrndcalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("process");
            BlackVolTermStructure vol = BlackVolOf(spec.GetProperty("volatility"), c.Context());
            if (c.Bool("extrapolate"))
            {
                vol = vol.EnableExtrapolation();
            }

            var process = new BlackScholesMertonProcess(
                spec.GetProperty("x0").GetDouble(), CurveOf(spec.GetProperty("dividend")), CurveOf(spec.GetProperty("riskFree")), vol);
            var calculator = new GBSMRNDCalculator(process);
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            double[][] inverse = [.. c.Input("invcdfPoints").EnumerateArray().Select(Doubles)];
            check.Output(c, "pdf", (object?[])[.. points.Select(p => Lazy(() => calculator.Pdf(p[1], p[0])))]);
            check.Output(c, "cdf", (object?[])[.. points.Select(p => Lazy(() => calculator.Cdf(p[1], p[0])))]);
            check.Output(c, "invcdf", (object?[])[.. inverse.Select(tp => Lazy(() => calculator.InvCdf(tp[1], tp[0])))]);
        }

        check.Complete();
    }
}
