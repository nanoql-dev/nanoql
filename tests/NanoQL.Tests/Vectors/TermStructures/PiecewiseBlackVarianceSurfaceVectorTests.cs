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
/// The PiecewiseBlackVarianceSurface vectors,
/// <c>tests/vectors/termstructures/volatility/equityfx/piecewiseblackvariancesurface.json</c>: surfaces from flat,
/// linearly interpolated and SABR sections and from a grid, on three day counters, with and without extrapolation, and
/// the constructors' refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PiecewiseBlackVarianceSurfaceVectorTests
{
    [Fact]
    public void PiecewiseBlackVarianceSurfaceVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/piecewiseblackvariancesurface");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", Lazy(() => SurfaceOf(c, context).MaxDate));
                continue;
            }

            PiecewiseBlackVarianceSurface s = SurfaceOf(c, context);
            if (c.Bool("extrapolate"))
            {
                s = s.EnableExtrapolation();
            }

            double[] times = c.Doubles("times"), strikes = c.Doubles("strikes");
            // the tenors' times and one between the first and last, as the generator takes them
            double[] tenorTimes = [.. c.Ints("dates").Select(d => s.TimeFromReference(new Date(d)))];
            double[] sectionTimes = [.. tenorTimes, (0.5 * (tenorTimes[0] + tenorTimes[^1])) + 0.01];
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "blackVariance", Grid(times, strikes, (t, k) => s.BlackVariance(t, k, true)));
            check.Output(c, "blackVol", Grid(times, strikes, (t, k) => s.BlackVol(t, k, true)));
            check.Output(c, "sectionTimes", sectionTimes);
            check.Output(c, "sectionVol", Grid(sectionTimes, strikes, (t, k) => s.SmileSection(t, true).Volatility(k)));
        }

        check.Complete();
    }

    // The surface a case describes, from its sections or its grid.
    private static PiecewiseBlackVarianceSurface SurfaceOf(VectorCase c, EvaluationContext context)
    {
        DayCounter dc = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
        Date[] dates = [.. c.Ints("dates").Select(s => new Date(s))];
        if (c.String("form") == "grid")
        {
            return PiecewiseBlackVarianceSurface.MakeFromGrid(
                context.EvaluationDate, dates, c.Doubles("gridStrikes"), InterpolationInputs.MatrixOf(c.Input("vols")), dc);
        }

        SmileSection[] sections = [.. c.Input("sections").EnumerateArray().Select(spec => SectionOf(spec, context))];
        return new PiecewiseBlackVarianceSurface(context.EvaluationDate, dates, sections, dc);
    }

    private static SmileSection SectionOf(JsonElement spec, EvaluationContext context) =>
        spec.GetProperty("type").GetString() is "FlatSmileSection" or "SabrSmileSection"
            ? SmileSectionInputs.SectionOf(spec, context)
            : InterpolatedSmileSectionVectorTests.SectionOf(spec, context);

    private static object?[] Grid(double[] times, double[] strikes, Func<double, double, double> f) =>
        [.. times.Select(t => (object?)strikes.Select(k => Lazy(() => f(t, k))).ToArray())];
}
