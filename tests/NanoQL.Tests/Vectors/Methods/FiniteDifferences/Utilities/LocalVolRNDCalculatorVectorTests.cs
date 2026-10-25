// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The LocalVolRNDCalculator vectors, <c>tests/vectors/methods/finitedifferences/utilities/localvolrndcalculator.json</c>:
/// the forward equation's density, distribution and inverse under fixed and Dupire local volatilities, its grids and
/// widening steps; the inverse at the points the stability filter kept.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LocalVolRNDCalculatorVectorTests
{
    [Fact]
    public void LocalVolRNDCalculatorVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/utilities/localvolrndcalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            YieldTermStructure r = CurveOf(c.Input("riskFree")), q = CurveOf(c.Input("dividend"));
            LocalVolTermStructure lv = LocalVolOf(c.Input("localVol"), r, q, c);
            JsonElement grid = c.Input("grid");
            JsonElement gaussian = grid[5];
            var calculator = new LocalVolRNDCalculator(
                c.Double("spot"),
                r,
                q,
                lv,
                grid[0].GetInt32(),
                grid[1].GetInt32(),
                grid[2].GetDouble(),
                grid[3].GetDouble(),
                grid[4].GetInt32(),
                gaussian.ValueKind == JsonValueKind.Null ? null : gaussian.GetDouble());
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            double[][] inverse = [.. c.Input("invcdfPoints").EnumerateArray().Select(Doubles)];
            double[] mesherTimes = c.Doubles("mesherTimes");
            check.Output(c, "pdf", (object?[])[.. points.Select(p => Lazy(() => calculator.Pdf(p[1], p[0])))]);
            check.Output(c, "cdf", (object?[])[.. points.Select(p => Lazy(() => calculator.Cdf(p[1], p[0])))]);
            check.Output(c, "invcdf", (object?[])[.. inverse.Select(tp => Lazy(() => calculator.InvCdf(tp[1], tp[0])))]);
            check.Output(
                c,
                "mesher",
                (object?[])[.. mesherTimes.Select(t => (object?)calculator.Mesher(t).Locations().Select(x => (object?)x).ToArray())]);
            check.Output(c, "rescaleTimeSteps", (object?[])[.. calculator.RescaleTimeSteps().Select(i => (object?)i)]);
        }

        check.Complete();
    }

    private static LocalVolTermStructure LocalVolOf(JsonElement spec, YieldTermStructure r, YieldTermStructure q, VectorCase c)
    {
        if (spec.GetProperty("type").GetString() == "Fixed")
        {
            return FixedLocalVolSurfaceVectorTests.SurfaceOf(spec.GetProperty("surface"));
        }

        BlackVolTermStructure black = BlackVolOf(spec.GetProperty("volatility"), c.Context());
        return new NoExceptLocalVolSurface(black, r, q, 100.0, spec.GetProperty("overwrite").GetDouble()).EnableExtrapolation();
    }
}
