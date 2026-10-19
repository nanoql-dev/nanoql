// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="StrippedOptionletAdapter"/>'s vectors,
/// <c>tests/vectors/termstructures/volatility/optionlet/strippedoptionletadapter.json</c>: adapters on strippers and
/// stripped optionlets, with and without extrapolation, their inspectors, volatilities by time and by date and Black
/// variances; and the adapter QuantLib refuses, on a stripper without settlement days.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StrippedOptionletAdapterVectorTests
{
    [Fact]
    public void StrippedOptionletAdapterVector()
    {
        var file = VectorFile.Load("termstructures/volatility/optionlet/strippedoptionletadapter");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            StrippedOptionletAdapter? adapter = null;
            check.Output(c, "built", () =>
            {
                adapter = StrippedOptionletAdapter.Create(SourceOf(c.Input("source"), context), context);
                return true;
            });
            if (adapter is null || !c.Expected.TryGetProperty("referenceDate", out _))
            {
                continue;
            }

            StrippedOptionletAdapter a = c.Input("enableExtrapolation").GetBoolean() ? adapter.EnableExtrapolation() : adapter;
            check.Output(c, "referenceDate", a.ReferenceDate);
            check.Output(c, "maxDate", a.MaxDate);
            check.Output(c, "maxTime", a.MaxTime);
            check.Output(c, "minStrike", a.MinStrike());
            check.Output(c, "maxStrike", a.MaxStrike());
            check.Output(c, "volatilityType", a.VolatilityType);
            check.Output(c, "displacement", a.Displacement);
            check.Output(c, "dayCounter", a.DayCounter.Name);
            check.Output(c, "calendar", a.Calendar!.Name);
            check.Output(c, "businessDayConvention", a.BusinessDayConvention);
            check.Output(c, "volatilityByTime", Points(c, "byTime", (p, k, e) => a.Volatility(p.GetDouble(), k, e)));
            check.Output(c, "volatilityByDate", Points(c, "byDate", (p, k, e) => a.Volatility(new Date(p.GetInt32()), k, e)));
            check.Output(c, "blackVarianceByTime", Points(c, "byTime", (p, k, e) => a.BlackVariance(p.GetDouble(), k, e)));
        }

        check.Complete();
    }

    private static StrippedOptionletBase SourceOf(JsonElement spec, EvaluationContext context) =>
        spec.GetProperty("kind").GetString() == "stripper"
            ? StripperInputs.StripperOf(spec, context)
            : StripperInputs.StrippedOptionletOf(spec.GetProperty("optionlet"), context);

    private static object?[] Points(VectorCase c, string name, Func<JsonElement, double, bool, double> volatility) =>
        [.. c.Input(name).EnumerateArray().Select(p => (Func<object?>)(() => volatility(p[0], InterpolationInputs.Number(p[1]), p[2].GetBoolean())))];
}
