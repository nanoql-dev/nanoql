// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's piecewise default curve vectors,
/// <c>tests/vectors/cxx/termstructures/credit/piecewisedefaultcurve.json</c>:
/// <see cref="PiecewiseDefaultCurve{TTraits, TInterpolator, TBootstrap}"/> on the default-density, survival-probability
/// and hazard-rate traits and the interpolators the wheel doesn't bind, bootstrapped on spread and upfront helpers:
/// its nodes, survival probabilities and hazard rates (null where the stability filter dropped them), and the ISDA
/// engine's refusal of a linear default-density curve.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PiecewiseDefaultCurveCxxVectorTests
{
    [Fact]
    public void PiecewiseDefaultCurveCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/credit/piecewisedefaultcurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CdsHelper[] helpers = [.. c.Input("helpers").EnumerateArray().Select(s => CreditInputs.HelperOf(s, context))];
            Date reference = c.DateInput("referenceDate");
            DayCounter dc = CreditInputs.DayCounterOf(c.Input("dayCounter"));
            Func<Curve> build = (c.String("traits"), c.String("interpolator")) switch
            {
                ("DefaultDensity", "BackwardFlat") => () => Curve.Of(new PiecewiseDefaultCurve<DefaultDensity, BackwardFlat, IterativeBootstrap>(reference, helpers, dc, context)),
                ("DefaultDensity", "Linear") => () => Curve.Of(new PiecewiseDefaultCurve<DefaultDensity, Linear, IterativeBootstrap>(reference, helpers, dc, context)),
                ("SurvivalProbability", "Linear") => () => Curve.Of(new PiecewiseDefaultCurve<SurvivalProbability, Linear, IterativeBootstrap>(reference, helpers, dc, context)),
                ("SurvivalProbability", "LogLinear") => () => Curve.Of(new PiecewiseDefaultCurve<SurvivalProbability, LogLinear, IterativeBootstrap>(reference, helpers, dc, context)),
                ("HazardRate", "Linear") => () => Curve.Of(new PiecewiseDefaultCurve<HazardRate, Linear, IterativeBootstrap>(reference, helpers, dc, context)),
                _ => throw new InvalidOperationException($"unknown curve {c.String("traits")} on {c.String("interpolator")}"),
            };
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", () => build() is null ? 1 : 0);
                continue;
            }

            Curve curve = build();
            Date[] sample = c.DateInputs("sample");
            check.Output(c, "dates", curve.Dates.Select(d => (object?)d));
            check.Output(c, "times", curve.Times.Select(t => (object?)t));
            check.Output(c, "data", CreditInputs.Kept(c, "data", curve.Data.Select(x => (Func<object?>)(() => x))));
            check.Output(
                c,
                "survivalProbability",
                CreditInputs.Kept(c, "survivalProbability", sample.Select(d => (Func<object?>)(() => curve.Term.SurvivalProbability(d, true)))));
            check.Output(
                c, "hazardRate", CreditInputs.Kept(c, "hazardRate", sample.Select(d => (Func<object?>)(() => curve.Term.HazardRate(d, true)))));
        }

        check.Complete();
    }

    private sealed record Curve(
        DefaultProbabilityTermStructure Term, ImmutableArray<Date> Dates, ImmutableArray<double> Times, ImmutableArray<double> Data)
    {
        public static Curve Of<TTraits, TInterpolator>(PiecewiseDefaultCurve<TTraits, TInterpolator, IterativeBootstrap> c)
            where TTraits : struct, IDefaultProbabilityTraits
            where TInterpolator : struct, IInterpolator =>
            new(c, c.Dates(), c.Times(), c.Data());
    }
}
