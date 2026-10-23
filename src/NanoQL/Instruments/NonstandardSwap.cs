// SPDX-FileCopyrightText: Copyright (C) 2013, 2016 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/nonstandardswap.hpp ql/instruments/nonstandardswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A vanilla swap, but possibly with period-dependent nominals, fixed rates, gearings and spreads.
/// </summary>
/// <remarks>
/// <para>
/// The constructors build the legs, as QuantLib's do: a fixed leg, and an Ibor leg with the index's default pricer,
/// which reads the context's at-par switch, so they take the context; or, for an overnight index, a compounded
/// overnight leg. A zero gearing becomes QuantLib's machine epsilon, so that the floating leg is built of floating
/// coupons. With the capital exchanges, a redemption follows each coupon whose nominal changes after it, and the last,
/// the nominals and fixed rates repeated (the rate as 0) beside each, as QuantLib's inspectors give them. The fixed leg
/// is paid by a payer swap.
/// </para>
/// <para>
/// Converted from a fixed-versus-floating swap, it keeps an overnight-indexed swap's observation conventions, as
/// QuantLib's does, and its rounding precision, which QuantLib's conversion drops, so a rounded swap's coupons would
/// differ from the source's: the converted swap is worth the source's, QuantLib's own identity
/// (<c>testNonstandardSwapConversionPreservesObservationConventions</c>).
/// </para>
/// </remarks>
public sealed class NonstandardSwap : Swap
{
    private readonly bool singleSpreadAndGearing;

