// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's abcd curve vectors, <c>tests/vectors/cxx/experimental/volatility/abcdatmvolcurve.json</c>:
/// <see cref="AbcdAtmVolCurve"/>'s fit after its first calculation, its factors, option times, maximum date and strike
/// range, its volatility, variance and factor at times inside its fitted tenors, and its volatility and variance at its
/// fitted tenors and at dates, the outputs one-ulp refits leave stable; QuantLib's refusals. The curve as
/// <c>tools/cxx/src/experimental/volatility/abcdatmvolcurve.cpp</c> builds it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AbcdAtmVolCurveCxxVectorTests
{
    [Fact]
    public void AbcdAtmVolCurveCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/volatility/abcdatmvolcurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement s = c.Input("curve");
            Calendar calendar = s.GetProperty("calendar").GetString() == "TARGET" ? new TARGET() : new NullCalendar();
            Period[] tenors = [.. s.GetProperty("tenors").EnumerateArray().Select(p => new Period(p[0].GetInt32(), (TimeUnit)p[1].GetInt32()))];
            bool[]? inclusion = s.GetProperty("inclusion").ValueKind == JsonValueKind.Null
                ? null
                : [.. s.GetProperty("inclusion").EnumerateArray().Select(f => f.GetBoolean())];
            var curve = new Lazy<AbcdAtmVolCurve>(() => AbcdAtmVolCurve.Create(
                s.GetProperty("settlementDays").GetInt32(), calendar, tenors, Doubles(s.GetProperty("vols")), context, inclusion));
            double[] times = Doubles(s.GetProperty("times"));
            Period[] queryTenors = [.. s.GetProperty("queryTenors").EnumerateArray().Select(p => new Period(p[0].GetInt32(), (TimeUnit)p[1].GetInt32()))];
            Date[] queryDates = [.. s.GetProperty("queryDates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
            (string Key, Func<object?> Value)[] outputs =
            [
                ("a", () => curve.Value.A),
                ("b", () => curve.Value.B),
                ("c", () => curve.Value.C),
                ("d", () => curve.Value.D),
                ("rmsError", () => curve.Value.RmsError),
                ("maxError", () => curve.Value.MaxError),
                ("endCriteria", () => (int)curve.Value.EndCriteria),
                ("k", () => curve.Value.K().ToArray()),
                ("optionTimes", () => curve.Value.OptionTimes.ToArray()),
                ("maxDate", () => curve.Value.MaxDate.SerialNumber),
                ("minStrike", () => curve.Value.MinStrike()),
                ("maxStrike", () => curve.Value.MaxStrike()),
                ("atmVol", () => times.Select(t => curve.Value.AtmVol(t)).ToArray()),
                ("atmVariance", () => times.Select(t => curve.Value.AtmVariance(t)).ToArray()),
                ("kAt", () => times.Select(t => curve.Value.K(t)).ToArray()),
                ("atmVolAtTenor", () => queryTenors.Select(p => curve.Value.AtmVol(p)).ToArray()),
                ("atmVarianceAtTenor", () => queryTenors.Select(p => curve.Value.AtmVariance(p)).ToArray()),
                ("atmVolAtDate", () => queryDates.Select(d => curve.Value.AtmVol(d)).ToArray()),
                ("atmVarianceAtDate", () => queryDates.Select(d => curve.Value.AtmVariance(d)).ToArray()),
            ];
            foreach ((string key, Func<object?> value) in outputs)
            {
                if (c.Expected.TryGetProperty(key, out _))
                {
                    check.Output(c, key, value);
                }
            }
        }

        check.Complete();
    }
}
