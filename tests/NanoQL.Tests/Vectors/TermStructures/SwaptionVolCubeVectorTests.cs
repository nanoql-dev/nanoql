// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The swaption volatility cube base's vectors, <c>tests/vectors/termstructures/volatility/swaption/swaptionvolcube.json</c>:
/// interpolated and SABR cubes on the test fixture and its variations (an at-the-money matrix on another date, normal,
/// shifted and constant at-the-money structures, indexed coupons, a 12-month short index, exogenous discounting); what
/// each cube answers from its at-the-money structure and holds, its grid, its option dates from times, its shifts, and
/// its at-the-money strikes at 25 option dates and swap tenors, the refused ones among them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwaptionVolCubeVectorTests
{
    [Fact]
    public void SwaptionVolCubeVector()
    {
        var file = VectorFile.Load("termstructures/volatility/swaption/swaptionvolcube");
        var check = new VectorAssert(file);
        double[] times = [0.0, 0.5, 1.0, 7.25, 30.0, 35.0];
        (Period Option, double Time, Period Swap, double Length)[] shiftPoints =
        [
            (new(1, TimeUnit.Years), 1.0, new(2, TimeUnit.Years), 2.0),
            (new(10, TimeUnit.Years), 10.0, new(10, TimeUnit.Years), 10.0),
            (new(5, TimeUnit.Years), 5.0, new(7, TimeUnit.Years), 7.0),
            (new(30, TimeUnit.Years), 30.0, new(30, TimeUnit.Years), 30.0),
        ];
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            SwaptionVolatilityCube s = CubeInputs.CubeOf(c.Input("cube"), context);
            (Date Date, Period Swap)[] points =
            [
                .. c.Input("points").EnumerateArray().Select(p => (
                    new Date(p.GetProperty("optionDate").GetInt32()), VolatilityInputs.TenorOf(p.GetProperty("swapTenor")))),
            ];

            void Output<T>(string name, IEnumerable<T> values) => check.Output(c, name, CubeInputs.Kept(c, name, values));
            void Lazy(string name, IEnumerable<Func<object?>> values) => check.Output(c, name, CubeInputs.Kept(c, name, values));

            Output("referenceDate", [s.ReferenceDate]);
            Output("maxDate", [s.MaxDate]);
            Output("maxTime", [s.MaxTime]);
            Output("volatilityType", [(int)s.VolatilityType]);
            Output("calendar", [s.Calendar!.Name]);
            Output("businessDayConvention", [(int)s.BusinessDayConvention]);
            Output("dayCounter", [s.DayCounter.Name]);
            Output("minStrike", [s.MinStrike()]);
            Output("maxStrike", [s.MaxStrike()]);
            Output("strikeSpreads", s.StrikeSpreads);
            Output("volSpreads", s.VolSpreads().SelectMany(row => row));
            Output("vegaWeightedSmileFit", [s.VegaWeightedSmileFit]);
            Output("swapIndexBase", [s.SwapIndexBase.Name]);
            Output("shortSwapIndexBase", [s.ShortSwapIndexBase.Name]);
            Output("allowsExtrapolation", [s.AllowsExtrapolation]);
            Output("optionTenors", s.OptionTenors.SelectMany(p => new[] { p.Length, (int)p.Units }));
            Output("optionDates", s.OptionDates);
            Output("optionTimes", s.OptionTimes);
            Output("swapTenors", s.SwapTenors.SelectMany(p => new[] { p.Length, (int)p.Units }));
            Output("swapLengths", s.SwapLengths);
            Lazy("optionDateFromTime", times.Select(t => (Func<object?>)(() => s.OptionDateFromTime(t))));
            Lazy(
                "shiftDatePeriod",
                shiftPoints.Select(p => (Func<object?>)(() => s.Shift(s.OptionDateFromTenor(p.Option), p.Swap))));
            Lazy("shiftTimeTime", shiftPoints.Select(p => (Func<object?>)(() => s.Shift(p.Time, p.Length))));
            Lazy("atmStrike", points.Select(p => (Func<object?>)(() => s.AtmStrike(p.Date, p.Swap, context))));
        }

        check.Complete();
    }
}