    /// <summary>Initializes a new instance of the <see cref="NonstandardSwap"/> class from a fixed-versus-floating
    /// swap: its constant nominal, rate and spread repeated on every coupon, a unit gearing.</summary>
    /// <param name="fromVanilla">The swap: a vanilla or an overnight-indexed swap.</param>
    /// <param name="context">The evaluation context, whose at-par switch the Ibor leg reads.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="NanoQLException">The swap's nominal isn't constant.</exception>
    public NonstandardSwap(FixedVsFloatingSwap fromVanilla, EvaluationContext context)
        : this(Terms.OfVanilla(fromVanilla, context))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NonstandardSwap"/> class with one gearing and one spread.
    /// </summary>
    /// <param name="type">A payer swap pays the fixed leg; a receiver swap receives it.</param>
    /// <param name="fixedNominal">The fixed leg's nominals, one per coupon.</param>
    /// <param name="floatingNominal">The floating leg's nominals, one per coupon.</param>
    /// <param name="fixedSchedule">The fixed leg's schedule.</param>
    /// <param name="fixedRate">The fixed rates, one per coupon.</param>
    /// <param name="fixedDayCount">The fixed leg's day counter.</param>
    /// <param name="floatingSchedule">The floating leg's schedule.</param>
    /// <param name="iborIndex">The floating leg's index; an overnight index builds compounded overnight
    /// coupons.</param>
    /// <param name="gearing">The floating coupons' gearing.</param>
    /// <param name="spread">The floating coupons' spread.</param>
    /// <param name="floatingDayCount">The floating leg's day counter; the index's when <see langword="null"/>,
    /// QuantLib's empty day counter.</param>
    /// <param name="context">The evaluation context, whose at-par switch the Ibor leg reads.</param>
    /// <param name="intermediateCapitalExchange">Whether the nominals' changes are exchanged.</param>
    /// <param name="finalCapitalExchange">Whether the final nominals are exchanged.</param>
    /// <param name="paymentConvention">The payment convention; the floating schedule's when
    /// <see langword="null"/>.</param>
    /// <param name="paymentLag">The payment lag, in business days.</param>
    /// <param name="paymentCalendar">The payment calendar; each leg's schedule's when <see langword="null"/>,
    /// QuantLib's empty calendar.</param>
    /// <exception cref="ArgumentNullException">A sequence, a schedule, a day counter but the floating one, the index
    /// or the context is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The type or the payment convention isn't a member of its type.
    /// </exception>
    /// <exception cref="ArgumentException">A sequence's size doesn't match its leg's coupons.</exception>
    public NonstandardSwap(
        SwapType type,
        IEnumerable<double> fixedNominal,
        IEnumerable<double> floatingNominal,
        Schedule fixedSchedule,
        IEnumerable<double> fixedRate,
        DayCounter fixedDayCount,
        Schedule floatingSchedule,
        IborIndex iborIndex,
        double gearing,
        double spread,
        DayCounter? floatingDayCount,
        EvaluationContext context,
        bool intermediateCapitalExchange = false,
        bool finalCapitalExchange = false,
        BusinessDayConvention? paymentConvention = null,
        int paymentLag = 0,
        Calendar? paymentCalendar = null)
        : this(Terms.OfConstants(
            type,
            fixedNominal,
            floatingNominal,
            fixedSchedule,
            fixedRate,
            fixedDayCount,
            floatingSchedule,
            iborIndex,
            gearing,
            spread,
            floatingDayCount,
            context,
            intermediateCapitalExchange,
            finalCapitalExchange,
            paymentConvention,
            paymentLag,
            paymentCalendar))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NonstandardSwap"/> class with a gearing and a spread per
    /// coupon.</summary>
    /// <param name="type">A payer swap pays the fixed leg; a receiver swap receives it.</param>
    /// <param name="fixedNominal">The fixed leg's nominals, one per coupon.</param>
    /// <param name="floatingNominal">The floating leg's nominals, one per coupon.</param>
    /// <param name="fixedSchedule">The fixed leg's schedule.</param>
    /// <param name="fixedRate">The fixed rates, one per coupon.</param>
    /// <param name="fixedDayCount">The fixed leg's day counter.</param>
    /// <param name="floatingSchedule">The floating leg's schedule.</param>
    /// <param name="iborIndex">The floating leg's index; an overnight index builds compounded overnight
    /// coupons.</param>
    /// <param name="gearing">The floating coupons' gearings, one per coupon.</param>
    /// <param name="spread">The floating coupons' spreads, one per coupon.</param>
    /// <param name="floatingDayCount">The floating leg's day counter; the index's when <see langword="null"/>,
    /// QuantLib's empty day counter.</param>
    /// <param name="context">The evaluation context, whose at-par switch the Ibor leg reads.</param>
    /// <param name="intermediateCapitalExchange">Whether the nominals' changes are exchanged.</param>
    /// <param name="finalCapitalExchange">Whether the final nominals are exchanged.</param>
    /// <param name="paymentConvention">The payment convention; the floating schedule's when
    /// <see langword="null"/>.</param>
    /// <param name="paymentLag">The payment lag, in business days.</param>
    /// <param name="paymentCalendar">The payment calendar; each leg's schedule's when <see langword="null"/>,
    /// QuantLib's empty calendar.</param>
    /// <exception cref="ArgumentNullException">A sequence, a schedule, a day counter but the floating one, the index
    /// or the context is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The type or the payment convention isn't a member of its type.
    /// </exception>
    /// <exception cref="ArgumentException">A sequence's size doesn't match its leg's coupons.</exception>
    public NonstandardSwap(
        SwapType type,
        IEnumerable<double> fixedNominal,
        IEnumerable<double> floatingNominal,
        Schedule fixedSchedule,
        IEnumerable<double> fixedRate,
        DayCounter fixedDayCount,
        Schedule floatingSchedule,
        IborIndex iborIndex,
        IEnumerable<double> gearing,
        IEnumerable<double> spread,
        DayCounter? floatingDayCount,
        EvaluationContext context,
        bool intermediateCapitalExchange = false,
        bool finalCapitalExchange = false,
        BusinessDayConvention? paymentConvention = null,
        int paymentLag = 0,
        Calendar? paymentCalendar = null)
        : this(Terms.OfSequences(
            type,
            fixedNominal,
            floatingNominal,
            fixedSchedule,
            fixedRate,
            fixedDayCount,
            floatingSchedule,
            iborIndex,
            gearing,
            spread,
            floatingDayCount,
            context,
            intermediateCapitalExchange,
            finalCapitalExchange,
            paymentConvention,
            paymentLag,
            paymentCalendar))
    {
    }

    private NonstandardSwap(Terms terms)
        : base([terms.FixedLeg, terms.FloatingLeg], [terms.Type == SwapType.Payer, terms.Type == SwapType.Receiver])
    {
        Type = terms.Type;
        FixedNominal = terms.FixedNominal;
        FloatingNominal = terms.FloatingNominal;
        FixedSchedule = terms.FixedSchedule;
        FixedRate = terms.FixedRate;
        FixedDayCount = terms.FixedDayCount;
        FloatingSchedule = terms.FloatingSchedule;
        IborIndex = terms.IborIndex;
        Spreads = terms.Spreads;
        Gearings = terms.Gearings;
        singleSpreadAndGearing = terms.SingleSpreadAndGearing;
        FloatingDayCount = terms.FloatingDayCount;
        PaymentConvention = terms.PaymentConvention;
        PaymentLag = terms.PaymentLag;
        PaymentCalendar = terms.PaymentCalendar;
    }

