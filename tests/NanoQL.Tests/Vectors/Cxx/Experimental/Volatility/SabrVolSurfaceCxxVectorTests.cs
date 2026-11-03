// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's SABR surface vectors, <c>tests/vectors/cxx/experimental/volatility/sabrvolsurface.json</c>:
/// <see cref="SabrVolSurface"/> on Euribor over an abcd at-the-money curve, its maximum date and strike range, its
/// tenors' option dates and volatility spreads, its volatility spreads by date, and at times whose dates are fixing
/// dates its sections' levels, exercise dates and volatilities and its at-the-money volatility, the elements one-ulp
/// refits leave stable; QuantLib's refusals. The surface as
/// <c>tools/cxx/src/experimental/volatility/sabrvolsurface.cpp</c> builds it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SabrVolSurfaceCxxVectorTests
{
    [Fact]
    public void SabrVolSurfaceCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/volatility/sabrvolsurface");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement s = c.Input("surface");
            Period[] tenors = [.. s.GetProperty("tenors").EnumerateArray().Select(p => new Period(p[0].GetInt32(), (TimeUnit)p[1].GetInt32()))];
            double[][] volSpreads = [.. s.GetProperty("volSpreads").EnumerateArray().Select(Doubles)];
            Date[] spreadDates = [.. s.GetProperty("spreadDates").EnumerateArray().Select(x => new Date(x.GetInt32()))];
            double[] times = Doubles(s.GetProperty("queryTimes"));
            double[] offsets = Doubles(s.GetProperty("strikeOffsets"));
            var surface = new Lazy<SabrVolSurface>(() =>
            {
                var index = new Euribor(
                    new Period(s.GetProperty("indexMonths").GetInt32(), TimeUnit.Months),
                    new FlatForward(context.EvaluationDate, s.GetProperty("rate").GetDouble(), new Actual365Fixed()));
                AbcdAtmVolCurve atm = AbcdAtmVolCurve.Create(0, new TARGET(), tenors, Doubles(s.GetProperty("atmVols")), context);
                return new SabrVolSurface(index, atm, tenors, Doubles(s.GetProperty("atmRateSpreads")), volSpreads, context);
            });
            (string Key, Func<object?> Value)[] outputs =
            [
                ("maxDate", () => surface.Value.MaxDate),
                ("minStrike", () => surface.Value.MinStrike()),
                ("maxStrike", () => surface.Value.MaxStrike()),
                ("volatilitySpreadsAtTenor", () => CubeInputs.Kept(c, "volatilitySpreadsAtTenor", tenors.Select(p => (Func<object?>)(() => surface.Value.VolatilitySpreads(p).ToArray())))),
                ("optionDates", () => CubeInputs.Kept(c, "optionDates", tenors.Select(p => (Func<object?>)(() => surface.Value.OptionDateFromTenor(p))))),
                ("volatilitySpreads", () => CubeInputs.Kept(c, "volatilitySpreads", spreadDates.Select(d => (Func<object?>)(() => surface.Value.VolatilitySpreads(d).ToArray())))),
                ("atmLevel", () => CubeInputs.Kept(c, "atmLevel", times.Select(t => (Func<object?>)(() => surface.Value.SmileSection(t, true).AtmLevel())))),
                ("exerciseDate", () => CubeInputs.Kept(c, "exerciseDate", times.Select(t => (Func<object?>)(() => surface.Value.SmileSection(t, true).ExerciseDate)))),
                ("volatility", () => CubeInputs.Kept(c, "volatility", times.Select(t => (Func<object?>)(() =>
                {
                    SmileSection section = surface.Value.SmileSection(t, true);
                    double level = section.AtmLevel()!.Value;
                    return offsets.Select(k => section.Volatility(level + k)).ToArray();
                })))),
                ("atmVol", () => CubeInputs.Kept(c, "atmVol", times.Select(t => (Func<object?>)(() => surface.Value.AtmVol(t, true))))),
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
