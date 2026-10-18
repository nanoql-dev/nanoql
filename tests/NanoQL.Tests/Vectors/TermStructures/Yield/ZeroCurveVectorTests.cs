// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The interpolated zero curve vectors, <c>tests/vectors/termstructures/yield/zerocurve.json</c>:
/// <see cref="InterpolatedZeroCurve{TInterpolator}"/> on five interpolators (as <see cref="BootstrapInputs"/> reads
/// their specs), each built through its named class (<see cref="ZeroCurve"/>, <see cref="CubicZeroCurve"/>) where the
/// case names one, on rates of three compoundings, with and without jumps, and its constructor's refusals: whether each
/// curve builds, its dates, times, data, zero rates and nodes, its maximum date and its zero rates and discounts at the
/// case's dates, one element per curve of the case.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ZeroCurveVectorTests
{
    [Fact]
    public void ZeroCurveVector()
    {
        var file = VectorFile.Load("termstructures/yield/zerocurve");
        var check = new VectorAssert(file);
        var resultDayCounter = new Actual365Fixed();
        foreach (VectorCase c in file.Cases)
        {
            Inputs inputs = Inputs.Of(c);
            var compounding = (Compounding)c.Int("compounding");
            var frequency = (Frequency)c.Int("frequency");
            JsonElement[] interpolators = [.. c.Input("interpolators").EnumerateArray()];
            JsonElement[] classes = [.. c.Input("classes").EnumerateArray()];
            Date[] when = c.DateInputs("discountDates");
            bool[] flags = InterpolationInputs.Bools(c.Input("extrapolate"));

            var curves = new Built?[interpolators.Length];
            check.Output(c, "built", interpolators.Select((i, k) => (Func<object?>)(() =>
            {
                curves[k] = CurveOf(i, classes[k].GetString(), inputs, compounding, frequency);
                return true;
            })).ToArray());
            check.Output(c, "dates", curves.Select(b => b is null ? [] : b.Dates.Cast<object?>().ToArray()).ToArray());
            check.Output(c, "times", curves.Select(b => b is null ? [] : b.Times.Cast<object?>().ToArray()).ToArray());
            check.Output(c, "data", curves.Select(b => b is null ? [] : b.Data.Cast<object?>().ToArray()).ToArray());
            check.Output(
                c, "zeroRates", curves.Select(b => b is null ? [] : b.ZeroRates.Cast<object?>().ToArray()).ToArray());
            check.Output(c, "nodes", curves.Select(b => b is null
                ? []
                : b.Nodes.Select(n => (object?)new object?[] { n.Key, n.Value }).ToArray()).ToArray());
            check.Output(c, "maxDate", curves.Select(b => (object?)b?.Curve.MaxDate).ToArray());
            check.Output(c, "zeroRateAt", curves.Select(b => b is null
                ? []
                : when.Zip(flags).Select(p => (Func<object?>)(() => b.Curve.ZeroRate(
                        p.First, resultDayCounter, Compounding.Continuous, Frequency.Annual, p.Second).Rate))
                    .ToArray<object?>()).ToArray());
            check.Output(c, "discountAt", curves.Select(b => b is null
                ? []
                : when.Zip(flags).Select(p => (Func<object?>)(() => b.Curve.Discount(p.First, p.Second)))
                    .ToArray<object?>()).ToArray());
        }

        check.Complete();
    }

    // The curve through the case's named class, or the generic where it names none.
    private static Built CurveOf(
        JsonElement i, string? named, Inputs n, Compounding compounding, Frequency frequency) =>
        (i.GetProperty("type").GetString(), named) switch
        {
            ("Linear", "ZeroCurve") => Of(new ZeroCurve(
                n.Dates, n.Rates, n.DayCounter, n.Calendar, n.Jumps, n.JumpDates, compounding: compounding, frequency: frequency)),
            ("Linear", null) => Of(new InterpolatedZeroCurve<Linear>(
                n.Dates, n.Rates, n.DayCounter, n.Calendar, n.Jumps, n.JumpDates, default, compounding, frequency)),
            ("Cubic", "CubicZeroCurve") => Of(new CubicZeroCurve(
                n.Dates, n.Rates, n.DayCounter, n.Calendar, n.Jumps, n.JumpDates, BootstrapInputs.CubicOf(i), compounding, frequency)),
            ("Cubic", null) => Of(new InterpolatedZeroCurve<Cubic>(
                n.Dates, n.Rates, n.DayCounter, n.Calendar, n.Jumps, n.JumpDates, BootstrapInputs.CubicOf(i), compounding, frequency)),
            ("LogLinear", null) => Of(new InterpolatedZeroCurve<LogLinear>(
                n.Dates, n.Rates, n.DayCounter, n.Calendar, n.Jumps, n.JumpDates, default, compounding, frequency)),
            var (t, k) => throw new InvalidOperationException($"unknown interpolator {t} through {k}"),
        };

    private static Built Of<TInterpolator>(InterpolatedZeroCurve<TInterpolator> curve)
        where TInterpolator : struct, IInterpolator =>
        new(curve, curve.Dates(), curve.Times(), curve.Data(), curve.ZeroRates(), curve.Nodes());

    // A case's nodes, conventions and jumps.
    private sealed record Inputs(
        Date[] Dates, double[] Rates, DayCounter DayCounter, Calendar? Calendar, double[]? Jumps, Date[]? JumpDates)
    {
        public static Inputs Of(VectorCase c)
        {
            JsonElement calendarSpec = c.Input("calendar");
            JsonElement jumpsSpec = c.Input("jumps");
            return new Inputs(
                c.DateInputs("dates"),
                c.Doubles("rates"),
                CashFlowInputs.DayCounterOf(c.Input("dayCounter")),
                calendarSpec.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CalendarOf(calendarSpec),
                jumpsSpec.ValueKind == JsonValueKind.Null ? null : CouponInputs.Reals(jumpsSpec),
                c.Input("jumpDates").ValueKind == JsonValueKind.Null ? null : c.DateInputs("jumpDates"));
        }
    }

    private sealed record Built(
        YieldTermStructure Curve,
        ImmutableArray<Date> Dates,
        ImmutableArray<double> Times,
        ImmutableArray<double> Data,
        ImmutableArray<double> ZeroRates,
        ImmutableArray<KeyValuePair<Date, double>> Nodes);
}