    /// <summary>Gets the swap's type: a payer swap pays the fixed leg.</summary>
    public SwapType Type { get; }

    /// <summary>Gets the fixed leg's nominals, one per cash flow, a redemption's beside it.</summary>
    public ImmutableArray<double> FixedNominal { get; }

    /// <summary>Gets the floating leg's nominals, one per cash flow, a redemption's beside it.</summary>
    public ImmutableArray<double> FloatingNominal { get; }

    /// <summary>Gets the fixed leg's schedule.</summary>
    public Schedule FixedSchedule { get; }

    /// <summary>Gets the fixed rates, one per cash flow, a redemption's 0.</summary>
    public ImmutableArray<double> FixedRate { get; }

    /// <summary>Gets the fixed leg's day counter.</summary>
    public DayCounter FixedDayCount { get; }

    /// <summary>Gets the floating leg's schedule.</summary>
    public Schedule FloatingSchedule { get; }

    /// <summary>Gets the floating leg's index.</summary>
    public IborIndex IborIndex { get; }

    /// <summary>Gets the floating coupons' spreads.</summary>
    public ImmutableArray<double> Spreads { get; }

    /// <summary>Gets the floating coupons' gearings, a zero one as machine epsilon.</summary>
    public ImmutableArray<double> Gearings { get; }

    /// <summary>Gets the floating leg's day counter; <see langword="null"/> for the index's.</summary>
    public DayCounter? FloatingDayCount { get; }

    /// <summary>Gets the payment convention.</summary>
    public BusinessDayConvention PaymentConvention { get; }

    /// <summary>Gets the payment lag, in business days.</summary>
    public int PaymentLag { get; }

    /// <summary>Gets the payment calendar; <see langword="null"/> for each leg's schedule's.</summary>
    public Calendar? PaymentCalendar { get; }

    /// <summary>Gets the fixed leg.</summary>
    public ImmutableArray<CashFlow> FixedLeg => Legs[0];

    /// <summary>Gets the floating leg.</summary>
    public ImmutableArray<CashFlow> FloatingLeg => Legs[1];

    /// <summary>The floating coupons' spread, when one was given.</summary>
    /// <returns>The spread.</returns>
    /// <exception cref="NanoQLException">A spread per coupon was given.</exception>
    public double Spread()
    {
        if (!singleSpreadAndGearing)
        {
            throw new NanoQLException("spread is a vector, use spreads inspector instead");
        }

        return Spreads[0];
    }

    /// <summary>The floating coupons' gearing, when one was given.</summary>
    /// <returns>The gearing.</returns>
    /// <exception cref="NanoQLException">A gearing per coupon was given.</exception>
    public double Gearing()
    {
        if (!singleSpreadAndGearing)
        {
            throw new NanoQLException("gearing is a vector, use gearings inspector instead");
        }

        return Gearings[0];
    }

    // QuantLib's constructors and init, run before the swap's own constructor takes the legs.
    private sealed class Terms
    {
        public required SwapType Type { get; init; }

        public required ImmutableArray<double> FixedNominal { get; init; }

        public required ImmutableArray<double> FloatingNominal { get; init; }

        public required Schedule FixedSchedule { get; init; }

        public required ImmutableArray<double> FixedRate { get; init; }

        public required DayCounter FixedDayCount { get; init; }

        public required Schedule FloatingSchedule { get; init; }

        public required IborIndex IborIndex { get; init; }

        public required ImmutableArray<double> Spreads { get; init; }

        public required ImmutableArray<double> Gearings { get; init; }

        public required bool SingleSpreadAndGearing { get; init; }

        public required DayCounter? FloatingDayCount { get; init; }

        public required BusinessDayConvention PaymentConvention { get; init; }

        public required int PaymentLag { get; init; }

        public required Calendar? PaymentCalendar { get; init; }

        public required ImmutableArray<CashFlow> FixedLeg { get; init; }

        public required ImmutableArray<CashFlow> FloatingLeg { get; init; }

