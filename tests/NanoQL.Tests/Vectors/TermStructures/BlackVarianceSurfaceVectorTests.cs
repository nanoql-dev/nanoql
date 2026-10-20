// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BlackVarianceSurface vectors, <c>tests/vectors/termstructures/volatility/equityfx/blackvariancesurface.json</c>:
/// surfaces of two to five strikes and one to four dates, their extrapolations and interpolations, and the
/// constructor's refusals. The points where QuantLib's variance is negative are left out of the file.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackVarianceSurfaceVectorTests
{
    [Fact]
    public void BlackVarianceSurfaceVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/blackvariancesurface");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", Lazy(() => BlackVolOf(c.Input("structure"), context).BlackVariance(0.1, 100.0)));
                continue;
            }

            BlackVolTermStructure s = BlackVolOf(c.Input("structure"), context);
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            check.Output(c, "referenceDate", s.ReferenceDate);
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "calendar", s.Calendar!.Name);
            check.Output(c, "dayCounter", s.DayCounter.Name);
            check.Output(c, "blackVariance", points.Select(p => (object?)s.BlackVariance(p[0], p[1], true)).ToArray());
            check.Output(c, "blackVol", points.Select(p => (object?)s.BlackVol(p[0], p[1], true)).ToArray());
            check.Output(c, "outsideStrikes", Lazy(() => s.BlackVol(0.5, 1e4)));
        }

        check.Complete();
    }
}
