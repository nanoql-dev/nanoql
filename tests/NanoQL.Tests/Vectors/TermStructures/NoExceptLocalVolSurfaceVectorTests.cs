// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The NoExceptLocalVolSurface vectors, <c>tests/vectors/termstructures/volatility/equityfx/noexceptlocalvolsurface.json</c>:
/// Dupire's local volatility over smooth and arbitrageable Black surfaces, the overwrite where the surface refuses;
/// the points near a negative extrapolated Black variance, which nanoql floors, left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NoExceptLocalVolSurfaceVectorTests
{
    [Fact]
    public void NoExceptLocalVolSurfaceVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/noexceptlocalvolsurface");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("process");
            BlackVolTermStructure black = BlackVolOf(spec.GetProperty("volatility"), c.Context()).EnableExtrapolation();
            var s = new NoExceptLocalVolSurface(
                black,
                CurveOf(spec.GetProperty("riskFree")),
                CurveOf(spec.GetProperty("dividend")),
                spec.GetProperty("x0").GetDouble(),
                c.Double("overwrite"));
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "localVol", (object?[])[.. points.Select(p => Lazy(() => s.LocalVol(p[0], p[1], true)))]);
        }

        check.Complete();
    }
}
