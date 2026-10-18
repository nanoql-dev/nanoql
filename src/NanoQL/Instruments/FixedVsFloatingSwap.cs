// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/fixedvsfloatingswap.hpp ql/instruments/fixedvsfloatingswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A fixed-rate vs floating-rate swap.</summary>
/// <remarks>
/// <para>
/// If no payment convention is passed, the convention of the floating-rate schedule is used.
/// </para>
/// <para>
/// Abstract, as QuantLib's is, though it has no abstract member: QuantLib's <c>setupFloatingArguments</c> fills its
/// engines' arguments, and nanoql's engines read the legs. A derived swap passes QuantLib's arguments and the builder
/// of its floating leg to the protected constructor, which checks its terms and builds the fixed leg first and then
/// calls the builder, in QuantLib's order: QuantLib's base constructor runs before the derived constructor's body
/// builds the floating leg. The fixed leg is the first leg and the floating leg the second. An engine's results are a
/// <see cref="FixedVsFloatingSwapResults"/>, whose fair rate and spread <see cref="FetchResults"/> derives from the
/// legs' BPS when the engine gives none.
/// </para>
/// <para>
/// When the context's <see cref="EvaluationContext.IncludeReferenceDateEvents"/> is <see langword="true"/>, payments
/// occurring at the settlement date of the swap might be included in the NPV and therefore affect the fair-rate and
/// fair-spread calculation. This might not be what you want.
/// </para>
/// </remarks>
public abstract class FixedVsFloatingSwap : Swap
{
    private const double BasisPoint = 1.0e-4;

    // These tell us if we can support the methods Nominal() and Nominals(). There might be false negatives (if we pass
    // constant vectors of different lengths as fixed and floating nominals), but whoever passes two vectors is mostly
    // going to read them instead.
    private readonly bool constantNominals;
    private readonly bool sameNominals;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedVsFloatingSwap"/> class with QuantLib's arguments and the
    /// builder of the derived swap's floating leg.
    /// </summary>
    /// <param name="type">Whether the fixed rate is paid or received.</param>
    /// <param name="fixedNominals">The fixed coupons' nominals; the last one repeats past the end.</param>
    /// <param name="fixedSchedule">The fixed leg's schedule.</param>
    /// <param name="fixedRate">The fixed rate.</param>
    /// <param name="fixedDayCount">The fixed leg's day counter; the index's when <see langword="null"/>.</param>
    /// <param name="floatingNominals">The floating coupons' nominals.</param>
    /// <param name="floatingSchedule">The floating leg's schedule.</param>
    /// <param name="iborIndex">The floating leg's index.</param>
    /// <param name="spread">The floating leg's spread.</param>
    /// <param name="floatingDayCount">The floating leg's day counter, or <see langword="null"/> for none.</param>
    /// <param name="paymentConvention">
    /// The payment convention; the floating schedule's convention when <see langword="null"/>.
    /// </param>
    /// <param name="paymentLag">The fixed leg's payment lag, in business days.</param>
    /// <param name="paymentCalendar">
    /// The fixed leg's payment calendar; the fixed schedule's calendar when <see langword="null"/>.
    /// </param>
    /// <param name="floatingLeg">
    /// Builds the derived swap's floating leg; called once the terms are checked and the fixed leg is built, as
    /// QuantLib's derived constructors build it in their bodies, so a refusal of the terms comes before the floating
    /// leg's.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument but a day counter or the calendar is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The type or the payment convention isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The floating leg is <see langword="default"/> or holds a null cash flow.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The fixed leg can't be built from the arguments, an empty fixed schedule among them.
    /// </exception>
    protected FixedVsFloatingSwap(
        SwapType type,
        IEnumerable<double> fixedNominals,
        Schedule fixedSchedule,
        double fixedRate,
        DayCounter? fixedDayCount,
        IEnumerable<double> floatingNominals,
        Schedule floatingSchedule,
        IborIndex iborIndex,
        double spread,
        DayCounter? floatingDayCount,
        BusinessDayConvention? paymentConvention,
        int paymentLag,
        Calendar? paymentCalendar,
        Func<ImmutableArray<CashFlow>> floatingLeg)
        : this(
            type,
            fixedNominals,
            fixedSchedule,
            fixedRate,
            fixedDayCount,
            floatingNominals,
            floatingSchedule,
            iborIndex,
            spread,
            floatingDayCount,
            paymentConvention,
            paymentLag,
            paymentCalendar,
            floatingLeg,
            nameof(fixedSchedule),
            nameof(paymentConvention))
    {
    }