        public static Terms OfVanilla(FixedVsFloatingSwap fromVanilla, EvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(fromVanilla);
            ArgumentNullException.ThrowIfNull(context);
            int fixedCount = fromVanilla.FixedLeg.Length, floatingCount = fromVanilla.FloatingLeg.Length;
            double nominal = fromVanilla.Nominal();
            OvernightConventions conventions = fromVanilla is OvernightIndexedSwap ois
                ? new OvernightConventions(ois.TelescopicValueDates, ois.AveragingMethod, ois.LookbackDays, ois.LockoutDays, ois.ApplyObservationShift, ois.RoundingPrecision)
                : OvernightConventions.Default;
            return Of(
                fromVanilla.Type,
                [.. Enumerable.Repeat(nominal, fixedCount)],
                [.. Enumerable.Repeat(nominal, floatingCount)],
                fromVanilla.FixedSchedule,
                [.. Enumerable.Repeat(fromVanilla.FixedRate, fixedCount)],
                fromVanilla.FixedDayCount,
                fromVanilla.FloatingSchedule,
                fromVanilla.IborIndex,
                [.. Enumerable.Repeat(1.0, floatingCount)],
                [.. Enumerable.Repeat(fromVanilla.Spread, floatingCount)],
                true,
                fromVanilla.FloatingDayCount,
                context,
                false,
                false,
                fromVanilla.PaymentConvention,
                fromVanilla.PaymentLag,
                fromVanilla.PaymentCalendar,
                conventions);
        }

        public static Terms OfConstants(
            SwapType type,
            IEnumerable<double> fixedNominal,
            IEnumerable<double> floatingNominal,
            Schedule fixedSchedule,
            IEnumerable<double> fixedRate,
            DayCounter fixedDayCount,
            Schedule floatingSchedule,
            IborIndex iborIndex,
            double gearing,
            double spread,
            DayCounter? floatingDayCount,
            EvaluationContext context,
            bool intermediateCapitalExchange,
            bool finalCapitalExchange,
            BusinessDayConvention? paymentConvention,
            int paymentLag,
            Calendar? paymentCalendar)
        {
            ArgumentNullException.ThrowIfNull(floatingNominal);
            List<double> floating = [.. floatingNominal];
            return OfEntry(
                type,
                fixedNominal,
                floating,
                fixedSchedule,
                fixedRate,
                fixedDayCount,
                floatingSchedule,
                iborIndex,
                [.. Enumerable.Repeat(gearing, floating.Count)],
                [.. Enumerable.Repeat(spread, floating.Count)],
                true,
                floatingDayCount,
                context,
                intermediateCapitalExchange,
                finalCapitalExchange,
                paymentConvention,
                paymentLag,
                paymentCalendar);
        }

        public static Terms OfSequences(
            SwapType type,
            IEnumerable<double> fixedNominal,
            IEnumerable<double> floatingNominal,
            Schedule fixedSchedule,
            IEnumerable<double> fixedRate,
            DayCounter fixedDayCount,
            Schedule floatingSchedule,
            IborIndex iborIndex,
            IEnumerable<double> gearing,
            IEnumerable<double> spread,
            DayCounter? floatingDayCount,
            EvaluationContext context,
            bool intermediateCapitalExchange,
            bool finalCapitalExchange,
            BusinessDayConvention? paymentConvention,
            int paymentLag,
            Calendar? paymentCalendar)
        {
            ArgumentNullException.ThrowIfNull(floatingNominal);
            ArgumentNullException.ThrowIfNull(gearing);
            ArgumentNullException.ThrowIfNull(spread);
            return OfEntry(
                type,
                fixedNominal,
                [.. floatingNominal],
                fixedSchedule,
                fixedRate,
                fixedDayCount,
                floatingSchedule,
                iborIndex,
                [.. gearing],
                [.. spread],
                false,
                floatingDayCount,
                context,
                intermediateCapitalExchange,
                finalCapitalExchange,
                paymentConvention,
                paymentLag,
                paymentCalendar);
        }

