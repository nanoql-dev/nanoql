// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the globally bootstrapped curves that the global bootstrap's vector files describe in their inputs, as their
/// generators build them (<c>tools/vectors/gen/termstructures/globalbootstrap.py</c>'s functions).
/// </summary>
/// <remarks>
/// A curve is a curve spec of <see cref="BootstrapInputs"/> whose <c>"bootstrap"</c> is <c>null</c> for
/// <see cref="GlobalBootstrap"/>'s zero value, QuantLib's default, or <c>{"accuracy", "optimizer", "endCriteria",
/// "additionalHelpers", "additionalDates", "penalty", "initialGuess"}</c>: an optimizer <c>{"type":
/// "LevenbergMarquardt", "epsfcn", "xtol", "gtol"}</c> or <c>null</c>, end criteria <c>{"maxIterations",
/// "maxStationaryStateIterations", "rootEpsilon", "functionEpsilon", "gradientNormEpsilon"}</c> or <c>null</c>, the
/// additional helpers' specs (<see cref="RateHelperInputs"/>) and dates, the penalty <c>"wheel"</c>
/// (<see cref="WheelPenalty"/>) or <c>null</c>, and the initial guess's values or <c>null</c>. A spec with a
/// <c>"class"</c> is built through that named class, its <c>"interpolator"</c> <c>null</c> for the class's default.
/// </remarks>
internal static class GlobalBootstrapInputs
{
    /// <summary>The globally bootstrapped curve a spec describes, built on the helpers in a context.</summary>
    /// <param name="spec">The curve's spec.</param>
    /// <param name="helpers">The helpers.</param>
    /// <param name="context">The context the curve is built in.</param>
    /// <returns>The curve with its nodes.</returns>
    public static BootstrapInputs.Built CurveOf(
        JsonElement spec, IReadOnlyList<BootstrapHelper<YieldTermStructure>> helpers, EvaluationContext context)
    {
        if (spec.TryGetProperty("class", out JsonElement named))
        {
            return NamedCurveOf(named.GetString()!, spec, helpers, context);
        }

        return spec.GetProperty("traits").GetString() switch
        {
            "Discount" => WithTraits<Discount>(spec, helpers, context),
            "ZeroYield" => WithTraits<ZeroYield>(spec, helpers, context),
            "ForwardRate" => WithTraits<ForwardRate>(spec, helpers, context),
            "SimpleZeroYield" => WithTraits<SimpleZeroYield>(spec, helpers, context),
            var t => throw new InvalidOperationException($"unknown traits {t}"),
        };
    }

