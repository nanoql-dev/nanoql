// SPDX-FileCopyrightText: Copyright (C) 2021 Marcin Rybacki
// SPDX-FileCopyrightText: Copyright (C) 2025 Uzair Beg
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/termstructures/crosscurrencyratehelpers.hpp ql/experimental/termstructures/crosscurrencyratehelpers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>QuantLib's anonymous-namespace functions of the cross-currency rate helpers.</summary>
[Experimental("NQL0001")]
internal static class CrossCurrencyRateHelpersFunctions
{
    /// <summary>The fixed rate the fixed-vs-floating helper's swap is built at.</summary>
    internal const double SampleFixedRate = 0.01;

    private const double BasisPoint = 1.0e-4;

    /// <summary>
    /// An explicitly passed <see cref="Frequency.NoFrequency"/> read as an unset payment frequency: before these
    /// parameters became optional, NoFrequency was the sentinel meaning "derive the schedule from the index tenor".
    /// </summary>
    /// <param name="frequency">The frequency.</param>
    /// <returns>The frequency, or <see langword="null"/> for none.</returns>
    internal static Frequency? NormalizedPaymentFrequency(Frequency? frequency) =>
        frequency == Frequency.NoFrequency ? null : frequency;

    /// <summary>A leg's schedule: from the settlement date, the fixing days after the adjusted evaluation date, to the
    /// tenor after it, backwards at the frequency.</summary>
    /// <param name="evaluationDate">The evaluation date.</param>
    /// <param name="tenor">The instrument's tenor.</param>
    /// <param name="frequency">The coupon frequency, as a period.</param>
    /// <param name="fixingDays">The settlement days.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="convention">The business-day convention.</param>
    /// <param name="endOfMonth">Whether the schedule follows the end-of-month rule.</param>
    /// <returns>The schedule.</returns>
    /// <exception cref="ArgumentException">The tenor is shorter than the coupon frequency.</exception>
    internal static Schedule LegSchedule(
        Date evaluationDate,
        Period tenor,
        Period frequency,
        int fixingDays,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth)
    {
        if (!(tenor >= frequency))
        {
            throw new ArgumentException(
                "XCCY instrument tenor should not be smaller than coupon frequency.", nameof(tenor));
        }

        Date referenceDate = calendar.Adjust(evaluationDate);
        Date earliestDate = calendar.Advance(referenceDate, fixingDays, TimeUnit.Days, convention);
        Date maturity = earliestDate + tenor;
        return new MakeSchedule()
            .From(earliestDate)
            .To(maturity)
            .WithTenor(frequency)
            .WithCalendar(calendar)
            .WithConvention(convention)
            .EndOfMonth(endOfMonth)
            .Backwards()
            .ToSchedule();
    }

    /// <summary>A floating leg's schedule, at the payment frequency, or the index's tenor when none is given.</summary>
    /// <param name="evaluationDate">The evaluation date.</param>
    /// <param name="tenor">The instrument's tenor.</param>
    /// <param name="fixingDays">The settlement days.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="convention">The business-day convention.</param>
    /// <param name="endOfMonth">Whether the schedule follows the end-of-month rule.</param>
    /// <param name="index">The leg's index.</param>
    /// <param name="paymentFrequency">The payment frequency, or <see langword="null"/> for the index's tenor.</param>
    /// <returns>The schedule.</returns>
    /// <exception cref="ArgumentException">An overnight index is given no payment frequency, or the tenor is shorter
    /// than the coupon frequency.</exception>
    internal static Schedule FloatingLegSchedule(
        Date evaluationDate,
        Period tenor,
        int fixingDays,
        Calendar calendar,
        BusinessDayConvention convention,
        bool endOfMonth,
        IborIndex index,
        Frequency? paymentFrequency)
    {
        Period freqPeriod;
        if (paymentFrequency is not Frequency f)
        {
            if (index is OvernightIndex)
            {
                throw new ArgumentException(
                    "Require payment frequency for overnight indices.", nameof(paymentFrequency));
            }

            freqPeriod = index.Tenor;
        }
        else
        {
            freqPeriod = new Period(f);
        }

        return LegSchedule(evaluationDate, tenor, freqPeriod, fixingDays, calendar, convention, endOfMonth);
    }

    /// <summary>A floating leg on a unit notional: an overnight leg on an overnight index, an Ibor leg
    /// otherwise.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="index">The index.</param>
    /// <param name="paymentLag">The payment lag, in days.</param>
    /// <param name="useIndexedCoupons">Whether Ibor coupons are indexed or at par; the context's at-par switch when
    /// <see langword="null"/>.</param>
    /// <param name="stubIndexSelection">The Ibor leg's stub-index selection.</param>
    /// <param name="context">The evaluation context, whose at-par switch an Ibor leg reads.</param>
    /// <returns>The leg.</returns>
    internal static ImmutableArray<CashFlow> BuildFloatingLeg(
        Schedule schedule,
        IborIndex index,
        int paymentLag,
        bool? useIndexedCoupons,
        StubIndexSelection stubIndexSelection,
        EvaluationContext context) =>
        index is OvernightIndex overnightIndex
            ? new OvernightLeg(schedule, overnightIndex)
                .WithNotionals(1.0)
                .WithPaymentLag(paymentLag)
                .ToLeg()
            : new IborLeg(schedule, index)
                .WithNotionals(1.0)
                .WithPaymentLag(paymentLag)
                .WithIndexedCoupons(useIndexedCoupons)
                .WithStubIndexSelection(stubIndexSelection)
                .ToLeg(context);

    /// <summary>
    /// A constant-notional leg's NPV, its notional exchanges at the start and maturity included, and its BPS per basis
    /// point, at the discount curve's reference date with the cash flows on that date counted.
    /// </summary>
    /// <param name="leg">The leg, on a unit notional.</param>
    /// <param name="initialNotionalExchangeDate">The date the notional is paid.</param>
    /// <param name="finalNotionalExchangeDate">The date the notional is received back.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The NPV and the BPS.</returns>
    internal static (double Npv, double Bps) NpvBpsConstNotionalLeg(
        ImmutableArray<CashFlow> leg,
        Date initialNotionalExchangeDate,
        Date finalNotionalExchangeDate,
        YieldTermStructure discountCurve,
        EvaluationContext context)
    {
        Date refDt = discountCurve.ReferenceDate;
        const bool includeSettleDtFlows = true;
        (double npv, double bps) = CashFlows.NpvBps(leg, discountCurve, context, includeSettleDtFlows, refDt, refDt);

        // Include NPV of the notional exchange at start and maturity.
        npv += -1.0 * discountCurve.Discount(initialNotionalExchangeDate);
        npv += discountCurve.Discount(finalNotionalExchangeDate);
        bps /= BasisPoint;
        return (npv, bps);
    }
}
