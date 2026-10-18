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
/// Builds the bootstrapped curves that the bootstrap vector files describe in their inputs, as their generators build
/// them (<c>tools/vectors/gen/termstructures/yield/piecewiseyieldcurve.py</c>'s functions), and checks a case's curves.
/// </summary>
/// <remarks>
/// <para>
/// A curve is <c>{"traits", "interpolator", "form", "dayCounter", "jumps", "jumpDates", "bootstrap"}</c>: the traits
/// <c>"Discount"</c>, <c>"ZeroYield"</c>, <c>"ForwardRate"</c> or <c>"SimpleZeroYield"</c>; <c>"form"</c>
/// <c>"referenceDate"</c> with a <c>"referenceDate"</c> for QuantLib's fixed reference date, or
/// <c>"settlementDays"</c> with <c>"settlementDays"</c> and <c>"calendar"</c> for its moving one, built by
/// <c>Create</c> in the case's context; null jumps or jump dates are none (the jumps then on QuantLib's default
/// dates). The bootstrap is <c>null</c> for <see cref="IterativeBootstrap"/>'s zero value, QuantLib's default, or
/// <c>{"accuracy", "minValue", "maxValue", "maxAttempts", "maxFactor", "minFactor", "dontThrow", "dontThrowSteps",
/// "maxEvaluations"}</c>, a null real QuantLib's <c>Null&lt;Real&gt;()</c>. An interpolator is <c>{"type": "Linear" |
/// "LogLinear" | "BackwardFlat" | "KrugerLog" | "MonotonicLogCubic"}</c>, or <c>{"type": "Cubic" | "LogCubic",
/// "derivativeApprox", "monotonic", "leftCondition", "leftConditionValue", "rightCondition",
/// "rightConditionValue"}</c>. The helpers are specs of <see cref="RateHelperInputs"/>.
/// </para>
/// <para>
/// A case of several curves on the same helpers (<c>"curves"</c>, with its <c>"discountDates"</c> and
/// <c>"extrapolate"</c> flags, one list per curve) gives each output as an array of one element per curve: a curve
/// QuantLib refuses has its throw in <c>"built"</c>, empty lists and a null maximum date.
/// </para>
/// </remarks>
internal static class BootstrapInputs
{
    /// <summary>The curve a spec describes, built on the helpers in a context.</summary>
    /// <param name="spec">The curve's spec.</param>
    /// <param name="helpers">The helpers.</param>
    /// <param name="context">The context the curve is built in.</param>
    /// <param name="ownLinear">
    /// Whether a linear interpolator is replaced by <see cref="OwnLinear"/>, a factory of one's own keeping
    /// <see cref="IInterpolator.CheckPoints"/>'s default.
    /// </param>
    /// <returns>The curve with its nodes.</returns>
    public static Built CurveOf(
        JsonElement spec,
        IReadOnlyList<BootstrapHelper<YieldTermStructure>> helpers,
        EvaluationContext context,
        bool ownLinear = false) =>
        spec.GetProperty("traits").GetString() switch
        {
            "Discount" => WithTraits<Discount>(spec, helpers, context, ownLinear),
            "ZeroYield" => WithTraits<ZeroYield>(spec, helpers, context, ownLinear),
            "ForwardRate" => WithTraits<ForwardRate>(spec, helpers, context, ownLinear),
            "SimpleZeroYield" => WithTraits<SimpleZeroYield>(spec, helpers, context, ownLinear),
            var t => throw new InvalidOperationException($"unknown traits {t}"),
        };

