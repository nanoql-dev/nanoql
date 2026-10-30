// SPDX-FileCopyrightText: Copyright (C) 2026 Zain Mughal
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/makemultipleresetsswap.hpp ql/instruments/makemultipleresetsswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A helper building multiple-resets swaps: QuantLib's <c>MakeMultipleResetsSwap</c>.</summary>
/// <remarks>
/// An immutable builder (the S-cms plan's CMD8): each method returns a modified copy, and <see cref="ToSwap"/> builds
/// the swap under a context, in place of QuantLib's conversion operators. By default the fixed leg's frequency is the
/// coupons' period, the index's tenor times the resets per coupon. With no fixed rate given, the swap is built at its
/// par rate, priced by the engine set, or by <see cref="DiscountingSwapEngine"/> on the index's forwarding curve; the
/// engine is never stored on the swap, where QuantLib's builder attaches it. A builder has no value equality.
/// </remarks>
public sealed record MakeMultipleResetsSwap
{
    /// <summary>Initializes a new instance of the <see cref="MakeMultipleResetsSwap"/> record.</summary>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="iborIndex">The index whose fixings the floating coupons combine.</param>
    /// <param name="resetsPerCoupon">The number of resets per floating coupon.</param>
    /// <exception cref="ArgumentNullException"><paramref name="iborIndex"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="resetsPerCoupon"/> isn't positive (the S-cms
    /// plan's CMR5 (xiv)).</exception>
    public MakeMultipleResetsSwap(Period tenor, IborIndex iborIndex, int resetsPerCoupon)
    {
        ArgumentNullException.ThrowIfNull(iborIndex);
        if (!(resetsPerCoupon > 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resetsPerCoupon), resetsPerCoupon, "a coupon needs at least one reset, so resetsPerCoupon must be positive");
        }

