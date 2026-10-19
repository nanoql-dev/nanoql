// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The interpolated swaption volatility cube's vectors,
/// <c>tests/vectors/termstructures/volatility/swaption/interpolatedswaptionvolatilitycube.json</c>: cubes on the test
/// fixture and its variations; their volatility spreads by strike spread, and at 16 points their volatilities, Black
/// variances and smile sections by tenors, dates, times and lengths, the refused ones among them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterpolatedSwaptionVolatilityCubeVectorTests
{
    [Fact]
    public void InterpolatedSwaptionVolatilityCubeVector()
    {
        var file = VectorFile.Load("termstructures/volatility/swaption/interpolatedswaptionvolatilitycube");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var s = (InterpolatedSwaptionVolatilityCube)CubeInputs.CubeOf(c.Input("cube"), context);
            Point[] points = [.. c.Input("points").EnumerateArray().Select(Point.Of)];

            check.Output(
                c, "volSpreads", Enumerable.Range(0, s.StrikeSpreads.Length).SelectMany(i => CubeInputs.Flat(s.VolSpreads(i))));
            check.Output(c, "volatilityDatePeriod", Over(points, p => s.Volatility(p.Date, p.SwapTenor, p.Strike, p.Extrapolate)));
            check.Output(c, "volatilityTimeTime", Over(points, p => s.Volatility(p.Time, p.SwapLength, p.Strike, p.Extrapolate)));
            check.Output(
                c, "blackVarianceDatePeriod", Over(points, p => s.BlackVariance(p.Date, p.SwapTenor, p.Strike, p.Extrapolate)));
            check.Output(
                c, "blackVarianceTimeTime", Over(points, p => s.BlackVariance(p.Time, p.SwapLength, p.Strike, p.Extrapolate)));
            check.Output(
                c,
                "smileSectionPeriodPeriod",
                Sections(
                    points,
                    p => p.Tenor is Period tenor
                        ? s.SmileSection(tenor, p.SwapTenor, p.Extrapolate)
                        : s.SmileSection(p.Date, p.SwapTenor, p.Extrapolate)));
            check.Output(c, "smileSectionDatePeriod", Sections(points, p => s.SmileSection(p.Date, p.SwapTenor, p.Extrapolate)));
            check.Output(c, "smileSectionTimeTime", Sections(points, p => s.SmileSection(p.Time, p.SwapLength, p.Extrapolate)));
        }

        check.Complete();
    }

    // Each value computed when compared, so that a throw is checked.
    private static Func<object?>[] Over<T>(Point[] points, Func<Point, T> f) => [.. points.Select(p => (Func<object?>)(() => f(p)))];

    // Each section as the generator lists it: nine outputs a section, each a throw where the section is refused.
    private static Func<object?>[] Sections(Point[] points, Func<Point, SmileSection> section) =>
    [
        .. points.SelectMany(p => new Func<SmileSection, object?>[]
        {
            x => x.ExerciseDate,
            x => x.ExerciseTime,
            x => x.VolatilityType,
            x => x.Shift,
            x => x.AtmLevel(),
            x => x.MinStrike(),
            x => x.MaxStrike(),
            x => x.Volatility(p.Strike),
            x => x.Variance(p.Strike),
        }.Select(f => (Func<object?>)(() => f(section(p))))),
    ];

    // An option-and-swap point of the inputs; the option tenor null where the point gives its date.
    private sealed record Point(
        Period? Tenor, Date Date, double Time, Period SwapTenor, double SwapLength, double Strike, bool Extrapolate)
    {
        public static Point Of(JsonElement p) => new(
            p.GetProperty("optionTenor").ValueKind == JsonValueKind.Null ? null : VolatilityInputs.TenorOf(p.GetProperty("optionTenor")),
            new Date(p.GetProperty("optionDate").GetInt32()),
            IndexInputs.Real(p.GetProperty("optionTime")),
            VolatilityInputs.TenorOf(p.GetProperty("swapTenor")),
            IndexInputs.Real(p.GetProperty("swapLength")),
            IndexInputs.Real(p.GetProperty("strike")),
            p.GetProperty("extrapolate").GetBoolean());
    }
}
