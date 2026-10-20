// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The piecewise default curve vectors, <c>tests/vectors/termstructures/credit/piecewisedefaultcurve.json</c>:
/// <see cref="PiecewiseFlatHazardRate"/> bootstrapped on spread and upfront helpers, on a reference date or after
/// settlement days: its dates, times and hazard rates, and its survival probabilities and hazard rates at 20 dates
/// (the solved ones null where the stability filter dropped them).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PiecewiseDefaultCurveVectorTests
{
    [Fact]
    public void PiecewiseDefaultCurveVector()
    {
        var file = VectorFile.Load("termstructures/credit/piecewisedefaultcurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CdsHelper[] helpers = [.. c.Input("helpers").EnumerateArray().Select(s => CreditInputs.HelperOf(s, context))];
            var curve = CreditInputs.PiecewiseOf(c.Input("curve"), helpers, context);
            Date[] sample = c.DateInputs("sample");
            check.Output(c, "referenceDate", curve.ReferenceDate);
            check.Output(c, "maxDate", curve.MaxDate);
            check.Output(c, "dates", curve.Dates().Select(d => (object?)d));
            check.Output(c, "times", curve.Times().Select(t => (object?)t));
            check.Output(c, "data", CreditInputs.Kept(c, "data", curve.Data().Select(x => (Func<object?>)(() => x))));
            check.Output(
                c,
                "survivalProbability",
                CreditInputs.Kept(c, "survivalProbability", sample.Select(d => (Func<object?>)(() => curve.SurvivalProbability(d, true)))));
            check.Output(
                c, "hazardRate", CreditInputs.Kept(c, "hazardRate", sample.Select(d => (Func<object?>)(() => curve.HazardRate(d, true)))));
        }

        check.Complete();
    }
}