        // QuantLib refuses an unknown type after building the legs (nonstandardswap.cpp:262-273); a value outside the
        // enum is refused on entry, as the enum row has it, with the other arguments' null checks.
        private static Terms OfEntry(
            SwapType type,
            IEnumerable<double> fixedNominal,
            List<double> floatingNominal,
            Schedule fixedSchedule,
            IEnumerable<double> fixedRate,
            DayCounter fixedDayCount,
            Schedule floatingSchedule,
            IborIndex iborIndex,
            List<double> gearing,
            List<double> spread,
            bool singleSpreadAndGearing,
            DayCounter? floatingDayCount,
            EvaluationContext context,
            bool intermediateCapitalExchange,
            bool finalCapitalExchange,
            BusinessDayConvention? paymentConvention,
            int paymentLag,
            Calendar? paymentCalendar)
        {
            if (type is not (SwapType.Payer or SwapType.Receiver))
            {
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown nonstandard-swap type");
            }

            ArgumentNullException.ThrowIfNull(fixedNominal);
            ArgumentNullException.ThrowIfNull(fixedSchedule);
            ArgumentNullException.ThrowIfNull(fixedRate);
            ArgumentNullException.ThrowIfNull(fixedDayCount);
            ArgumentNullException.ThrowIfNull(floatingSchedule);
            ArgumentNullException.ThrowIfNull(iborIndex);
            ArgumentNullException.ThrowIfNull(context);
            BusinessDayConvention convention = paymentConvention ?? floatingSchedule.BusinessDayConvention;
            EnumArguments.Check(convention, nameof(paymentConvention));
            return Of(
                type,
                [.. fixedNominal],
                floatingNominal,
                fixedSchedule,
                [.. fixedRate],
                fixedDayCount,
                floatingSchedule,
                iborIndex,
                gearing,
                spread,
                singleSpreadAndGearing,
                floatingDayCount,
                context,
                intermediateCapitalExchange,
                finalCapitalExchange,
                convention,
                paymentLag,
                paymentCalendar,
                OvernightConventions.Default);
        }

