// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The constant swaption volatility vectors, <c>tests/vectors/termstructures/volatility/swaption/swaptionconstantvol.json</c>:
/// on structures with a fixed reference date and on structures built from settlement days and the evaluation context,
/// the reference and maximum dates, the strike range and the volatility type, and at 10 option-and-swap pairs
/// <see cref="SwaptionVolatilityStructure"/>'s volatility and Black variance by date and tenor and by time and length,
/// its shift in the six overloads, and the smile sections of the six overloads.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwaptionConstantVolVectorTests
{
    [Fact]
    public void SwaptionConstantVolVector()
    {
        var file = VectorFile.Load("termstructures/volatility/swaption/swaptionconstantvol");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            SwaptionVolatilityStructure s = VolatilityInputs.SwaptionVolatilityOf(c.Input("surface"), c.Context());
            Pair[] pairs = [.. c.Input("pairs").EnumerateArray().Select(Pair.Of)];

            check.Output(c, "referenceDate", s.ReferenceDate);
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "volatilityType", s.VolatilityType);
            check.Output(c, "volatilityDatePeriod", Over(pairs, p => s.Volatility(p.Date, p.SwapTenor, p.Strike, p.Extrapolate)));
            check.Output(c, "volatilityTimeTime", Over(pairs, p => s.Volatility(p.Time, p.SwapLength, p.Strike, p.Extrapolate)));
            check.Output(
                c, "blackVarianceDatePeriod", Over(pairs, p => s.BlackVariance(p.Date, p.SwapTenor, p.Strike, p.Extrapolate)));
            check.Output(
                c, "blackVarianceTimeTime", Over(pairs, p => s.BlackVariance(p.Time, p.SwapLength, p.Strike, p.Extrapolate)));
            check.Output(c, "shiftPeriodPeriod", Over(pairs, p => s.Shift(p.Tenor, p.SwapTenor, p.Extrapolate)));
            check.Output(c, "shiftDatePeriod", Over(pairs, p => s.Shift(p.Date, p.SwapTenor, p.Extrapolate)));
            check.Output(c, "shiftTimePeriod", Over(pairs, p => s.Shift(p.Time, p.SwapTenor, p.Extrapolate)));
            check.Output(c, "shiftPeriodTime", Over(pairs, p => s.Shift(p.Tenor, p.SwapLength, p.Extrapolate)));
            check.Output(c, "shiftDateTime", Over(pairs, p => s.Shift(p.Date, p.SwapLength, p.Extrapolate)));
            check.Output(c, "shiftTimeTime", Over(pairs, p => s.Shift(p.Time, p.SwapLength, p.Extrapolate)));
            check.Output(
                c,
                "smileSectionDatePeriod",
                Over(pairs, p => SmileSectionInputs.Summary(s.SmileSection(p.Date, p.SwapTenor), p.Strike)));
            check.Output(
                c,
                "smileSectionTimeTime",
                Over(pairs, p => SmileSectionInputs.Summary(s.SmileSection(p.Time, p.SwapLength), p.Strike)));
            check.Output(
                c, "smileVolatilityPeriodPeriod", Over(pairs, p => s.SmileSection(p.Tenor, p.SwapTenor).Volatility(p.Strike)));
            check.Output(
                c, "smileVolatilityTimePeriod", Over(pairs, p => s.SmileSection(p.Time, p.SwapTenor).Volatility(p.Strike)));
            check.Output(
                c, "smileVolatilityPeriodTime", Over(pairs, p => s.SmileSection(p.Tenor, p.SwapLength).Volatility(p.Strike)));
            check.Output(
                c, "smileVolatilityDateTime", Over(pairs, p => s.SmileSection(p.Date, p.SwapLength).Volatility(p.Strike)));
        }

        check.Complete();
    }

    // Each value computed when compared, so that a throw is checked.
    private static Func<object?>[] Over<T>(Pair[] pairs, Func<Pair, T> f) => [.. pairs.Select(p => (Func<object?>)(() => f(p)))];

    // An option-and-swap pair of the inputs.
    private sealed record Pair(
        Period Tenor, Date Date, double Time, Period SwapTenor, double SwapLength, double Strike, bool Extrapolate)
    {
        public static Pair Of(JsonElement p) => new(
            VolatilityInputs.TenorOf(p.GetProperty("optionTenor")),
            new Date(p.GetProperty("optionDate").GetInt32()),
            IndexInputs.Real(p.GetProperty("optionTime")),
            VolatilityInputs.TenorOf(p.GetProperty("swapTenor")),
            IndexInputs.Real(p.GetProperty("swapLength")),
            IndexInputs.Real(p.GetProperty("strike")),
            p.GetProperty("extrapolate").GetBoolean());
    }
}
