// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2015 Maddalena Zanzi
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Lungwitz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/ratehelpers.hpp ql/termstructures/yield/ratehelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A rate helper for bootstrapping over BMA swap rates: the Libor fraction of the BMA swap, built at construction, that
/// the curve prices at par.
/// </summary>
/// <remarks>
/// <para>
/// The helper builds its swap when it is constructed, a payer of BMA on a nominal of 100 with an arbitrary Libor
/// fraction of 0.75, from the settlement date for the tenor, so it reads the context's evaluation date and its at-par
/// switch then. Its dates are the earliest date and the latest date, the BMA value date of the first Wednesday after
/// the swap's maturity; the maturity, latest relevant and pillar dates fall back on the latest date.
/// </para>
/// <para>
/// Only the BMA leg prices against the curve the helper is bound to: its coupons are copied on a BMA index forecasting
/// on that curve. The Libor leg forecasts on the Libor index's own curve, and the swap is discounted on it, as in
/// QuantLib; without that curve the implied quote refuses "discounting term structure handle is empty".
/// </para>
/// </remarks>
public sealed class BMASwapRateHelper : RelativeDateRateHelper
{
    private const double BasisPoint = 1.0e-4;

    private readonly Period tenor;
    private readonly int settlementDays;
    private readonly Calendar calendar;
    private readonly Period bmaPeriod;
    private readonly BusinessDayConvention bmaConvention;
    private readonly DayCounter? bmaDayCount;
    private readonly BMAIndex bmaIndex;
    private readonly IborIndex iborIndex;

    // Written by the constructor, and on a fresh copy only: the swap rebuilt when the helper is re-dated, and its BMA
    // coupons copied on the bound curve.
    private BMASwap swap;

