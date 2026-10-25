// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FixedLocalVolSurface vectors, <c>tests/vectors/termstructures/volatility/equityfx/fixedlocalvolsurface.json</c>:
/// surfaces by each constructor, each strike extrapolation, linear and cubic in strike, and the constructors' refusals.
/// The points where nanoql's constant extrapolation at a node departs from QuantLib's are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FixedLocalVolSurfaceVectorTests
{
    [Fact]
    public void FixedLocalVolSurfaceVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/fixedlocalvolsurface");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("surface");
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", Lazy(() => SurfaceOf(spec).MaxTime));
                continue;
            }

            FixedLocalVolSurface s = SurfaceOf(spec);
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "maxTime", s.MaxTime);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "localVol", (object?[])[.. points.Select(p => Lazy(() => s.LocalVol(p[0], p[1], true)))]);
        }

        check.Complete();
    }

    // The surface a spec describes, by the constructor its form names, re-interpolated where the spec asks.
    internal static FixedLocalVolSurface SurfaceOf(JsonElement spec)
    {
        var reference = new Date(spec.GetProperty("referenceDate").GetInt32());
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        var lower = (FixedLocalVolSurfaceExtrapolation)spec.GetProperty("lower").GetInt32();
        var upper = (FixedLocalVolSurfaceExtrapolation)spec.GetProperty("upper").GetInt32();
        double[][] rows = [.. spec.GetProperty("vols").EnumerateArray().Select(Doubles)];
        var m = new Matrix(rows.Length, rows.Length == 0 ? 0 : rows[0].Length);
        for (int i = 0; i < m.Rows; i++)
        {
            for (int j = 0; j < m.Columns; j++)
            {
                m[i, j] = rows[i][j];
            }
        }

        FixedLocalVolSurface s = spec.GetProperty("form").GetString() switch
        {
            "dates" => new FixedLocalVolSurface(
                reference,
                [.. spec.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))],
                Doubles(spec.GetProperty("strikes")),
                m,
                dc,
                lower,
                upper),
            "times" => new FixedLocalVolSurface(reference, Doubles(spec.GetProperty("times")), Doubles(spec.GetProperty("strikes")), m, dc, lower, upper),
            _ => new FixedLocalVolSurface(
                reference,
                Doubles(spec.GetProperty("times")),
                spec.GetProperty("strikes").EnumerateArray().Select(k => (IEnumerable<double>)Doubles(k)),
                m,
                dc,
                lower,
                upper),
        };

        JsonElement interpolation = spec.GetProperty("interpolation");
        return interpolation.ValueKind == JsonValueKind.Null ? s : s.WithInterpolation<Cubic>();
    }
}
