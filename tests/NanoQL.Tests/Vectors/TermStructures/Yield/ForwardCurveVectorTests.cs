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
/// The interpolated forward curve vectors, <c>tests/vectors/termstructures/yield/forwardcurve.json</c>:
/// <see cref="InterpolatedForwardCurve{TInterpolator}"/> on four interpolators (as <see cref="BootstrapInputs"/> reads
/// their specs), each built through its named class (<see cref="ForwardCurve"/>, <see cref="LinearForwardCurve"/>,
/// <see cref="CubicForwardCurve"/>) where the case names one, with and without jumps, and its constructor's refusals:
/// whether each curve builds, its dates, times, forwards (its data, by both inspectors) and nodes, its maximum date and
/// its zero rates, instantaneous forward rates and discounts at the case's dates, one element per curve of the case.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ForwardCurveVectorTests
{
    [Fact]
    public void ForwardCurveVector()
    {
        var file = VectorFile.Load("termstructures/yield/forwardcurve");
        var check = new VectorAssert(file);
        var resultDayCounter = new Actual365Fixed();
        foreach (VectorCase c in file.Cases)
        {
            Date[] dates = c.DateInputs("dates");
            double[] forwards = c.Doubles("forwards");
            DayCounter dayCounter = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
            JsonElement calendarSpec = c.Input("calendar");
            Calendar? calendar =
                calendarSpec.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CalendarOf(calendarSpec);
            JsonElement jumpsSpec = c.Input("jumps");
            double[]? jumps = jumpsSpec.ValueKind == JsonValueKind.Null ? null : CouponInputs.Reals(jumpsSpec);
            Date[]? jumpDates = c.Input("jumpDates").ValueKind == JsonValueKind.Null ? null : c.DateInputs("jumpDates");
            JsonElement[] interpolators = [.. c.Input("interpolators").EnumerateArray()];
            JsonElement[] classes = [.. c.Input("classes").EnumerateArray()];
            Date[] when = c.DateInputs("discountDates");
            bool[] flags = InterpolationInputs.Bools(c.Input("extrapolate"));

            var curves = new Built?[interpolators.Length];
            check.Output(c, "built", interpolators.Select((i, k) => (Func<object?>)(() =>
            {
                curves[k] = CurveOf(i, classes[k].GetString(), dates, forwards, dayCounter, calendar, jumps, jumpDates);
                return true;
            })).ToArray());
            check.Output(c, "dates", curves.Select(b => b is null ? [] : b.Dates.Cast<object?>().ToArray()).ToArray());
            check.Output(c, "times", curves.Select(b => b is null ? [] : b.Times.Cast<object?>().ToArray()).ToArray());
            check.Output(c, "forwards", curves.Select(b => b is null ? [] : Forwards(b)).ToArray());
            check.Output(c, "nodes", curves.Select(b => b is null
                ? []
                : b.Nodes.Select(n => (object?)new object?[] { n.Key, n.Value }).ToArray()).ToArray());
            check.Output(c, "maxDate", curves.Select(b => (object?)b?.Curve.MaxDate).ToArray());
            check.Output(c, "zeroRateAt", curves.Select(b => b is null
                ? []
                : when.Zip(flags).Select(p => (Func<object?>)(() => b.Curve.ZeroRate(
                        p.First, resultDayCounter, Compounding.Continuous, Frequency.Annual, p.Second).Rate))
                    .ToArray<object?>()).ToArray());
            check.Output(c, "forwardAt", curves.Select(b => b is null
                ? []
                : when.Zip(flags).Select(p => (Func<object?>)(() => b.Curve.ForwardRate(
                        p.First, p.First, resultDayCounter, Compounding.Continuous, Frequency.Annual, p.Second).Rate))
                    .ToArray<object?>()).ToArray());
            check.Output(c, "discountAt", curves.Select(b => b is null
                ? []
                : when.Zip(flags).Select(p => (Func<object?>)(() => b.Curve.Discount(p.First, p.Second)))
                    .ToArray<object?>()).ToArray());
        }

        check.Complete();
    }

    // The forwards, by both inspectors, which must agree.
    private static object?[] Forwards(Built b)
    {
        Assert.Equal(b.Data, b.Forwards);
        return b.Forwards.Cast<object?>().ToArray();
    }

    // The curve through the case's named class, or the generic where it names none.
    private static Built CurveOf(
        JsonElement i,
        string? named,
        Date[] dates,
        double[] forwards,
        DayCounter dayCounter,
        Calendar? calendar,
        double[]? jumps,
        Date[]? jumpDates) =>
        (i.GetProperty("type").GetString(), named) switch
        {
            ("BackwardFlat", "ForwardCurve") => Of(new ForwardCurve(dates, forwards, dayCounter, calendar, jumps, jumpDates)),
            ("Linear", "LinearForwardCurve") =>
                Of(new LinearForwardCurve(dates, forwards, dayCounter, calendar, jumps, jumpDates)),
            ("Cubic", "CubicForwardCurve") => Of(new CubicForwardCurve(
                dates, forwards, dayCounter, calendar, jumps, jumpDates, BootstrapInputs.CubicOf(i))),
            ("Cubic", null) => Of(new InterpolatedForwardCurve<Cubic>(
                dates, forwards, dayCounter, calendar, jumps, jumpDates, BootstrapInputs.CubicOf(i))),
            var (t, k) => throw new InvalidOperationException($"unknown interpolator {t} through {k}"),
        };

    private static Built Of<TInterpolator>(InterpolatedForwardCurve<TInterpolator> curve)
        where TInterpolator : struct, IInterpolator =>
        new(curve, curve.Dates(), curve.Times(), curve.Data(), curve.Forwards(), curve.Nodes());

    private sealed record Built(
        YieldTermStructure Curve,
        ImmutableArray<Date> Dates,
        ImmutableArray<double> Times,
        ImmutableArray<double> Data,
        ImmutableArray<double> Forwards,
        ImmutableArray<KeyValuePair<Date, double>> Nodes);
}
