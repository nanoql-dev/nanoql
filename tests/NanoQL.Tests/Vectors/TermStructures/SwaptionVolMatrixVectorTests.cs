// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The swaption volatility matrix vectors, <c>tests/vectors/termstructures/volatility/swaption/swaptionvolmatrix.json</c>:
/// matrices on option tenors with a fixed reference date, on option dates, and floating (built by
/// <see cref="SwaptionVolatilityMatrix.Create"/> from the context), with shifts, flat extrapolation and extrapolation
/// enabled; their grid, and at 25 option-and-swap points their volatilities, variances, shifts, smile sections,
/// located corners and option dates from times; and the refusals. Three of them replace QuantLib's
/// <c>testSwaptionVolMatrixObservability</c>: a floating matrix built under two evaluation dates, and a matrix built with
/// a changed volatility, since a nanoql structure is rebuilt for a new date or new data.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwaptionVolMatrixVectorTests
{
    [Fact]
    public void SwaptionVolMatrixVector()
    {
        var file = VectorFile.Load("termstructures/volatility/swaption/swaptionvolmatrix");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", () => SwaptionInputs.MatrixOf(c.Input("matrix"), context) is null ? 0 : null);
                continue;
            }

            SwaptionVolatilityMatrix s = SwaptionInputs.MatrixOf(c.Input("matrix"), context);
            Point[] points = [.. c.Input("points").EnumerateArray().Select(Point.Of)];

            check.Output(c, "referenceDate", s.ReferenceDate);
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "volatilityType", s.VolatilityType);
            check.Output(c, "optionTenors", s.OptionTenors.Select(p => new object[] { p.Length, (int)p.Units }));
            check.Output(c, "optionDates", s.OptionDates);
            check.Output(c, "optionTimes", s.OptionTimes);
            check.Output(c, "swapTenors", s.SwapTenors.Select(p => new object[] { p.Length, (int)p.Units }));
            check.Output(c, "swapLengths", s.SwapLengths);
            check.Output(c, "volatilityDatePeriod", Over(points, p => s.Volatility(p.Date, p.SwapTenor, p.Strike, p.Extrapolate)));
            check.Output(c, "volatilityTimeTime", Over(points, p => s.Volatility(p.Time, p.SwapLength, p.Strike, p.Extrapolate)));
            check.Output(
                c, "blackVarianceDatePeriod", Over(points, p => s.BlackVariance(p.Date, p.SwapTenor, p.Strike, p.Extrapolate)));
            check.Output(
                c, "blackVarianceTimeTime", Over(points, p => s.BlackVariance(p.Time, p.SwapLength, p.Strike, p.Extrapolate)));
            check.Output(c, "shiftPeriodPeriod", Over(points, p => s.Shift(p.Tenor, p.SwapTenor, p.Extrapolate)));
            check.Output(c, "shiftDatePeriod", Over(points, p => s.Shift(p.Date, p.SwapTenor, p.Extrapolate)));
            check.Output(c, "shiftTimePeriod", Over(points, p => s.Shift(p.Time, p.SwapTenor, p.Extrapolate)));
            check.Output(c, "shiftPeriodTime", Over(points, p => s.Shift(p.Tenor, p.SwapLength, p.Extrapolate)));
            check.Output(c, "shiftDateTime", Over(points, p => s.Shift(p.Date, p.SwapLength, p.Extrapolate)));
            check.Output(c, "shiftTimeTime", Over(points, p => s.Shift(p.Time, p.SwapLength, p.Extrapolate)));
            check.Output(
                c,
                "smileSectionDatePeriod",
                Over(points, p => SmileSectionInputs.Summary(s.SmileSection(p.Date, p.SwapTenor, p.Extrapolate), p.Strike)));
            check.Output(
                c,
                "smileSectionTimeTime",
                Over(points, p => SmileSectionInputs.Summary(s.SmileSection(p.Time, p.SwapLength, p.Extrapolate), p.Strike)));
            check.Output(
                c,
                "smileVolatilityPeriodPeriod",
                Over(points, p => s.SmileSection(p.Tenor, p.SwapTenor, p.Extrapolate).Volatility(p.Strike)));
            check.Output(
                c,
                "smileVolatilityTimePeriod",
                Over(points, p => s.SmileSection(p.Time, p.SwapTenor, p.Extrapolate).Volatility(p.Strike)));
            check.Output(
                c,
                "smileVolatilityPeriodTime",
                Over(points, p => s.SmileSection(p.Tenor, p.SwapLength, p.Extrapolate).Volatility(p.Strike)));
            check.Output(
                c,
                "smileVolatilityDateTime",
                Over(points, p => s.SmileSection(p.Date, p.SwapLength, p.Extrapolate).Volatility(p.Strike)));
            check.Output(c, "locateDatePeriod", Over(points, p => Corner(s.Locate(p.Date, p.SwapTenor))));
            check.Output(c, "locateTimeTime", Over(points, p => Corner(s.Locate(p.Time, p.SwapLength))));
            check.Output(c, "optionDateFromTime", Over(points, p => s.OptionDateFromTime(p.Time)));
        }

        check.Complete();
    }

    private static int[] Corner((int OptionIndex, int SwapIndex) corner) => [corner.OptionIndex, corner.SwapIndex];

    // Each value computed when compared, so that a throw is checked.
    private static Func<object?>[] Over<T>(Point[] points, Func<Point, T> f) => [.. points.Select(p => (Func<object?>)(() => f(p)))];

    // An option-and-swap point of the inputs.
    private sealed record Point(
        Period Tenor, Date Date, double Time, Period SwapTenor, double SwapLength, double Strike, bool Extrapolate)
    {
        public static Point Of(JsonElement p) => new(
            VolatilityInputs.TenorOf(p.GetProperty("optionTenor")),
            new Date(p.GetProperty("optionDate").GetInt32()),
            IndexInputs.Real(p.GetProperty("optionTime")),
            VolatilityInputs.TenorOf(p.GetProperty("swapTenor")),
            IndexInputs.Real(p.GetProperty("swapLength")),
            IndexInputs.Real(p.GetProperty("strike")),
            p.GetProperty("extrapolate").GetBoolean());
    }
}