    // The protected constructor, given the names the derived swap's constructor gives the fixed schedule and the
    // payment convention, so that the base's refusal of either names that constructor's own parameter.
    private protected FixedVsFloatingSwap(
        SwapType type,
        IEnumerable<double> fixedNominals,
        Schedule fixedSchedule,
        double fixedRate,
        DayCounter? fixedDayCount,
        IEnumerable<double> floatingNominals,
        Schedule floatingSchedule,
        IborIndex iborIndex,
        double spread,
        DayCounter? floatingDayCount,
        BusinessDayConvention? paymentConvention,
        int paymentLag,
        Calendar? paymentCalendar,
        Func<ImmutableArray<CashFlow>> floatingLeg,
        string fixedScheduleName,
        string paymentConventionName)
        : this(
            Terms.Of(
                type,
                fixedNominals,
                fixedSchedule,
                fixedRate,
                fixedDayCount,
                floatingNominals,
                floatingSchedule,
                iborIndex,
                paymentConvention,
                paymentLag,
                paymentCalendar,
                fixedScheduleName,
                paymentConventionName),
            spread,
            floatingDayCount,
            floatingLeg)
    {
    }

    // The constructor above checks the terms and builds the fixed leg before it chains here; the floating leg is built
    // after them, as the derived constructor's body builds it in QuantLib.
    private FixedVsFloatingSwap(
        Terms terms, double spread, DayCounter? floatingDayCount, Func<ImmutableArray<CashFlow>> floatingLeg)
        : base(
            [terms.FixedLeg, BuildFloatingLeg(floatingLeg)],
            [terms.Type == SwapType.Payer, terms.Type == SwapType.Receiver])
    {
        Type = terms.Type;
        FixedNominals = terms.FixedNominals;
        FixedSchedule = terms.FixedSchedule;
        FixedRate = terms.FixedRate;
        FixedDayCount = terms.FixedDayCount;
        FloatingNominals = terms.FloatingNominals;
        FloatingSchedule = terms.FloatingSchedule;
        IborIndex = terms.IborIndex;
        Spread = spread;
        FloatingDayCount = floatingDayCount;
        PaymentConvention = terms.PaymentConvention;
        PaymentLag = terms.PaymentLag;
        PaymentCalendar = terms.PaymentCalendar;

        sameNominals = SameNominals(FixedNominals, FloatingNominals);
        if (!sameNominals)
        {
            constantNominals = false;
        }
        else
        {
            constantNominals = true;
            double front = FixedNominals[0];
            foreach (double x in FixedNominals)
            {
                if (x != front)
                {
                    constantNominals = false;
                    break;
                }
            }
        }
    }

    /// <summary>Gets whether the fixed rate is paid or received.</summary>
    public SwapType Type { get; }

    /// <summary>Gets the fixed coupons' nominals, as given.</summary>
    public ImmutableArray<double> FixedNominals { get; }

    /// <summary>Gets the fixed leg's schedule.</summary>
    public Schedule FixedSchedule { get; }

    /// <summary>Gets the fixed rate.</summary>
    public double FixedRate { get; }

    /// <summary>Gets the fixed leg's day counter: the index's when none was given.</summary>
    public DayCounter FixedDayCount { get; }

    /// <summary>Gets the floating coupons' nominals, as given.</summary>
    public ImmutableArray<double> FloatingNominals { get; }

    /// <summary>Gets the floating leg's schedule.</summary>
    public Schedule FloatingSchedule { get; }

    /// <summary>Gets the floating leg's index.</summary>
    public IborIndex IborIndex { get; }

