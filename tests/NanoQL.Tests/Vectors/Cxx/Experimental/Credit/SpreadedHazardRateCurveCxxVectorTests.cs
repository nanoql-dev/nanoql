// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's spreaded hazard-rate curve vectors,
/// <c>tests/vectors/cxx/experimental/credit/spreadedhazardratecurve.json</c>: <see cref="SpreadedHazardRateCurve"/> and
/// <see cref="FactorSpreadedHazardRateCurve"/> on flat and interpolated originals, their dates, maximum time and hazard
/// rates (their survival, exact here, is in the Divergences record).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SpreadedHazardRateCurveCxxVectorTests
{
    [Fact]
    public void SpreadedHazardRateCurveCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/spreadedhazardratecurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            EvaluationContext context = c.Context();
            DefaultProbabilityTermStructure original = CreditInputs.CurveOf(a[1], context);
            double spread = VectorCase.Real(a[2]);
            DefaultProbabilityTermStructure curve = a[0].GetBoolean()
                ? new FactorSpreadedHazardRateCurve(original, spread)
                : new SpreadedHazardRateCurve(original, spread);
            double[] times = [.. a[3].EnumerateArray().Select(VectorCase.Real)];
            check.Output(c, "referenceDate", curve.ReferenceDate);
            check.Output(c, "maxDate", curve.MaxDate);
            check.Output(c, "maxTime", curve.MaxTime);
            check.Output(c, "hazardRate", () => times.Select(t => curve.HazardRate(t, true)).ToArray());
        }

        check.Complete();
    }
}
