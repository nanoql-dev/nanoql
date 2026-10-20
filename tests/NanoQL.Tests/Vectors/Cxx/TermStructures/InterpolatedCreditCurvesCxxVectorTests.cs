// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's interpolated credit curve vectors,
/// <c>tests/vectors/cxx/termstructures/credit/interpolatedcreditcurves.json</c>:
/// <see cref="InterpolatedHazardRateCurve{TInterpolator}"/>,
/// <see cref="InterpolatedDefaultDensityCurve{TInterpolator}"/> and
/// <see cref="InterpolatedSurvivalProbabilityCurve{TInterpolator}"/> on backward-flat, linear and log-linear
/// interpolation, with and without jumps: their nodes, jump dates and times, and their probabilities, densities and
/// hazard rates at sample dates, refused where QuantLib refuses them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterpolatedCreditCurvesCxxVectorTests
{
    [Fact]
    public void InterpolatedCreditCurvesCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/credit/interpolatedcreditcurves");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (DefaultProbabilityTermStructure curve, ImmutableArray<Date> dates, ImmutableArray<double> times, ImmutableArray<double> data) =
                CurveOf(c.Input("curve"));
            Date[] sample = c.DateInputs("sample");
            check.Output(c, "dates", dates.Select(d => (object?)d));
            check.Output(c, "times", times.Select(t => (object?)t));
            check.Output(c, "data", data.Select(x => (object?)x));
            check.Output(c, "maxDate", curve.MaxDate);
            check.Output(c, "jumpDates", curve.JumpDates().Select(d => (object?)d));
            check.Output(c, "jumpTimes", curve.JumpTimes().Select(t => (object?)t));
            check.Output(c, "survivalProbability", sample.Select(d => (object?)(Func<object?>)(() => curve.SurvivalProbability(d, true))));
            check.Output(c, "defaultDensity", sample.Select(d => (object?)(Func<object?>)(() => curve.DefaultDensity(d, true))));
            check.Output(c, "hazardRate", sample.Select(d => (object?)(Func<object?>)(() => curve.HazardRate(d, true))));
        }

        check.Complete();
    }

    private static (DefaultProbabilityTermStructure Curve, ImmutableArray<Date> Dates, ImmutableArray<double> Times, ImmutableArray<double> Data) CurveOf(
        JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        string interpolator = spec.GetProperty("interpolator").GetString()!;
        Date[] dates = [.. spec.GetProperty("dates").EnumerateArray().Select(e => new Date(e.GetInt32()))];
        double[] data = [.. spec.GetProperty("data").EnumerateArray().Select(e => e.GetDouble())];
        DayCounter dc = CreditInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        double[]? jumps = spec.TryGetProperty("jumps", out JsonElement j) ? [.. j.EnumerateArray().Select(e => e.GetDouble())] : null;
        Date[]? jumpDates = spec.TryGetProperty("jumpDates", out JsonElement jd)
            ? [.. jd.EnumerateArray().Select(e => new Date(e.GetInt32()))]
            : null;
        return (type, interpolator) switch
        {
            ("HazardRateCurve", "BackwardFlat") => Of(new InterpolatedHazardRateCurve<BackwardFlat>(dates, data, dc, null, jumps, jumpDates)),
            ("HazardRateCurve", "Linear") => Of(new InterpolatedHazardRateCurve<Linear>(dates, data, dc, null, jumps, jumpDates)),
            ("HazardRateCurve", "LogLinear") => Of(new InterpolatedHazardRateCurve<LogLinear>(dates, data, dc, null, jumps, jumpDates)),
            ("DefaultDensityCurve", "BackwardFlat") => Of(new InterpolatedDefaultDensityCurve<BackwardFlat>(dates, data, dc, null, jumps, jumpDates)),
            ("DefaultDensityCurve", "Linear") => Of(new InterpolatedDefaultDensityCurve<Linear>(dates, data, dc, null, jumps, jumpDates)),
            ("DefaultDensityCurve", "LogLinear") => Of(new InterpolatedDefaultDensityCurve<LogLinear>(dates, data, dc, null, jumps, jumpDates)),
            ("SurvivalProbabilityCurve", "BackwardFlat") => Of(new InterpolatedSurvivalProbabilityCurve<BackwardFlat>(dates, data, dc, null, jumps, jumpDates)),
            ("SurvivalProbabilityCurve", "Linear") => Of(new InterpolatedSurvivalProbabilityCurve<Linear>(dates, data, dc, null, jumps, jumpDates)),
            ("SurvivalProbabilityCurve", "LogLinear") => Of(new InterpolatedSurvivalProbabilityCurve<LogLinear>(dates, data, dc, null, jumps, jumpDates)),
            _ => throw new InvalidOperationException($"unknown curve {type} on {interpolator}"),
        };
    }

    private static (DefaultProbabilityTermStructure, ImmutableArray<Date>, ImmutableArray<double>, ImmutableArray<double>) Of<TTraits, TInterpolator>(
        IInterpolatedCurve<TTraits, TInterpolator> curve)
        where TTraits : struct
        where TInterpolator : struct, IInterpolator =>
        ((DefaultProbabilityTermStructure)curve, curve.Dates(), curve.Times(), curve.Data());
}
