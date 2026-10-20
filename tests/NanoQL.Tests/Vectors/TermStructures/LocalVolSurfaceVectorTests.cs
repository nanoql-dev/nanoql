// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The LocalVolSurface vectors, <c>tests/vectors/termstructures/volatility/equityfx/localvolsurface.json</c>:
/// Dupire's local volatility from constant, curve and surface Black volatilities, with QuantLib's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LocalVolSurfaceVectorTests
{
    [Fact]
    public void LocalVolSurfaceVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/localvolsurface");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var s = new LocalVolSurface(
                BlackVolOf(c.Input("blackVolatility"), context),
                CurveOf(c.Input("riskFree")),
                CurveOf(c.Input("dividend")),
                c.Double("underlying"));
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            if (c.Expected.TryGetProperty("referenceDate", out _))
            {
                check.Output(c, "referenceDate", s.ReferenceDate);
                check.Output(c, "maxDate", s.MaxDate);
                check.Output(c, "minStrike", s.MinStrike());
                check.Output(c, "maxStrike", s.MaxStrike());
                check.Output(c, "dayCounter", s.DayCounter.Name);
                check.Output(c, "calendarEmpty", s.Calendar is null);
            }

            check.Output(c, "localVol", points.Select(p => Lazy(() => s.LocalVol(p[0], p[1], true))).ToArray());
        }

        check.Complete();
    }
}
