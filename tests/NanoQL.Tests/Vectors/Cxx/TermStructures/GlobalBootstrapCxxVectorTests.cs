// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's global bootstrap vectors, <c>tests/vectors/cxx/termstructures/globalbootstrap.json</c>: what the
/// wheel's <c>GlobalBootstrap</c> can't take, built as <c>tools/cxx/src/termstructures/globalbootstrap.cpp</c> builds
/// it: instrument weights, additional dates from offsets of the evaluation date, the harness's gradient penalty on
/// additional helpers, its initial-guess function, which counts its calls, and its traits without a global guess
/// (<see cref="DiscountWithoutGlobalGuess"/>). Each case's curve: whether it builds, its nodes, its maximum date, each
/// helper's and additional helper's quote error (null for one not alive at the reference date) and the guess
/// function's calls.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GlobalBootstrapCxxVectorTests
{
    [Fact]
    public void GlobalBootstrapCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/globalbootstrap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BootstrapHelper<YieldTermStructure>[] helpers = RateHelperInputs.HelpersOf(c.Input("helpers"), context);
            JsonElement spec = c.Input("bootstrap");
            BootstrapHelper<YieldTermStructure>[] additional =
                RateHelperInputs.HelpersOf(spec.GetProperty("additionalHelpers"), context);
            int guessCalls = 0;
            GlobalBootstrap bootstrap = BootstrapOf(spec, additional, context, () => ++guessCalls);
            BootstrapInputs.Built? built = null;
            check.Output(c, "built", () =>
            {
                built = CurveOf(c.Input("curve"), helpers, bootstrap, context);
                return true;
            });
            if (built is null)
            {
                continue;
            }

            YieldTermStructure curve = built.Curve;
            check.Output(c, "dates", built.Dates.Cast<object?>().ToArray());
            check.Output(c, "data", built.Data.Cast<object?>().ToArray());
            check.Output(c, "maxDate", curve.MaxDate);
            check.Output(c, "quoteErrors", QuoteErrors(helpers, curve, context));
            check.Output(c, "additionalQuoteErrors", QuoteErrors(additional, curve, context));
            check.Output(c, "guessCalls", guessCalls);
        }

        check.Complete();
    }

    // Each helper's quote error against the curve, null for one QuantLib doesn't bind, its pillar not after the
    // curve's reference date.
    private static Func<object?>[] QuoteErrors(
        BootstrapHelper<YieldTermStructure>[] helpers, YieldTermStructure curve, EvaluationContext context) =>
        [.. helpers.Select(h => (Func<object?>)(() => h.PillarDate is Date d && d > curve.ReferenceDate
            ? h.SetTermStructure(curve).QuoteError(context)
            : null))];

    private static GlobalBootstrap BootstrapOf(
        JsonElement spec, BootstrapHelper<YieldTermStructure>[] additional, EvaluationContext context, Action guessCalled)
    {
        JsonElement dates = spec.GetProperty("dates");
        Date[]? additionalDates = dates.ValueKind == JsonValueKind.Null
            ? null
            : [.. dates.GetProperty("offsets").EnumerateArray().Select(d => context.EvaluationDate + d.GetInt32())];
        JsonElement penalty = spec.GetProperty("penalty");
        JsonElement guess = spec.GetProperty("guess");
        return new GlobalBootstrap(
            CouponInputs.NullableReal(spec.GetProperty("accuracy")),
            instrumentWeights: CouponInputs.Reals(spec.GetProperty("weights")),
            additionalHelpers: additional,
            additionalDates: additionalDates,
            additionalPenalties: penalty.ValueKind == JsonValueKind.Null
                ? null
                : GradientPenalty(penalty.GetProperty("weight").GetDouble(), penalty.GetProperty("helpers").GetBoolean()),
            initialGuessFn: guess.ValueKind == JsonValueKind.Null
                ? null
                : LinearGuess(
                    guess.GetProperty("a").GetDouble(),
                    guess.GetProperty("b").GetDouble(),
                    guess.GetProperty("extra").GetInt32(),
                    guessCalled));
    }

    // The harness's penalty, QuantLib's testGlobalBootstrapInitialGuessFn's: the additional helpers' quote errors when
    // asked, then the weight times each change of the simple forward rates between nodes over its time step.
    private static Func<IBootstrapState, EvaluationContext, Vector> GradientPenalty(double w, bool withHelpers) =>
        (state, context) =>
        {
            ReadOnlySpan<double> times = state.Times;
            ReadOnlySpan<double> data = state.Data;
            int nInst = withHelpers ? state.AdditionalHelpers.Count : 0;
            var errors = new Vector(nInst + times.Length - 2);

            // instruments errors
            for (int i = 0; i < nInst; ++i)
            {
                errors[i] = state.AdditionalHelpers[i].QuoteError(context);
            }

            // gradient penalties
            var rates = new double[data.Length - 1];
            for (int i = 0; i < times.Length - 1; ++i)
            {
                rates[i] = ((data[i] / data[i + 1]) - 1.0) / (times[i + 1] - times[i]);
            }

            for (int i = 0; i < times.Length - 2; ++i)
            {
                errors[nInst + i] = w * (rates[i + 1] - rates[i]) / (times[i + 1] - times[i]);
            }

            return errors;
        };

    // The harness's guess function: a + b t at each node's time after the first, then extra values at the last's.
    private static Func<IBootstrapState, EvaluationContext, Vector> LinearGuess(
        double a, double b, int extra, Action called) =>
        (state, _) =>
        {
            called();
            int pillars = state.Times.Length - 1;
            var guess = new Vector(pillars + extra);
            for (int i = 0; i < guess.Size; ++i)
            {
                guess[i] = a + (b * state.Times[Math.Min(i + 1, pillars)]);
            }

            return guess;
        };

    private static BootstrapInputs.Built CurveOf(
        JsonElement spec,
        BootstrapHelper<YieldTermStructure>[] helpers,
        GlobalBootstrap bootstrap,
        EvaluationContext context) =>
        (spec.GetProperty("traits").GetString(), spec.GetProperty("interpolator").GetString()) switch
        {
            ("Discount", "LogLinear") => Build<Discount, LogLinear>(spec, helpers, bootstrap, context),
            ("Discount", "Linear") => Build<Discount, Linear>(spec, helpers, bootstrap, context),
            ("ZeroYield", "Linear") => Build<ZeroYield, Linear>(spec, helpers, bootstrap, context),
            ("ForwardRate", "BackwardFlat") => Build<ForwardRate, BackwardFlat>(spec, helpers, bootstrap, context),
            ("SimpleZeroYield", "Linear") => Build<SimpleZeroYield, Linear>(spec, helpers, bootstrap, context),
            ("DiscountWithoutGlobalGuess", "LogLinear") =>
                Build<DiscountWithoutGlobalGuess, LogLinear>(spec, helpers, bootstrap, context),
            var t => throw new InvalidOperationException($"unknown curve {t}"),
        };

    private static BootstrapInputs.Built Build<TTraits, TInterpolator>(
        JsonElement spec,
        BootstrapHelper<YieldTermStructure>[] helpers,
        GlobalBootstrap bootstrap,
        EvaluationContext context)
        where TTraits : struct, IYieldTraits
        where TInterpolator : struct, IInterpolator
    {
        var curve = new PiecewiseYieldCurve<TTraits, TInterpolator, GlobalBootstrap>(
            new Date(spec.GetProperty("referenceDate").GetInt32()),
            helpers,
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            context,
            bootstrap: bootstrap);
        return new BootstrapInputs.Built(curve, curve.Dates(), curve.Data(), curve.Times(), curve.Nodes());
    }

    /// <summary>
    /// The harness's traits: QuantLib's <see cref="Discount"/> without its global guess, so a global bootstrap takes
    /// each node's guess in turn, and <see cref="IBootstrapTraits{TTermStructure}.HasGlobalGuess"/> keeps its default.
    /// </summary>
    internal readonly struct DiscountWithoutGlobalGuess : IYieldTraits
    {
        public static int MaxIterations => Discount.MaxIterations;

        public static bool HasTransform => Discount.HasTransform;

        public static Date InitialDate(BootstrapState<YieldTermStructure> c) => Discount.InitialDate(c);

        public static double InitialValue(BootstrapState<YieldTermStructure> c) => Discount.InitialValue(c);

        public static double Guess(int i, BootstrapState<YieldTermStructure> c, bool validData, int firstAliveHelper) =>
            Discount.Guess(i, c, validData, firstAliveHelper);

        public static double MinValueAfter(
            int i, BootstrapState<YieldTermStructure> c, bool validData, int firstAliveHelper) =>
            Discount.MinValueAfter(i, c, validData, firstAliveHelper);

        public static double MaxValueAfter(
            int i, BootstrapState<YieldTermStructure> c, bool validData, int firstAliveHelper) =>
            Discount.MaxValueAfter(i, c, validData, firstAliveHelper);

        public static double TransformDirect(double x, int i, BootstrapState<YieldTermStructure> c) =>
            Discount.TransformDirect(x, i, c);

        public static double TransformInverse(double x, int i, BootstrapState<YieldTermStructure> c) =>
            Discount.TransformInverse(x, i, c);

        public static void UpdateGuess(double[] data, double value, int i) => Discount.UpdateGuess(data, value, i);

        public static double DiscountImpl(
            ReadOnlySpan<double> times, ReadOnlySpan<double> data, Interpolation interpolation, double t) =>
            Discount.DiscountImpl(times, data, interpolation, t);

        public static Date MaxDate(ReadOnlySpan<Date> dates, Date? maxDate) => Discount.MaxDate(dates, maxDate);
    }
}
