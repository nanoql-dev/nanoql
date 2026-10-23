// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Gaussian1d swaption volatility vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/swaption/gaussian1dswaptionvolatility.json</c>:
/// <see cref="Gaussian1dSwaptionVolatility"/> on a Euribor swap base with curves or the model's, three GSR models and
/// an engine given; its dates, strike range and maximum swap tenor, its volatilities by tenor, date and time where
/// stable (the S7 plan's S7R6), and its smile sections by date and by time, their exercise dates and times and levels.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Gaussian1dSwaptionVolatilityCxxVectorTests
{
    [Fact]
    public void Gaussian1dSwaptionVolatilityCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/swaption/gaussian1dswaptionvolatility");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Gaussian1dSwaptionVolatility vol = VolatilityOf(c.Input("volatility"), context);
            double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
            Period[] optionTenors = [.. c.Input("optionTenors").EnumerateArray().Select(SwapInputs.PeriodOf)];
            Period[] swapTenors = [.. c.Input("swapTenors").EnumerateArray().Select(SwapInputs.PeriodOf)];
            Date[] optionDates = c.DateInputs("optionDates");
            double[] optionTimes = VolatilityInputs.Reals(c.Input("optionTimes"));
            double[] swapLengths = VolatilityInputs.Reals(c.Input("swapLengths"));

            check.Output(c, "referenceDate", vol.ReferenceDate);
            check.Output(c, "maxDate", vol.MaxDate);
            check.Output(c, "minStrike", vol.MinStrike());
            check.Output(c, "maxStrike", vol.MaxStrike());
            check.Output(c, "maxSwapTenor", new[] { vol.MaxSwapTenor.Length, (int)vol.MaxSwapTenor.Units });
            Check(check, c, "volatility(Period,Period)", Grid(optionTenors, swapTenors, strikes, (o, s, k) => vol.Volatility(o, s, k)));
            Check(check, c, "volatility(Date,Period)", Grid(optionDates, swapTenors, strikes, (o, s, k) => vol.Volatility(o, s, k)));
            Check(check, c, "volatility(Time,Time)", Grid(optionTimes, swapLengths, strikes, (o, s, k) => vol.Volatility(o, s, k)));

            SmileSection[] byDate = [.. optionDates.SelectMany(d => swapTenors.Select(s => vol.SmileSection(d, s)))];
            SmileSection[] byTime = [.. optionTimes.SelectMany(t => swapLengths.Select(l => vol.SmileSection(t, l)))];
            foreach ((string name, SmileSection[] sections) in new[] { ("smileSection(Date,Period)", byDate), ("smileSection(Time,Time)", byTime) })
            {
                check.Output(c, name + ".exerciseDate", sections.Select(x => x.ExerciseDate).ToArray());
                check.Output(c, name + ".exerciseTime", sections.Select(x => x.ExerciseTime).ToArray());
                check.Output(c, name + ".atmLevel", sections.Select(x => x.AtmLevel()).ToArray());
            }
        }

        check.Complete();
    }

    private static Gaussian1dSwaptionVolatility VolatilityOf(JsonElement spec, EvaluationContext context)
    {
        Gsr model = Gaussian1dInputs.GsrOf(spec.GetProperty("model"));
        return new Gaussian1dSwaptionVolatility(
            CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
            (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
            Gaussian1dSmileSectionCxxVectorTests.SwapIndexOf(spec.GetProperty("swapIndex")),
            model,
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            context,
            Gaussian1dSmileSectionCxxVectorTests.EngineOf(spec.GetProperty("engine"), model));
    }

    // The values over options × swaps × strikes, row by option and swap, each computed when compared.
    private static Func<object?>[][] Grid<TOption, TSwap>(
        TOption[] options, TSwap[] swaps, double[] strikes, Func<TOption, TSwap, double, double> f) =>
        [.. options.SelectMany(o => swaps.Select(s => strikes.Select(k => (Func<object?>)(() => f(o, s, k))).ToArray()))];

    private static void Check(VectorAssert check, VectorCase c, string name, object value)
    {
        if (c.Expected.TryGetProperty(name, out _))
        {
            check.Output(c, name, value);
        }
    }
}