    /// <summary>The bootstrap a spec describes: QuantLib's default for <c>null</c>.</summary>
    /// <param name="spec">The spec, or JSON <c>null</c>.</param>
    /// <returns>The bootstrap.</returns>
    public static IterativeBootstrap BootstrapOf(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null
            ? default
            : new IterativeBootstrap(
                CouponInputs.NullableReal(spec.GetProperty("accuracy")),
                CouponInputs.NullableReal(spec.GetProperty("minValue")),
                CouponInputs.NullableReal(spec.GetProperty("maxValue")),
                spec.GetProperty("maxAttempts").GetInt32(),
                spec.GetProperty("maxFactor").GetDouble(),
                spec.GetProperty("minFactor").GetDouble(),
                spec.GetProperty("dontThrow").GetBoolean(),
                spec.GetProperty("dontThrowSteps").GetInt32(),
                spec.GetProperty("maxEvaluations").GetInt32());

    /// <summary>The cubic factory an interpolator spec of type <c>"Cubic"</c> describes.</summary>
    /// <param name="i">The spec.</param>
    /// <returns>The factory.</returns>
    public static Cubic CubicOf(JsonElement i) =>
        new(
            InterpolationInputs.DerivativeApprox(i),
            i.GetProperty("monotonic").GetBoolean(),
            InterpolationInputs.Condition(i, "leftCondition"),
            InterpolationInputs.Number(i.GetProperty("leftConditionValue")),
            InterpolationInputs.Condition(i, "rightCondition"),
            InterpolationInputs.Number(i.GetProperty("rightConditionValue")));

    /// <summary>The log-cubic factory an interpolator spec of type <c>"LogCubic"</c> describes.</summary>
    /// <param name="i">The spec.</param>
    /// <returns>The factory.</returns>
    public static LogCubic LogCubicOf(JsonElement i) =>
        new(
            InterpolationInputs.DerivativeApprox(i),
            i.GetProperty("monotonic").GetBoolean(),
            InterpolationInputs.Condition(i, "leftCondition"),
            InterpolationInputs.Number(i.GetProperty("leftConditionValue")),
            InterpolationInputs.Condition(i, "rightCondition"),
            InterpolationInputs.Number(i.GetProperty("rightConditionValue")));

    /// <summary>
    /// Compares the curves of a case of several curves on the same helpers: whether each builds, its nodes' dates and
    /// data, its maximum date, its discounts and, when the case has them, each helper's quote error, read from a copy
    /// of the helper bound to the built curve.
    /// </summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="helpers">The helpers the curves are built on.</param>
    /// <param name="context">The context the curves are built in.</param>
    /// <param name="ownLinear">Whether a linear interpolator is replaced by <see cref="OwnLinear"/>.</param>
    public static void CheckCurves(
        VectorAssert check,
        VectorCase c,
        BootstrapHelper<YieldTermStructure>[] helpers,
        EvaluationContext context,
        bool ownLinear = false)
    {
        JsonElement[] specs = [.. c.Input("curves").EnumerateArray()];
        JsonElement[] dates = [.. c.Input("discountDates").EnumerateArray()];
        JsonElement[] flags = [.. c.Input("extrapolate").EnumerateArray()];
        var curves = new Built?[specs.Length];
        check.Output(c, "built", specs.Select((s, k) => (Func<object?>)(() =>
        {
            curves[k] = CurveOf(s, helpers, context, ownLinear);
            return true;
        })).ToArray());
        check.Output(c, "dates", curves.Select(b => b is null ? [] : b.Dates.Cast<object?>().ToArray()).ToArray());
        check.Output(c, "data", curves.Select(b => b is null ? [] : b.Data.Cast<object?>().ToArray()).ToArray());
        check.Output(c, "maxDate", curves.Select(b => (object?)b?.Curve.MaxDate).ToArray());
        check.Output(c, "discounts", curves.Select((b, k) => b is null
            ? []
            : dates[k].EnumerateArray().Zip(flags[k].EnumerateArray())
                .Select(p => (Func<object?>)(() =>
                    b.Curve.Discount(new Date(p.First.GetInt32()), p.Second.GetBoolean())))
                .ToArray<object?>()).ToArray());
        if (c.Expected.TryGetProperty("quoteErrors", out _))
        {
            check.Output(c, "quoteErrors", curves.Select(b => b is null
                ? []
                : helpers.Select(h => (Func<object?>)(() => h.SetTermStructure(b.Curve).QuoteError(context)))
                    .ToArray<object?>()).ToArray());
        }
    }

    private static Built WithTraits<TTraits>(
        JsonElement spec,
        IReadOnlyList<BootstrapHelper<YieldTermStructure>> helpers,
        EvaluationContext context,
        bool ownLinear)
        where TTraits : struct, IYieldTraits
    {
        JsonElement i = spec.GetProperty("interpolator");
        return i.GetProperty("type").GetString() switch
        {
            "Linear" when ownLinear => Build<TTraits, OwnLinear>(spec, default, helpers, context),
            "Linear" => Build<TTraits, Linear>(spec, default, helpers, context),
            "LogLinear" => Build<TTraits, LogLinear>(spec, default, helpers, context),
            "BackwardFlat" => Build<TTraits, BackwardFlat>(spec, default, helpers, context),
            "KrugerLog" => Build<TTraits, KrugerLog>(spec, default, helpers, context),
            "MonotonicLogCubic" => Build<TTraits, MonotonicLogCubic>(spec, default, helpers, context),
            "Cubic" => Build<TTraits, Cubic>(spec, CubicOf(i), helpers, context),
            "LogCubic" => Build<TTraits, LogCubic>(spec, LogCubicOf(i), helpers, context),
            var t => throw new InvalidOperationException($"unknown interpolator {t}"),
        };
    }

    private static Built Build<TTraits, TInterpolator>(
        JsonElement spec,
        TInterpolator interpolator,
        IReadOnlyList<BootstrapHelper<YieldTermStructure>> helpers,
        EvaluationContext context)
        where TTraits : struct, IYieldTraits
        where TInterpolator : struct, IInterpolator
    {
        DayCounter dayCounter = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        JsonElement jumpsSpec = spec.GetProperty("jumps");
        JsonElement jumpDatesSpec = spec.GetProperty("jumpDates");
        double[]? jumps = jumpsSpec.ValueKind == JsonValueKind.Null ? null : CouponInputs.Reals(jumpsSpec);
        Date[]? jumpDates = jumpDatesSpec.ValueKind == JsonValueKind.Null
            ? null
            : [.. jumpDatesSpec.EnumerateArray().Select(d => new Date(d.GetInt32()))];
        IterativeBootstrap bootstrap = BootstrapOf(spec.GetProperty("bootstrap"));
        PiecewiseYieldCurve<TTraits, TInterpolator, IterativeBootstrap> curve =
            spec.GetProperty("form").GetString() == "referenceDate"
                ? new(
                    new Date(spec.GetProperty("referenceDate").GetInt32()),
                    helpers,
                    dayCounter,
                    context,
                    jumps,
                    jumpDates,
                    interpolator,
                    bootstrap)
                : PiecewiseYieldCurve<TTraits, TInterpolator, IterativeBootstrap>.Create(
                    spec.GetProperty("settlementDays").GetInt32(),
                    CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
                    helpers,
                    dayCounter,
                    context,
                    jumps,
                    jumpDates,
                    interpolator,
                    bootstrap);
        return new Built(curve, curve.Dates(), curve.Data(), curve.Times(), curve.Nodes());
    }

    /// <summary>
    /// A linear-interpolation factory of one's own, as a user writes one: it implements <see cref="IInterpolator"/> by
    /// delegating to <see cref="Linear"/> and keeps <see cref="IInterpolator.CheckPoints"/>'s default, which refuses
    /// nothing.
    /// </summary>
    internal readonly struct OwnLinear : IInterpolator
    {
        /// <summary>Gets a value indicating whether the interpolation is global: <see langword="false"/>.</summary>
        public static bool Global => false;

        /// <summary>Gets the fewest points the interpolation takes: 2.</summary>
        public static int RequiredPoints => 2;

        /// <summary>Linear interpolation over the points.</summary>
        /// <param name="x">The x values.</param>
        /// <param name="y">The y values.</param>
        /// <returns>The interpolation.</returns>
        public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y) =>
            default(Linear).Interpolate(x, y);
    }

    /// <summary>A bootstrapped curve with its nodes, read through its closed generic type.</summary>
    /// <param name="Curve">The curve.</param>
    /// <param name="Dates">Its nodes' dates.</param>
    /// <param name="Data">Its nodes' data.</param>
    /// <param name="Times">Its nodes' times.</param>
    /// <param name="Nodes">Its nodes.</param>
    internal sealed record Built(
        YieldTermStructure Curve,
        ImmutableArray<Date> Dates,
        ImmutableArray<double> Data,
        ImmutableArray<double> Times,
        ImmutableArray<KeyValuePair<Date, double>> Nodes);
}