    /// <summary>Initializes a new instance of the <see cref="BMASwapRateHelper"/> class.</summary>
    /// <param name="liborFraction">The quoted Libor fraction.</param>
    /// <param name="tenor">The swap's maturity, from the settlement date.</param>
    /// <param name="settlementDays">The settlement days, counted from the evaluation date on the calendar.</param>
    /// <param name="calendar">The calendar of the settlement and the maturity.</param>
    /// <param name="bmaPeriod">The BMA leg's tenor.</param>
    /// <param name="bmaConvention">The BMA leg's business-day convention.</param>
    /// <param name="bmaDayCount">The BMA leg's day counter; the BMA index's when <see langword="null"/>, QuantLib's
    /// empty day counter.</param>
    /// <param name="bmaIndex">The BMA index, whose calendar the BMA leg's schedule takes.</param>
    /// <param name="index">The Libor leg's index, which forecasts and discounts the swap on its own curve.</param>
    /// <param name="context">The evaluation context, read when the swap is built.</param>
    /// <exception cref="ArgumentNullException">An argument but the BMA day counter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="settlementDays"/> is negative, or <paramref name="bmaConvention"/> isn't a member of its type.
    /// </exception>
    /// <exception cref="ArgumentException">The swap's schedules or legs can't be built from the arguments.</exception>
    /// <exception cref="NanoQLException">The swap's schedules or legs can't be built from the arguments.</exception>
    public BMASwapRateHelper(
        double liborFraction,
        Period tenor,
        int settlementDays,
        Calendar calendar,
        Period bmaPeriod,
        BusinessDayConvention bmaConvention,
        DayCounter? bmaDayCount,
        BMAIndex bmaIndex,
        IborIndex index,
        EvaluationContext context)
        : base(liborFraction, context)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);

        // QuantLib uses the calendar unchecked, and every use of its empty one fails.
        ArgumentNullException.ThrowIfNull(calendar);
        EnumArguments.Check(bmaConvention, nameof(bmaConvention));

        // QuantLib dereferences both indexes before any check, which is undefined for a null index.
        ArgumentNullException.ThrowIfNull(bmaIndex);
        ArgumentNullException.ThrowIfNull(index);
        this.tenor = tenor;
        this.settlementDays = settlementDays;
        this.calendar = calendar;
        this.bmaPeriod = bmaPeriod;
        this.bmaConvention = bmaConvention;
        this.bmaDayCount = bmaDayCount;
        this.bmaIndex = bmaIndex;
        iborIndex = index;
        swap = BuildSwap(context);
    }

    /// <summary>Gets the swap the helper prices: QuantLib's <c>swap()</c>.</summary>
    public BMASwap Swap => swap;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// The Libor index has no forwarding curve, or the swap's results aren't available, as for an expired swap.
    /// </exception>
    public override double ImpliedQuote(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireTermStructure();
        YieldTermStructure? discount = iborIndex.ForwardingTermStructure;
        BMASwapResults results = discount is null
            ? DiscountingSwapEngine.CalculateWithoutCurve(swap, context)
            : DiscountingSwapEngine.Calculate(swap, context, discount);

        // QuantLib's fairLiborFraction(), its refusals in its order
        if (results.LiborLegBPS is null || results.LiborLegNPV is null)
        {
            throw new NanoQLException("result not available");
        }

        double spreadNPV = (swap.LiborSpread / BasisPoint) * results.LiborLegBPS.Value;
        if (results.LiborLegNPV.Value - spreadNPV == 0.0)
        {
            throw new NanoQLException("result not available (null libor NPV)");
        }

        return results.FairLiborFraction ?? throw new NanoQLException("result not available");
    }

    /// <inheritdoc/>
    protected internal override BootstrapHelper<YieldTermStructure> WithTermStructure(YieldTermStructure t)
    {
        var copy = (BMASwapRateHelper)base.WithTermStructure(t);

        // built in the provider of the helper's BMA index
        using (ConventionProvider.Use(bmaIndex.Provider))
        {
            copy.swap = WithBmaIndex(swap, new BMAIndex(t));
        }

        return copy;
    }

    /// <inheritdoc/>
    protected override void InitializeDates(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        swap = BuildSwap(context);
    }

    // The swap with its BMA coupons copied on another index: what QuantLib's relinked handle gives the coupons of the
    // swap it built. A coupon's fixing schedule depends on the index's calendar only, which the copy keeps.
    private static BMASwap WithBmaIndex(BMASwap swap, BMAIndex index)
    {
        ImmutableArray<CashFlow> bma = swap.BmaLeg;
        var leg = ImmutableArray.CreateBuilder<CashFlow>(bma.Length);
        foreach (CashFlow cf in bma)
        {
            var c = (AverageBMACoupon)cf;
            leg.Add(new AverageBMACoupon(
                c.Date,
                c.ContractNominal,
                c.AccrualStartDate,
                c.AccrualEndDate,
                index,
                c.Gearing,
                c.Spread,
                c.ReferencePeriodStart,
                c.ReferencePeriodEnd,
                c.DayCounter));
        }

        return (BMASwap)swap.WithLeg(1, leg.MoveToImmutable());
    }

    // initializeDates' swap and dates.
    private BMASwap BuildSwap(EvaluationContext context)
    {
        // settlement days are counted from the actual evaluation date,
        // even when it is not a business day (see issue #753)
        Date earliest = calendar.Advance(
            context.EvaluationDate, settlementDays, TimeUnit.Days, BusinessDayConvention.Following);
        earliestDate = earliest;

        Date maturity = earliest + tenor;

        // dummy BMA index with curve/swap arguments, built in the provider of the helper's BMA index
        BMAIndex clonedIndex;
        using (ConventionProvider.Use(bmaIndex.Provider))
        {
            clonedIndex = new BMAIndex(TermStructure);
        }

        Schedule bmaSchedule = new MakeSchedule()
            .From(earliest)
            .To(maturity)
            .WithTenor(bmaPeriod)
            .WithCalendar(bmaIndex.FixingCalendar)
            .WithConvention(bmaConvention)
            .Backwards()
            .ToSchedule();

        Schedule liborSchedule = new MakeSchedule()
            .From(earliest)
            .To(maturity)
            .WithTenor(iborIndex.Tenor)
            .WithCalendar(iborIndex.FixingCalendar)
            .WithConvention(iborIndex.BusinessDayConvention)
            .EndOfMonth(iborIndex.EndOfMonth)
            .Backwards()
            .ToSchedule();

        var built = new BMASwap(
            SwapType.Payer,
            100.0,
            liborSchedule,
            0.75, // arbitrary
            0.0,
            iborIndex,
            iborIndex.DayCounter,
            bmaSchedule,
            clonedIndex,
            bmaDayCount,
            context);

        Date d = calendar.Adjust(built.MaturityDate(), BusinessDayConvention.Following);
        int w = (int)d.Weekday;
        Date nextWednesday = w >= 4 ? d + (11 - w) : d + (4 - w);
        latestDate = clonedIndex.ValueDate(clonedIndex.FixingCalendar.Adjust(nextWednesday));
        return built;
    }
}