        // QuantLib's init (nonstandardswap.cpp:144-274).
        private static Terms Of(
            SwapType type,
            List<double> fixedNominal,
            List<double> floatingNominal,
            Schedule fixedSchedule,
            List<double> fixedRate,
            DayCounter fixedDayCount,
            Schedule floatingSchedule,
            IborIndex iborIndex,
            List<double> gearing,
            List<double> spread,
            bool singleSpreadAndGearing,
            DayCounter? floatingDayCount,
            EvaluationContext context,
            bool intermediateCapitalExchange,
            bool finalCapitalExchange,
            BusinessDayConvention paymentConvention,
            int paymentLag,
            Calendar? paymentCalendar,
            OvernightConventions conventions)
        {
            if (!(fixedNominal.Count == fixedRate.Count))
            {
                throw new ArgumentException(
                    $"Fixed nominal size ({fixedNominal.Count}) does not match fixed rate size ({fixedRate.Count})",
                    nameof(fixedRate));
            }

            if (!(fixedNominal.Count == fixedSchedule.Size - 1))
            {
                throw new ArgumentException(
                    $"Fixed nominal size ({fixedNominal.Count}) does not match schedule size ({fixedSchedule.Size}) - 1",
                    nameof(fixedNominal));
            }

            if (!(floatingNominal.Count == floatingSchedule.Size - 1))
            {
                throw new ArgumentException(
                    $"Floating nominal size ({floatingNominal.Count}) does not match schedule size ({floatingSchedule.Size}) - 1",
                    nameof(floatingNominal));
            }

            if (!(floatingNominal.Count == spread.Count))
            {
                throw new ArgumentException(
                    $"Floating nominal size ({floatingNominal.Count}) does not match spread size ({spread.Count})",
                    nameof(spread));
            }

            if (!(floatingNominal.Count == gearing.Count))
            {
                throw new ArgumentException(
                    $"Floating nominal size ({floatingNominal.Count}) does not match gearing size ({gearing.Count})",
                    nameof(gearing));
            }

            // if the gearing is zero then the ibor leg will be set up with fixed
            // coupons which makes trouble here in this context. We therefore use
            // a dirty trick and enforce the gearing to be non zero.
            for (int i = 0; i < gearing.Count; i++)
            {
                if (ComparisonFunctions.Close(gearing[i], 0.0))
                {
                    gearing[i] = NumericLimits.QL_EPSILON;
                }
            }

            var fixedLeg = new List<CashFlow>(new FixedRateLeg(fixedSchedule)
                .WithNotionals(fixedNominal)
                .WithCouponRates(fixedRate, fixedDayCount)
                .WithPaymentAdjustment(paymentConvention)
                .WithPaymentLag(paymentLag)
                .WithPaymentCalendar(paymentCalendar ?? fixedSchedule.Calendar)
                .ToLeg());

            List<CashFlow> floatingLeg;
            if (iborIndex is OvernightIndex overnightIndex)
            {
                // Only compounded averaging is supported; OvernightLeg uses it by
                // default.
                OvernightLeg leg = new OvernightLeg(floatingSchedule, overnightIndex)
                    .WithNotionals(floatingNominal)
                    .WithPaymentDayCounter(floatingDayCount)
                    .WithPaymentAdjustment(paymentConvention)
                    .WithPaymentLag(paymentLag)
                    .WithPaymentCalendar(paymentCalendar)
                    .WithSpreads(spread)
                    .WithGearings(gearing)
                    .WithTelescopicValueDates(conventions.TelescopicValueDates)
                    .WithAveragingMethod(conventions.AveragingMethod)
                    .WithLookbackDays(conventions.LookbackDays)
                    .WithLockoutDays(conventions.LockoutDays)
                    .WithObservationShift(conventions.ApplyObservationShift);

                // the source swap's rounding, which QuantLib's conversion drops
                if (conventions.RoundingPrecision is int precision)
                {
                    leg = leg.WithRoundingPrecision(precision);
                }

                floatingLeg = [.. leg.ToLeg()];
            }
            else
            {
                floatingLeg = [.. new IborLeg(floatingSchedule, iborIndex)
                    .WithNotionals(floatingNominal)
                    .WithPaymentDayCounter(floatingDayCount)
                    .WithPaymentAdjustment(paymentConvention)
                    .WithPaymentLag(paymentLag)
                    .WithPaymentCalendar(paymentCalendar)
                    .WithSpreads(spread)
                    .WithGearings(gearing)
                    .ToLeg(context)];
            }

            if (intermediateCapitalExchange)
            {
                for (int i = 0; i < fixedLeg.Count - 1; i++)
                {
                    double cap = fixedNominal[i] - fixedNominal[i + 1];
                    if (!ComparisonFunctions.Close(cap, 0.0))
                    {
                        fixedLeg.Insert(i + 1, new Redemption(cap, fixedLeg[i].Date));
                        fixedNominal.Insert(i + 1, fixedNominal[i]);
                        fixedRate.Insert(i + 1, 0.0);
                        i++;
                    }
                }

                for (int i = 0; i < floatingLeg.Count - 1; i++)
                {
                    double cap = floatingNominal[i] - floatingNominal[i + 1];
                    if (!ComparisonFunctions.Close(cap, 0.0))
                    {
                        floatingLeg.Insert(i + 1, new Redemption(cap, floatingLeg[i].Date));
                        floatingNominal.Insert(i + 1, floatingNominal[i]);
                        i++;
                    }
                }
            }

            if (finalCapitalExchange)
            {
                fixedLeg.Add(new Redemption(fixedNominal[^1], fixedLeg[^1].Date));
                fixedNominal.Add(fixedNominal[^1]);
                fixedRate.Add(0.0);
                floatingLeg.Add(new Redemption(floatingNominal[^1], floatingLeg[^1].Date));
                floatingNominal.Add(floatingNominal[^1]);
            }

            return new Terms
            {
                Type = type,
                FixedNominal = [.. fixedNominal],
                FloatingNominal = [.. floatingNominal],
                FixedSchedule = fixedSchedule,
                FixedRate = [.. fixedRate],
                FixedDayCount = fixedDayCount,
                FloatingSchedule = floatingSchedule,
                IborIndex = iborIndex,
                Spreads = [.. spread],
                Gearings = [.. gearing],
                SingleSpreadAndGearing = singleSpreadAndGearing,
                FloatingDayCount = floatingDayCount,
                PaymentConvention = paymentConvention,
                PaymentLag = paymentLag,
                PaymentCalendar = paymentCalendar,
                FixedLeg = [.. fixedLeg],
                FloatingLeg = [.. floatingLeg],
            };
        }
    }

    // An overnight-indexed swap's conventions, which the conversion keeps; QuantLib's defaults otherwise.
    private sealed record OvernightConventions(
        bool TelescopicValueDates,
        RateAveragingType AveragingMethod,
        int? LookbackDays,
        int LockoutDays,
        bool ApplyObservationShift,
        int? RoundingPrecision)
    {
        public static readonly OvernightConventions Default = new(false, RateAveragingType.Compound, null, 0, false, null);
    }
}
