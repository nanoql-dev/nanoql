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
/// The C++ harness's extended variance surface vectors,
/// <c>tests/vectors/cxx/experimental/volatility/extendedblackvariancesurface.json</c>:
/// <see cref="ExtendedBlackVarianceSurface"/> on a smile and a skew, bilinear and bicubic, each extrapolation constant or
/// the interpolator's, its maximum date and strike range, and its variances, volatilities and forward variances by date
/// and by time at strikes inside and outside its own, extrapolating; QuantLib's refusals. The surface as
/// <c>tools/cxx/src/experimental/volatility/extendedblackvariancesurface.cpp</c> builds it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExtendedBlackVarianceSurfaceCxxVectorTests
{
    [Fact]
    public void ExtendedBlackVarianceSurfaceCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/volatility/extendedblackvariancesurface");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement s = c.Input("surface");
            Date[] dates = Serials(s.GetProperty("dates"));
            Date[] queries = Serials(s.GetProperty("queryDates"));
            double[] times = Doubles(s.GetProperty("queryTimes"));
            double[] ks = Doubles(s.GetProperty("queryStrikes"));
            string interpolation = s.GetProperty("interpolation").GetString()!;
            var surface = new Lazy<ExtendedBlackVarianceSurface>(() =>
            {
                var bilinear = new ExtendedBlackVarianceSurface(
                    new Date(s.GetProperty("referenceDate").GetInt32()),
                    new NullCalendar(),
                    dates,
                    Doubles(s.GetProperty("strikes")),
                    Doubles(s.GetProperty("vols")),
                    CashFlowInputs.DayCounterOf(s.GetProperty("dayCounter")),
                    (ExtendedBlackVarianceSurfaceExtrapolation)s.GetProperty("lowerExtrapolation").GetInt32(),
                    (ExtendedBlackVarianceSurfaceExtrapolation)s.GetProperty("upperExtrapolation").GetInt32());
                return interpolation switch
                {
                    "Bilinear" => bilinear,
                    "Bicubic" => bilinear.SetInterpolation<Bicubic>(),
                    _ => throw new InvalidOperationException($"unknown interpolation '{interpolation}'"),
                };
            });
            (string Key, Func<object?> Value)[] outputs =
            [
                ("maxDate", () => surface.Value.MaxDate),
                ("minStrike", () => surface.Value.MinStrike()),
                ("maxStrike", () => surface.Value.MaxStrike()),
                ("varianceAtDate", () => queries.Select(d => ks.Select(k => surface.Value.BlackVariance(d, k, true)).ToArray()).ToArray()),
                ("volAtDate", () => queries.Select(d => ks.Select(k => surface.Value.BlackVol(d, k, true)).ToArray()).ToArray()),
                ("forwardVariance", () => Enumerable.Range(0, queries.Length - 1)
                    .Select(i => ks.Select(k => (Func<object?>)(() => surface.Value.BlackForwardVariance(queries[i], queries[i + 1], k, true))).ToArray())
                    .ToArray()),
                ("varianceAtTime", () => times.Select(t => ks.Select(k => surface.Value.BlackVariance(t, k, true)).ToArray()).ToArray()),
                ("volAtTime", () => times.Select(t => ks.Select(k => surface.Value.BlackVol(t, k, true)).ToArray()).ToArray()),
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
