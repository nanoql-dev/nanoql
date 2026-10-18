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
/// The interpolated discount curve vectors, <c>tests/vectors/termstructures/yield/discountcurve.json</c>:
/// <see cref="InterpolatedDiscountCurve{TInterpolator}"/> on six interpolators and the log-mixed-linear-cubic ones (as
/// <see cref="BootstrapInputs"/> reads their specs), with and without jumps, the named curves built through their
/// classes (<see cref="DiscountCurve"/>, …) where a case names them, and its constructor's refusals: whether
/// each curve builds, its dates, times, data, discounts and nodes, its maximum date and its discounts at the case's
/// dates, one element per curve of the case.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscountCurveVectorTests
{
    [Fact]
    public void DiscountCurveVector()
    {
        var file = VectorFile.Load("termstructures/yield/discountcurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Date[] dates = c.DateInputs("dates");
            double[] dfs = c.Doubles("discounts");
            DayCounter dayCounter = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
            JsonElement calendarSpec = c.Input("calendar");
            Calendar? calendar =
                calendarSpec.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CalendarOf(calendarSpec);
            JsonElement jumpsSpec = c.Input("jumps");
            double[]? jumps = jumpsSpec.ValueKind == JsonValueKind.Null ? null : CouponInputs.Reals(jumpsSpec);
            Date[]? jumpDates = c.Input("jumpDates").ValueKind == JsonValueKind.Null ? null : c.DateInputs("jumpDates");
            JsonElement[] interpolators = [.. c.Input("interpolators").EnumerateArray()];
            string?[] classes = c.Inputs.TryGetProperty("classes", out JsonElement named)
                ? [.. named.EnumerateArray().Select(e => e.GetString())]
                : new string?[interpolators.Length];
            Date[] when = c.DateInputs("discountDates");
            bool[] flags = InterpolationInputs.Bools(c.Input("extrapolate"));

            var curves = new Built?[interpolators.Length];
            check.Output(c, "built", interpolators.Select((i, k) => (Func<object?>)(() =>
            {
                curves[k] = classes[k] is string cls
                    ? NamedOf(cls, i, dates, dfs, dayCounter, calendar, jumps, jumpDates)
                    : CurveOf(i, dates, dfs, dayCounter, calendar, jumps, jumpDates);
                return true;
            })).ToArray());
            check.Output(c, "dates", curves.Select(b => b is null ? [] : b.Dates.Cast<object?>().ToArray()).ToArray());
            check.Output(c, "times", curves.Select(b => b is null ? [] : b.Times.Cast<object?>().ToArray()).ToArray());
            check.Output(c, "data", curves.Select(b => b is null ? [] : b.Data.Cast<object?>().ToArray()).ToArray());
            check.Output(
                c, "discounts", curves.Select(b => b is null ? [] : b.Discounts.Cast<object?>().ToArray()).ToArray());
            check.Output(c, "nodes", curves.Select(b => b is null
                ? []
                : b.Nodes.Select(n => (object?)new object?[] { n.Key, n.Value }).ToArray()).ToArray());
            check.Output(c, "maxDate", curves.Select(b => (object?)b?.Curve.MaxDate).ToArray());
            check.Output(c, "discountAt", curves.Select(b => b is null
                ? []
                : when.Zip(flags).Select(p => (Func<object?>)(() => b.Curve.Discount(p.First, p.Second)))
                    .ToArray<object?>()).ToArray());
        }

        check.Complete();
    }

    private static Built CurveOf(
        JsonElement i,
        Date[] dates,
        double[] dfs,
        DayCounter dayCounter,
        Calendar? calendar,
        double[]? jumps,
        Date[]? jumpDates) =>
        i.GetProperty("type").GetString() switch
        {
            "LogLinear" => Build<LogLinear>(default, dates, dfs, dayCounter, calendar, jumps, jumpDates),
            "MonotonicLogCubic" =>
                Build<MonotonicLogCubic>(default, dates, dfs, dayCounter, calendar, jumps, jumpDates),
            "KrugerLog" => Build<KrugerLog>(default, dates, dfs, dayCounter, calendar, jumps, jumpDates),
            "LogCubic" => Build(BootstrapInputs.LogCubicOf(i), dates, dfs, dayCounter, calendar, jumps, jumpDates),
            "Cubic" => Build(BootstrapInputs.CubicOf(i), dates, dfs, dayCounter, calendar, jumps, jumpDates),
            "LogMixedLinearCubic" =>
                Build(BootstrapInputs.LogMixedLinearCubicOf(i), dates, dfs, dayCounter, calendar, jumps, jumpDates),
            var t => throw new InvalidOperationException($"unknown interpolator {t}"),
        };

    // The curve through its named class, which passes the spec's interpolator where it takes parameters and leaves a
    // stateless one to the class's default.
    private static Built NamedOf(
        string named,
        JsonElement i,
        Date[] dates,
        double[] dfs,
        DayCounter dayCounter,
        Calendar? calendar,
        double[]? jumps,
        Date[]? jumpDates) =>
        named switch
        {
            "DiscountCurve" => Of(new DiscountCurve(dates, dfs, dayCounter, calendar, jumps, jumpDates)),
            "LogCubicDiscountCurve" => Of(new LogCubicDiscountCurve(
                dates, dfs, dayCounter, calendar, jumps, jumpDates, BootstrapInputs.LogCubicOf(i))),
            "KrugerLogDiscountCurve" => Of(new KrugerLogDiscountCurve(dates, dfs, dayCounter, calendar, jumps, jumpDates)),
            "LogMixedLinearCubicDiscountCurve" => Of(new LogMixedLinearCubicDiscountCurve(
                dates, dfs, dayCounter, calendar, jumps, jumpDates, BootstrapInputs.LogMixedLinearCubicOf(i))),
            _ => throw new InvalidOperationException($"unknown named curve {named}"),
        };

    private static Built Build<TInterpolator>(
        TInterpolator interpolator,
        Date[] dates,
        double[] dfs,
        DayCounter dayCounter,
        Calendar? calendar,
        double[]? jumps,
        Date[]? jumpDates)
        where TInterpolator : struct, IInterpolator
    {
        return Of(new InterpolatedDiscountCurve<TInterpolator>(
            dates, dfs, dayCounter, calendar, jumps, jumpDates, interpolator));
    }

    private static Built Of<TInterpolator>(InterpolatedDiscountCurve<TInterpolator> curve)
        where TInterpolator : struct, IInterpolator =>
        new(curve, curve.Dates(), curve.Times(), curve.Data(), curve.Discounts(), curve.Nodes());

    private sealed record Built(
        YieldTermStructure Curve,
        ImmutableArray<Date> Dates,
        ImmutableArray<double> Times,
        ImmutableArray<double> Data,
        ImmutableArray<double> Discounts,
        ImmutableArray<KeyValuePair<Date, double>> Nodes);
}
