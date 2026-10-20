// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The default-probability term structure vectors, <c>tests/vectors/termstructures/defaulttermstructure.json</c>:
/// <see cref="DefaultProbabilityTermStructure"/>'s probabilities, densities and hazard rates by date and by time, on
/// a flat hazard rate and the three interpolated curves, with and without extrapolation, and its refusals of reversed
/// dates and times.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DefaultTermStructureVectorTests
{
    [Fact]
    public void DefaultTermStructureVector()
    {
        var file = VectorFile.Load("termstructures/defaulttermstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            DefaultProbabilityTermStructure curve = CreditInputs.CurveOf(c.Input("curve"), context);
            Date d = c.DateInput("date");
            Date start = c.DateInput("start");
            double t = curve.TimeFromReference(d);
            double t0 = curve.TimeFromReference(start);
            check.Output(c, "time", t);
            check.Output(c, "survivalProbability", () => curve.SurvivalProbability(d, true));
            check.Output(c, "defaultProbability", () => curve.DefaultProbability(d, true));
            check.Output(c, "defaultDensity", () => curve.DefaultDensity(d, true));
            check.Output(c, "hazardRate", () => curve.HazardRate(d, true));
            check.Output(c, "survivalProbabilityTime", () => curve.SurvivalProbability(t, true));
            check.Output(c, "defaultProbabilityTime", () => curve.DefaultProbability(t, true));
            check.Output(c, "defaultDensityTime", () => curve.DefaultDensity(t, true));
            check.Output(c, "hazardRateTime", () => curve.HazardRate(t, true));
            check.Output(c, "defaultProbabilityBetween", () => curve.DefaultProbability(start, d, true));
            check.Output(c, "defaultProbabilityBetweenTimes", () => curve.DefaultProbability(t0, t, true));
            check.Output(c, "unextrapolated", () => curve.SurvivalProbability(d));
            check.Output(c, "reversed", () => curve.DefaultProbability(d, start - 1, true));
            check.Output(c, "reversedTimes", () => curve.DefaultProbability(t, t - 0.25, true));
        }

        check.Complete();
    }
}
