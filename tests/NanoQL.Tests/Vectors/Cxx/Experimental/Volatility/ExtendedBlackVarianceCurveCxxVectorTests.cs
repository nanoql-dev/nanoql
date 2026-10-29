// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's extended variance curve vectors,
/// <c>tests/vectors/cxx/experimental/volatility/extendedblackvariancecurve.json</c>: <see cref="ExtendedBlackVarianceCurve"/>
/// on three day counters, linear, cubic and backward-flat, its maximum date and strike range, and its variances,
/// volatilities and forward variances by date and by time, extrapolating; QuantLib's refusals. The curve as
/// <c>tools/cxx/src/experimental/volatility/extendedblackvariancecurve.cpp</c> builds it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExtendedBlackVarianceCurveCxxVectorTests
{
    [Fact]
    public void ExtendedBlackVarianceCurveCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/volatility/extendedblackvariancecurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement s = c.Input("curve");
            Date[] dates = Serials(s.GetProperty("dates"));
            Date[] queries = Serials(s.GetProperty("queryDates"));
            double[] times = Doubles(s.GetProperty("queryTimes"));
            double strike = s.GetProperty("strike").GetDouble();
            string interpolation = s.GetProperty("interpolation").GetString()!;
            var curve = new Lazy<ExtendedBlackVarianceCurve>(() =>
            {
                var linear = new ExtendedBlackVarianceCurve(
                    new Date(s.GetProperty("referenceDate").GetInt32()),
                    dates,
                    Doubles(s.GetProperty("vols")),
                    CashFlowInputs.DayCounterOf(s.GetProperty("dayCounter")),
                    s.GetProperty("forceMonotoneVariance").GetBoolean());
                return interpolation switch
                {
                    "Linear" => linear,
                    "Cubic" => linear.SetInterpolation<Cubic>(),
                    "BackwardFlat" => linear.SetInterpolation<BackwardFlat>(),
                    _ => throw new InvalidOperationException($"unknown interpolation '{interpolation}'"),
                };
            });
            (string Key, Func<object?> Value)[] outputs =
            [
                ("maxDate", () => curve.Value.MaxDate),
                ("minStrike", () => curve.Value.MinStrike()),
                ("maxStrike", () => curve.Value.MaxStrike()),
                ("varianceAtDate", () => queries.Select(d => curve.Value.BlackVariance(d, strike, true)).ToArray()),
                ("volAtDate", () => queries.Select(d => curve.Value.BlackVol(d, strike, true)).ToArray()),
                ("forwardVariance", () => Enumerable.Range(0, queries.Length - 1)
                    .Select(i => (Func<object?>)(() => curve.Value.BlackForwardVariance(queries[i], queries[i + 1], strike, true)))
                    .ToArray()),
                ("varianceAtTime", () => times.Select(t => curve.Value.BlackVariance(t, strike, true)).ToArray()),
                ("volAtTime", () => times.Select(t => curve.Value.BlackVol(t, strike, true)).ToArray()),
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

    private static Date[] Serials(JsonElement e) => [.. e.EnumerateArray().Select(x => new Date(x.GetInt32()))];
}