    /// <summary>The bootstrap a spec describes: QuantLib's default for <c>null</c>.</summary>
    /// <param name="spec">The spec, or JSON <c>null</c>.</param>
    /// <param name="context">The context the additional helpers are built in.</param>
    /// <returns>The bootstrap.</returns>
    public static GlobalBootstrap BootstrapOf(JsonElement spec, EvaluationContext context)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return default;
        }

        JsonElement o = spec.GetProperty("optimizer");
        OptimizationMethod? optimizer = o.ValueKind == JsonValueKind.Null
            ? null
            : new LevenbergMarquardt(
                o.GetProperty("epsfcn").GetDouble(), o.GetProperty("xtol").GetDouble(), o.GetProperty("gtol").GetDouble());
        JsonElement e = spec.GetProperty("endCriteria");
        EndCriteria? endCriteria = e.ValueKind == JsonValueKind.Null
            ? null
            : new EndCriteria(
                e.GetProperty("maxIterations").GetInt32(),
                e.GetProperty("maxStationaryStateIterations").GetInt32(),
                e.GetProperty("rootEpsilon").GetDouble(),
                e.GetProperty("functionEpsilon").GetDouble(),
                e.GetProperty("gradientNormEpsilon").GetDouble());
        BootstrapHelper<YieldTermStructure>[] additionalHelpers =
            RateHelperInputs.HelpersOf(spec.GetProperty("additionalHelpers"), context);
        Date[] additionalDates = [.. spec.GetProperty("additionalDates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
        Func<IBootstrapState, EvaluationContext, Vector>? penalties = spec.GetProperty("penalty").GetString() switch
        {
            null => null,
            "wheel" => WheelPenalty,
            var p => throw new InvalidOperationException($"unknown penalty {p}"),
        };
        JsonElement g = spec.GetProperty("initialGuess");
        double[]? guess = g.ValueKind == JsonValueKind.Null ? null : CouponInputs.Reals(g);
        return new GlobalBootstrap(
            CouponInputs.NullableReal(spec.GetProperty("accuracy")),
            optimizer,
            endCriteria,
            additionalHelpers: additionalHelpers,
            additionalDates: additionalDates,
            additionalPenalties: penalties,
            initialGuessFn: guess is null ? null : (_, _) => new Vector(guess));
    }

    /// <summary>
    /// QuantLib-Python's penalty (its <c>AdditionalErrors</c>): the implied quotes of the additional helpers between
    /// the first and the last, each against the line between those two's.
    /// </summary>
    /// <param name="state">The working state of the curve under construction.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The errors, two fewer than the additional helpers.</returns>
    public static Vector WheelPenalty(IBootstrapState state, EvaluationContext context)
    {
        IReadOnlyList<IBootstrapHelper> h = state.AdditionalHelpers;
        var errors = new Vector(h.Count - 2);
        double a = h[0].ImpliedQuote(context);
        double b = h[^1].ImpliedQuote(context);
        for (int k = 0; k < errors.Size; ++k)
        {
            errors[k] = ((((double)(errors.Size - k) * a) + ((double)(1 + k) * b)) / (double)(errors.Size + 1))
                - h[1 + k].ImpliedQuote(context);
        }

        return errors;
    }

    private static BootstrapInputs.Built WithTraits<TTraits>(
        JsonElement spec, IReadOnlyList<BootstrapHelper<YieldTermStructure>> helpers, EvaluationContext context)
        where TTraits : struct, IYieldTraits
    {
        JsonElement i = spec.GetProperty("interpolator");
        return i.GetProperty("type").GetString() switch
        {
            "Linear" => Build<TTraits, Linear>(spec, default, helpers, context),
            "LogLinear" => Build<TTraits, LogLinear>(spec, default, helpers, context),
            "BackwardFlat" => Build<TTraits, BackwardFlat>(spec, default, helpers, context),
            "KrugerLog" => Build<TTraits, KrugerLog>(spec, default, helpers, context),
            "Cubic" => Build<TTraits, Cubic>(spec, BootstrapInputs.CubicOf(i), helpers, context),
            "LogCubic" => Build<TTraits, LogCubic>(spec, BootstrapInputs.LogCubicOf(i), helpers, context),
            var t => throw new InvalidOperationException($"unknown interpolator {t}"),
        };
    }

    private static BootstrapInputs.Built Build<TTraits, TInterpolator>(
        JsonElement spec,
        TInterpolator interpolator,
        IReadOnlyList<BootstrapHelper<YieldTermStructure>> helpers,
        EvaluationContext context)
        where TTraits : struct, IYieldTraits
        where TInterpolator : struct, IInterpolator
    {
        DayCounter dayCounter = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        GlobalBootstrap bootstrap = BootstrapOf(spec.GetProperty("bootstrap"), context);
        return Of(Form(
            spec,
            d => new PiecewiseYieldCurve<TTraits, TInterpolator, GlobalBootstrap>(
                d, helpers, dayCounter, context, i: interpolator, bootstrap: bootstrap),
            (n, cal) => PiecewiseYieldCurve<TTraits, TInterpolator, GlobalBootstrap>.Create(
                n, cal, helpers, dayCounter, context, i: interpolator, bootstrap: bootstrap)));
    }

    // The named class a spec names, in its form.
    private static BootstrapInputs.Built NamedCurveOf(
        string name, JsonElement spec, IReadOnlyList<BootstrapHelper<YieldTermStructure>> helpers, EvaluationContext context)
    {
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        GlobalBootstrap b = BootstrapOf(spec.GetProperty("bootstrap"), context);
        JsonElement i = spec.GetProperty("interpolator");
        return name switch
        {
            "GlobalLinearSimpleZeroCurve" => Of(Form(
                spec,
                d => new GlobalLinearSimpleZeroCurve(d, helpers, dc, context, bootstrap: b),
                (n, cal) => GlobalLinearSimpleZeroCurve.Create(n, cal, helpers, dc, context, bootstrap: b))),
            "GlobalPiecewiseFlatForward" => Of(Form(
                spec,
                d => new GlobalPiecewiseFlatForward(d, helpers, dc, context, bootstrap: b),
                (n, cal) => GlobalPiecewiseFlatForward.Create(n, cal, helpers, dc, context, bootstrap: b))),
            "GlobalPiecewiseLogLinearDiscount" => Of(Form(
                spec,
                d => new GlobalPiecewiseLogLinearDiscount(d, helpers, dc, context, bootstrap: b),
                (n, cal) => GlobalPiecewiseLogLinearDiscount.Create(n, cal, helpers, dc, context, bootstrap: b))),
            "GlobalPiecewiseLinearForward" => Of(Form(
                spec,
                d => new GlobalPiecewiseLinearForward(d, helpers, dc, context, bootstrap: b),
                (n, cal) => GlobalPiecewiseLinearForward.Create(n, cal, helpers, dc, context, bootstrap: b))),
            "GlobalPiecewiseLinearZero" => Of(Form(
                spec,
                d => new GlobalPiecewiseLinearZero(d, helpers, dc, context, bootstrap: b),
                (n, cal) => GlobalPiecewiseLinearZero.Create(n, cal, helpers, dc, context, bootstrap: b))),
            "GlobalPiecewiseCubicZero" => Of(Form(
                spec,
                d => new GlobalPiecewiseCubicZero(d, helpers, dc, context, bootstrap: b),
                (n, cal) => GlobalPiecewiseCubicZero.Create(n, cal, helpers, dc, context, bootstrap: b))),
            "GlobalPiecewiseLogCubicDiscount" => Of(Form(
                spec,
                d => new GlobalPiecewiseLogCubicDiscount(
                    d, helpers, dc, context, i: BootstrapInputs.LogCubicOf(i), bootstrap: b),
                (n, cal) => GlobalPiecewiseLogCubicDiscount.Create(
                    n, cal, helpers, dc, context, i: BootstrapInputs.LogCubicOf(i), bootstrap: b))),
            "GlobalPiecewiseSplineCubicDiscount" => Of(Form(
                spec,
                d => new GlobalPiecewiseSplineCubicDiscount(d, helpers, dc, context, bootstrap: b),
                (n, cal) => GlobalPiecewiseSplineCubicDiscount.Create(n, cal, helpers, dc, context, bootstrap: b))),
            "GlobalPiecewiseKrugerLogDiscount" => Of(Form(
                spec,
                d => new GlobalPiecewiseKrugerLogDiscount(d, helpers, dc, context, bootstrap: b),
                (n, cal) => GlobalPiecewiseKrugerLogDiscount.Create(n, cal, helpers, dc, context, bootstrap: b))),
            "GlobalPiecewiseLogMixedLinearCubicDiscount" => Of(Form(
                spec,
                d => new GlobalPiecewiseLogMixedLinearCubicDiscount(
                    d, helpers, dc, context, i: BootstrapInputs.LogMixedLinearCubicOf(i), bootstrap: b),
                (n, cal) => GlobalPiecewiseLogMixedLinearCubicDiscount.Create(
                    n, cal, helpers, dc, context, i: BootstrapInputs.LogMixedLinearCubicOf(i), bootstrap: b))),
            _ => throw new InvalidOperationException($"unknown named class {name}"),
        };
    }

    // The curve in the spec's form: a fixed reference date, or settlement days and a calendar.
    private static T Form<T>(JsonElement spec, Func<Date, T> byReferenceDate, Func<int, Calendar, T> bySettlementDays) =>
        spec.GetProperty("form").GetString() == "referenceDate"
            ? byReferenceDate(new Date(spec.GetProperty("referenceDate").GetInt32()))
            : bySettlementDays(
                spec.GetProperty("settlementDays").GetInt32(), CashFlowInputs.CalendarOf(spec.GetProperty("calendar")));

    private static BootstrapInputs.Built Of<TTraits, TInterpolator>(
        PiecewiseYieldCurve<TTraits, TInterpolator, GlobalBootstrap> curve)
        where TTraits : struct, IYieldTraits
        where TInterpolator : struct, IInterpolator =>
        new(curve, curve.Dates(), curve.Data(), curve.Times(), curve.Nodes());
}