    /// <summary>Gets the floating leg's spread.</summary>
    public double Spread { get; }

    /// <summary>Gets the floating leg's day counter, or <see langword="null"/> when none was given.</summary>
    public DayCounter? FloatingDayCount { get; }

    /// <summary>Gets the payment convention: the floating schedule's when none was given.</summary>
    public BusinessDayConvention PaymentConvention { get; }

    /// <summary>Gets the fixed leg's payment lag, in business days.</summary>
    public int PaymentLag { get; }

    /// <summary>Gets the payment calendar as given, or <see langword="null"/> when none was.</summary>
    public Calendar? PaymentCalendar { get; }

    /// <summary>Gets the fixed leg.</summary>
    public ImmutableArray<CashFlow> FixedLeg => Legs[0];

    /// <summary>Gets the floating leg.</summary>
    public ImmutableArray<CashFlow> FloatingLeg => Legs[1];

    /// <summary>The nominal, when it is constant across the coupons of both legs.</summary>
    /// <returns>The nominal.</returns>
    /// <exception cref="NanoQLException">The nominal is not constant across coupons.</exception>
    public double Nominal()
    {
        if (!constantNominals)
        {
            throw new NanoQLException("nominal is not constant");
        }

        return FixedNominals[0];
    }

    /// <summary>The nominals, when they are the same for the two legs.</summary>
    /// <returns>The nominals.</returns>
    /// <exception cref="NanoQLException">The nominals are not the same for the two legs.</exception>
    public ImmutableArray<double> Nominals()
    {
        if (!sameNominals)
        {
            throw new NanoQLException("different nominals on fixed and floating leg");
        }

        return FixedNominals;
    }