        Provider = iborIndex.Provider;
        Tenor = tenor;
        IborIndex = iborIndex;
        ResetsPerCoupon = resetsPerCoupon;
        FixedDayCount = iborIndex.DayCounter;
    }

    // The provider the builder takes its conventions from and builds inside: its index's.
    private ConventionProvider Provider { get; init; }

    private Period Tenor { get; init; }

    private IborIndex IborIndex { get; init; }

    private int ResetsPerCoupon { get; init; }

    private double? FixedRate { get; init; }

    private Period ForwardStart { get; init; }

    private int? SettlementDays { get; init; }

    private Date? EffectiveDate { get; init; }

    private Date? TerminationDate { get; init; }

    private SwapType Type { get; init; } = SwapType.Payer;

    private double Nominal { get; init; } = 1.0;

    private Frequency FixedFrequency { get; init; } = Frequency.NoFrequency;

    private DayCounter FixedDayCount { get; init; }

    private BusinessDayConvention FixedConvention { get; init; } = BusinessDayConvention.ModifiedFollowing;

    private double Spread { get; init; }

    private RateAveragingType AveragingMethod { get; init; } = RateAveragingType.Compound;

    private Func<FixedVsFloatingSwap, EvaluationContext, FixedVsFloatingSwapResults>? Engine { get; init; }

    /// <summary>Sets whether the fixed rate is received.</summary>
    /// <param name="flag">Whether it is.</param>
    /// <returns>The modified builder.</returns>
    public MakeMultipleResetsSwap ReceiveFixed(bool flag = true) => this with { Type = flag ? SwapType.Receiver : SwapType.Payer };

    /// <summary>Sets whether the fixed rate is paid or received.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The type isn't a member of the enum.</exception>
    public MakeMultipleResetsSwap WithType(SwapType type)
    {
        EnumArguments.Check(type, nameof(type));
        return this with { Type = type };
    }

    /// <summary>Sets the nominal.</summary>
    /// <param name="n">The nominal.</param>
    /// <returns>The modified builder.</returns>
    public MakeMultipleResetsSwap WithNominal(double n) => this with { Nominal = n };

    /// <summary>Sets the fixed rate; with none the swap is built at its par rate.</summary>
    /// <param name="fixedRate">The rate, or <see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c>.</param>
    /// <returns>The modified builder.</returns>
    public MakeMultipleResetsSwap WithFixedRate(double? fixedRate) => this with { FixedRate = fixedRate };

    /// <summary>Sets the settlement days from the evaluation date to the start, the index's fixing days by
    /// default.</summary>
    /// <param name="settlementDays">The days, or <see langword="null"/> for the default.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The days are negative.</exception>
    public MakeMultipleResetsSwap WithSettlementDays(int? settlementDays)
    {
        if (settlementDays is int d)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(d, nameof(settlementDays));
        }

        return this with { SettlementDays = settlementDays };
    }

    /// <summary>Sets the start date, in place of the settlement days.</summary>
    /// <param name="d">The date, or <see langword="null"/>, QuantLib's <c>Date()</c>, for none.</param>
    /// <returns>The modified builder.</returns>
    public MakeMultipleResetsSwap WithEffectiveDate(Date? d) => this with { EffectiveDate = d };

    /// <summary>Sets the end date, in place of the tenor.</summary>
    /// <param name="d">The date, or <see langword="null"/>, QuantLib's <c>Date()</c>, for none.</param>
    /// <returns>The modified builder.</returns>
    public MakeMultipleResetsSwap WithTerminationDate(Date? d) =>
        d is null ? this with { TerminationDate = d } : this with { TerminationDate = d, Tenor = default };

    /// <summary>Sets the forward start from the spot date.</summary>
    /// <param name="fwdStart">The forward start.</param>
    /// <returns>The modified builder.</returns>
    public MakeMultipleResetsSwap WithForwardStart(Period fwdStart) => this with { ForwardStart = fwdStart };

    /// <summary>Sets the fixed leg's frequency, the coupons' period by default.</summary>
    /// <param name="f">The frequency, or <see cref="Frequency.NoFrequency"/> for the default.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The frequency isn't a member of the enum.</exception>
    public MakeMultipleResetsSwap WithFixedLegFrequency(Frequency f)
    {
        EnumArguments.Check(f, nameof(f));
        return this with { FixedFrequency = f };
    }

    /// <summary>Sets the fixed leg's day counter, the index's by default.</summary>
    /// <param name="dc">The day counter.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dc"/> is null.</exception>
    public MakeMultipleResetsSwap WithFixedLegDayCount(DayCounter dc)
    {
        ArgumentNullException.ThrowIfNull(dc);
        return this with { FixedDayCount = dc };
    }

    /// <summary>Sets the fixed leg's convention.</summary>
    /// <param name="bdc">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MakeMultipleResetsSwap WithFixedLegConvention(BusinessDayConvention bdc)
    {
        EnumArguments.Check(bdc, nameof(bdc));
        return this with { FixedConvention = bdc };
    }

    /// <summary>Sets the spread added to each floating fixing.</summary>
    /// <param name="sp">The spread.</param>
    /// <returns>The modified builder.</returns>
    public MakeMultipleResetsSwap WithFloatingLegSpread(double sp) => this with { Spread = sp };

    /// <summary>Sets how the floating fixings combine.</summary>
    /// <param name="m">The method.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The method isn't a member of the enum.</exception>
    public MakeMultipleResetsSwap WithAveragingMethod(RateAveragingType m)
    {
        EnumArguments.Check(m, nameof(m));
        return this with { AveragingMethod = m };
    }

    /// <summary>Sets <see cref="DiscountingSwapEngine"/> on a discount curve, without the settlement date's flows, as
    /// the engine that computes the par rate when no fixed rate is given.</summary>
    /// <param name="d">The discount curve, or <see langword="null"/>, QuantLib's empty handle: the engine then refuses
    /// to price, so a build at the par rate is refused.</param>
    /// <returns>The modified builder.</returns>
    public MakeMultipleResetsSwap WithDiscountingTermStructure(YieldTermStructure? d) =>
        this with
        {
            Engine = (swap, context) => d is null
                ? DiscountingSwapEngine.CalculateWithoutCurve(swap, context)
                : DiscountingSwapEngine.Calculate(swap, context, d, includeSettlementDateFlows: false),
        };

    /// <summary>Sets the engine that computes the par rate when no fixed rate is given; it is never stored on the
    /// swap.</summary>
    /// <param name="engine">The engine, or <see langword="null"/> for the default: a
    /// <see cref="DiscountingSwapEngine"/> on the index's forwarding curve.</param>
    /// <returns>The modified builder.</returns>
    public MakeMultipleResetsSwap WithPricingEngine(
        Func<FixedVsFloatingSwap, EvaluationContext, FixedVsFloatingSwapResults>? engine) =>
        this with { Engine = engine };

    /// <summary>Builds the swap.</summary>
    /// <param name="context">The evaluation context, whose evaluation date the start is counted from.</param>
    /// <returns>The swap.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">Both a start date and settlement days were given; or, with no fixed rate
    /// given, the index has no forwarding curve and no engine was set, or the engine gives no fair rate; each with
    /// QuantLib's text.</exception>
    public MultipleResetsSwap ToSwap(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Built inside the builder's provider, so what the build constructs takes its conventions.
        using IDisposable scope = ConventionProvider.Use(Provider);

        Calendar cal = IborIndex.FixingCalendar;
        BusinessDayConvention bdc = IborIndex.BusinessDayConvention;
        if (!(EffectiveDate is null || SettlementDays is null))
        {
            throw new NanoQLException("withEffectiveDate and withSettlementDays are mutually exclusive");
        }

        Date startDate;
        if (EffectiveDate is Date effective)
        {
            startDate = effective;
        }
        else
        {
            int settlDays = SettlementDays ?? IborIndex.FixingDays;
            Date refDate = context.EvaluationDate;
            startDate = cal.Advance(cal.Adjust(refDate), settlDays, TimeUnit.Days);
            startDate = cal.Advance(
                startDate, ForwardStart, ForwardStart.Length < 0 ? BusinessDayConvention.Preceding : BusinessDayConvention.Following);
        }

        // The end date is left unadjusted; the schedules adjust it with their
        // termination-date convention.  Generating backwards from an adjusted
        // date would shift all the other dates and add a stub at the start
        // whenever the maturity falls on a holiday.
        Date endDate = TerminationDate ?? startDate + Tenor;

        Period resetTenor = IborIndex.Tenor;
        Frequency fixedFreq = FixedFrequency;
        if (fixedFreq == Frequency.NoFrequency)
        {
            var couponTenor = new Period(ResetsPerCoupon * resetTenor.Length, resetTenor.Units);
            fixedFreq = couponTenor.Frequency();
        }

        var fixedSchedule = new Schedule(
            startDate, endDate, new Period(fixedFreq), cal, FixedConvention, FixedConvention, DateGenerationRule.Backward, false, context);
        var fullResetSchedule = new Schedule(
            startDate, endDate, resetTenor, cal, bdc, bdc, DateGenerationRule.Backward, false, context);

        double usedFixedRate;
        if (FixedRate is double given)
        {
            usedFixedRate = given;
        }
        else
        {
            var temp = new MultipleResetsSwap(
                Type, Nominal, fixedSchedule, 0.0, FixedDayCount, fullResetSchedule, IborIndex, ResetsPerCoupon, Spread, AveragingMethod);
            FixedVsFloatingSwapResults results;
            if (Engine is null)
            {
                YieldTermStructure disc = IborIndex.ForwardingTermStructure
                    ?? throw new NanoQLException($"null term structure set to this instance of {IborIndex.Name}");
                results = DiscountingSwapEngine.Calculate((FixedVsFloatingSwap)temp, context, disc, includeSettlementDateFlows: false);
            }
            else
            {
                results = Engine(temp, context) ?? throw new NanoQLException("the pricing engine returned no results");
            }

            usedFixedRate = results.FairRate ?? throw new NanoQLException("result not available");
        }

        return new MultipleResetsSwap(
            Type, Nominal, fixedSchedule, usedFixedRate, FixedDayCount, fullResetSchedule, IborIndex, ResetsPerCoupon, Spread, AveragingMethod);
    }
}
