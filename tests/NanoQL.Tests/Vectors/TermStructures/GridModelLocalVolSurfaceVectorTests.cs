// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The GridModelLocalVolSurface vectors,
/// <c>tests/vectors/termstructures/volatility/equityfx/gridmodellocalvolsurface.json</c>: grids at their initial
/// parameters and at parameters set, their local volatilities, and the parameter vectors' refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GridModelLocalVolSurfaceVectorTests
{
    [Fact]
    public void GridModelLocalVolSurfaceVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/gridmodellocalvolsurface");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var surface = new GridModelLocalVolSurface(
                c.Context().EvaluationDate,
                [.. c.Ints("dates").Select(d => new Date(d))],
                c.Input("strikes").EnumerateArray().Select(k => (IEnumerable<double>)Doubles(k)),
                CashFlowInputs.DayCounterOf(c.Input("dayCounter")),
                (FixedLocalVolSurfaceExtrapolation)c.Int("lower"),
                (FixedLocalVolSurfaceExtrapolation)c.Int("upper"));
            JsonElement parameters = c.Input("params");
            if (c.Expected.TryGetProperty("setParams", out _))
            {
                check.Output(c, "setParams", Lazy(() => surface.WithParams(new Vector(Doubles(parameters)))));
                continue;
            }

            GridModelLocalVolSurface s = parameters.ValueKind == JsonValueKind.Null
                ? surface
                : (GridModelLocalVolSurface)surface.WithParams(new Vector(Doubles(parameters)));
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            check.Output(c, "params", (object?[])[.. s.Params().AsSpan().ToArray().Select(p => (object?)p)]);
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "maxTime", s.MaxTime);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "localVol", (object?[])[.. points.Select(p => Lazy(() => s.LocalVol(p[0], p[1], true)))]);
        }

        check.Complete();
    }
}