    /// <inheritdoc/>
    /// <remarks>An expired swap has zero leg BPS, and neither a fair rate nor a fair spread.</remarks>
    public override FixedVsFloatingSwapResults SetupExpired()
    {
        // The swap's expired results already hold zero leg BPS.
        return new FixedVsFloatingSwapResults(base.SetupExpired())
        {
            FairRate = null,
            FairSpread = null,
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The fair rate and spread are the engine's when it gives them; otherwise each is derived from the NPV and its
    /// leg's BPS, and is <see langword="null"/> when that BPS is zero or unknown.
    /// </remarks>
    public override FixedVsFloatingSwapResults FetchResults(InstrumentResults results)
    {
        SwapResults r = base.FetchResults(results);

        // might be a swap engine's, so no error is thrown
        var fetched = r as FixedVsFloatingSwapResults;
        double? fairRate = fetched?.FairRate;
        double? fairSpread = fetched?.FairSpread;

        if (fairRate is null)
        {
            // calculate it from other results
            if (r.LegBPS[0] is double bps && bps != 0.0)
            {
                fairRate = FixedRate - (r.NPV / (bps / BasisPoint));
            }
        }

        if (fairSpread is null)
        {
            // ditto
            if (r.LegBPS[1] is double bps && bps != 0.0)
            {
                fairSpread = Spread - (r.NPV / (bps / BasisPoint));
            }
        }

        return fetched is not null
            ? fetched with { FairRate = fairRate, FairSpread = fairSpread }
            : new FixedVsFloatingSwapResults(r) { FairRate = fairRate, FairSpread = fairSpread };
    }

    /// <summary>The payment convention the swap uses: the one given, or the floating schedule's.</summary>
    /// <param name="paymentConvention">The convention given, or <see langword="null"/>.</param>
    /// <param name="floatingSchedule">The floating leg's schedule.</param>
    /// <param name="paramName">The name of the swap constructor's parameter that takes the convention.</param>
    /// <returns>The convention.</returns>
    private protected static BusinessDayConvention ResolvePaymentConvention(
        BusinessDayConvention? paymentConvention,
        Schedule floatingSchedule,
        string paramName)
    {
        if (paymentConvention is BusinessDayConvention c)
        {
            EnumArguments.Check(c, paramName);
            return c;
        }

        return floatingSchedule.BusinessDayConvention;
    }

    /// <summary>Refuses a null floating index with QuantLib's text, as the constructor does.</summary>
    /// <param name="index">The index.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <exception cref="ArgumentNullException">The index is <see langword="null"/>.</exception>
    private protected static void CheckFloatingIndex(IborIndex? index, string paramName)
    {
        if (index is null)
        {
            throw new ArgumentNullException(paramName, "null floating index provided");
        }
    }

    private static ImmutableArray<CashFlow> BuildFloatingLeg(Func<ImmutableArray<CashFlow>> floatingLeg)
    {
        ArgumentNullException.ThrowIfNull(floatingLeg);
        return CheckLeg(floatingLeg(), nameof(floatingLeg));
    }

    // std::equal over two ranges: the same length and each pair equal under ==.
    private static bool SameNominals(ImmutableArray<double> x, ImmutableArray<double> y)
    {
        if (x.Length != y.Length)
        {
            return false;
        }

        for (int i = 0; i < x.Length; ++i)
        {
            if (x[i] != y[i])
            {
                return false;
            }
        }

        return true;
    }

    // The constructor's arguments, checked and resolved as QuantLib's constructor resolves them, with the fixed leg
    // built from them before the swap's own constructor takes the legs.
    private sealed class Terms
    {
        public required SwapType Type { get; init; }

        public required ImmutableArray<double> FixedNominals { get; init; }

        public required Schedule FixedSchedule { get; init; }

        public required double FixedRate { get; init; }

        public required DayCounter FixedDayCount { get; init; }

        public required ImmutableArray<double> FloatingNominals { get; init; }

        public required Schedule FloatingSchedule { get; init; }

        public required IborIndex IborIndex { get; init; }

        public required BusinessDayConvention PaymentConvention { get; init; }

        public required int PaymentLag { get; init; }

        public required Calendar? PaymentCalendar { get; init; }

        public required ImmutableArray<CashFlow> FixedLeg { get; init; }

        public static Terms Of(
            SwapType type,
            IEnumerable<double> fixedNominals,
            Schedule fixedSchedule,
            double fixedRate,
            DayCounter? fixedDayCount,
            IEnumerable<double> floatingNominals,
            Schedule floatingSchedule,
            IborIndex iborIndex,
            BusinessDayConvention? paymentConvention,
            int paymentLag,
            Calendar? paymentCalendar,
            string fixedScheduleName,
            string paymentConventionName)
        {
            ArgumentNullException.ThrowIfNull(fixedNominals);
            ArgumentNullException.ThrowIfNull(fixedSchedule, fixedScheduleName);
            ArgumentNullException.ThrowIfNull(floatingNominals);
            ArgumentNullException.ThrowIfNull(floatingSchedule);
            CheckFloatingIndex(iborIndex, nameof(iborIndex));
            EnumArguments.Check(type, nameof(type));

            DayCounter fixedDc = fixedDayCount ?? iborIndex.DayCounter;
            BusinessDayConvention convention =
                ResolvePaymentConvention(paymentConvention, floatingSchedule, paymentConventionName);
            ImmutableArray<double> fixedNotionals = [.. fixedNominals];

            // An empty fixed schedule is refused by the fixed leg's conversion, after its notional check, as QuantLib's
            // leg reaches it.
            ImmutableArray<CashFlow> fixedLeg = new FixedRateLeg(fixedSchedule)
                .WithNotionals(fixedNotionals)
                .WithCouponRates(fixedRate, fixedDc)
                .WithPaymentAdjustment(convention)
                .WithPaymentLag(paymentLag)
                .WithPaymentCalendar(paymentCalendar ?? fixedSchedule.Calendar)
                .ToLeg();

            return new Terms
            {
                Type = type,
                FixedNominals = fixedNotionals,
                FixedSchedule = fixedSchedule,
                FixedRate = fixedRate,
                FixedDayCount = fixedDc,
                FloatingNominals = [.. floatingNominals],
                FloatingSchedule = floatingSchedule,
                IborIndex = iborIndex,
                PaymentConvention = convention,
                PaymentLag = paymentLag,
                PaymentCalendar = paymentCalendar,
                FixedLeg = fixedLeg,
            };
        }
    }
}
