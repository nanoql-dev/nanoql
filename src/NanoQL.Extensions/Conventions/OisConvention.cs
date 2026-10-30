// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Extensions.Conventions.ConventionRoutes;

namespace NanoQL.Extensions.Conventions;

/// <summary>
/// An overnight-indexed swap convention resolved in a set (<see cref="ConventionSet.Ois"/>): its record, its base
/// chain merged, and the builder and rate helper it feeds, on its overnight index built in the set.
/// </summary>
/// <remarks>
/// An option the record leaves out is the builder's: the set's <c>MakeOIS.</c> record for the index's class, or its
/// <c>MakeOIS</c> record, read through the index's provider when the swap is built, or the index's own conventions.
/// The rate helper takes one convention for both legs and their termination dates, one rule and one end-of-month flag,
/// and counts the settlement days on the overnight leg's calendar: a record whose values for them differ is refused.
/// </remarks>
public sealed class OisConvention
{
    private const string Helper = nameof(OISRateHelper);

    internal OisConvention(ConventionSet set, OisConventionRecord record)
    {
        Set = set;
        Record = record;
    }

    /// <summary>Gets the set the convention was resolved in, whose names its record's fields are.</summary>
    public ConventionSet Set { get; }

    /// <summary>Gets the record with every field its base chain gives, under the convention's name.</summary>
    public OisConventionRecord Record { get; }

    /// <summary>An OIS builder on the record's overnight index built in the set, with every option the record gives
    /// set.</summary>
    /// <param name="swapTenor">The swap's tenor.</param>
    /// <param name="forwardingCurve">The index's forwarding curve, or <see langword="null"/> for none.</param>
    /// <returns>The builder; it takes its other options' defaults from the set when it builds.</returns>
    /// <exception cref="ArgumentException">The record gives no index, or no overnight index, or a name it gives is
    /// unknown.</exception>
    public MakeOIS Builder(Period swapTenor, YieldTermStructure? forwardingCurve = null)
    {
        OisConventionRecord r = Record;
        var b = new MakeOIS(swapTenor, Index(forwardingCurve));
        b = r.SettlementDays is int settlementDays ? b.WithSettlementDays(settlementDays) : b;
        b = r.SettlementCalendar is string settlementCalendar ? b.WithSettlementCalendar(Set.Calendar(settlementCalendar)) : b;
        b = r.PaymentLag is int lag ? b.WithPaymentLag(lag) : b;
        b = r.PaymentConvention is BusinessDayConvention payment ? b.WithPaymentAdjustment(payment) : b;
        b = r.PaymentFrequency is Frequency frequency ? b.WithPaymentFrequency(frequency) : b;
        b = r.PaymentCalendar is string paymentCalendar ? b.WithPaymentCalendar(Set.Calendar(paymentCalendar)) : b;
        b = r.FixedLegFrequency is Frequency fixedFrequency ? b.WithFixedLegPaymentFrequency(fixedFrequency) : b;
        b = r.FixedLegCalendar is string fixedCalendar ? b.WithFixedLegCalendar(Set.Calendar(fixedCalendar)) : b;
        b = r.FixedLegConvention is BusinessDayConvention fixedConvention ? b.WithFixedLegConvention(fixedConvention) : b;
        b = r.FixedLegTerminationDateConvention is BusinessDayConvention fixedTermination
            ? b.WithFixedLegTerminationDateConvention(fixedTermination)
            : b;
        b = r.FixedLegRule is DateGenerationRule fixedRule ? b.WithFixedLegRule(fixedRule) : b;
        b = r.FixedLegEndOfMonth is bool fixedEndOfMonth ? b.WithFixedLegEndOfMonth(fixedEndOfMonth) : b;
        b = r.FixedLegDayCounter is string fixedDayCounter ? b.WithFixedLegDayCount(Set.DayCounter(fixedDayCounter)) : b;
        b = r.OvernightLegCalendar is string overnightCalendar ? b.WithOvernightLegCalendar(Set.Calendar(overnightCalendar)) : b;
        b = r.OvernightLegConvention is BusinessDayConvention overnightConvention ? b.WithOvernightLegConvention(overnightConvention) : b;
        b = r.OvernightLegTerminationDateConvention is BusinessDayConvention overnightTermination
            ? b.WithOvernightLegTerminationDateConvention(overnightTermination)
            : b;
        b = r.OvernightLegRule is DateGenerationRule overnightRule ? b.WithOvernightLegRule(overnightRule) : b;
        b = r.OvernightLegEndOfMonth is bool overnightEndOfMonth ? b.WithOvernightLegEndOfMonth(overnightEndOfMonth) : b;
        b = r.TelescopicValueDates is bool telescopic ? b.WithTelescopicValueDates(telescopic) : b;
        b = r.Averaging is RateAveragingType averaging ? b.WithAveragingMethod(averaging) : b;
        b = r.LookbackDays is int lookback ? b.WithLookbackDays(lookback) : b;
        b = r.LockoutDays is int lockout ? b.WithLockoutDays(lockout) : b;
        return r.ObservationShift is bool shift ? b.WithObservationShift(shift) : b;
    }

    /// <summary>An OIS rate helper on the record's overnight index built in the set.</summary>
    /// <param name="rate">The swap rate quoted.</param>
    /// <param name="tenor">The swap's tenor.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="discountingCurve">The exogenous discount curve, or <see langword="null"/> for the bootstrapped
    /// curve.</param>
    /// <param name="forwardingCurve">The index's forwarding curve, or <see langword="null"/> for the bootstrapped
    /// curve.</param>
    /// <param name="forwardStart">The forward start.</param>
    /// <returns>The helper.</returns>
    /// <exception cref="ArgumentException">The record gives no index, or no overnight index, a name it gives is unknown,
    /// or it gives a value the helper can't take.</exception>
    public OISRateHelper RateHelper(
        double rate,
        Period tenor,
        EvaluationContext context,
        YieldTermStructure? discountingCurve = null,
        YieldTermStructure? forwardingCurve = null,
        Period forwardStart = default)
    {
        OisConventionRecord r = Record;
        OvernightIndex index = Index(forwardingCurve);
        OisDefaults d = MakeOIS.Defaults(index, Set);

        // the helper's one convention, rule and end of month, each taken where the record's agree
        BusinessDayConvention fixedConvention = r.FixedLegConvention ?? d.FixedLegConvention;
        BusinessDayConvention convention = Tied(
            Helper,
            r,
            nameof(r.FixedLegConvention),
            Tied(Helper, r, nameof(r.FixedLegConvention), fixedConvention, nameof(r.FixedLegTerminationDateConvention), r.FixedLegTerminationDateConvention ?? d.FixedLegTerminationDateConvention),
            nameof(r.OvernightLegConvention),
            Tied(
                Helper,
                r,
                nameof(r.OvernightLegConvention),
                r.OvernightLegConvention ?? d.OvernightLegConvention,
                nameof(r.OvernightLegTerminationDateConvention),
                r.OvernightLegTerminationDateConvention ?? d.OvernightLegTerminationDateConvention));
        DateGenerationRule rule = Tied(
            Helper, r, nameof(r.FixedLegRule), r.FixedLegRule ?? d.FixedLegRule, nameof(r.OvernightLegRule), r.OvernightLegRule ?? d.OvernightLegRule);

        // an end-of-month flag given drops the start date's default for both legs, as the builder's setters do
        bool? endOfMonth = r.FixedLegEndOfMonth is null && r.OvernightLegEndOfMonth is null
            ? null
            : Tied(Helper, r, nameof(r.FixedLegEndOfMonth), r.FixedLegEndOfMonth ?? false, nameof(r.OvernightLegEndOfMonth), r.OvernightLegEndOfMonth ?? false);

        // the settlement days are counted on the overnight leg's calendar
        Calendar overnightCalendar = CalendarOf(Set, r.OvernightLegCalendar) ?? index.FixingCalendar;
        Untaken(Helper, r, nameof(r.SettlementCalendar), CalendarOf(Set, r.SettlementCalendar), overnightCalendar);

        Frequency paymentFrequency = r.PaymentFrequency ?? d.OvernightLegPaymentFrequency;
        return new OISRateHelper(
            r.SettlementDays ?? d.SettlementDays,
            tenor,
            rate,
            index,
            context,
            discountingCurve,
            r.TelescopicValueDates ?? d.TelescopicValueDates,
            r.PaymentLag ?? d.PaymentLag,
            r.PaymentConvention ?? d.PaymentAdjustment,
            paymentFrequency,
            CalendarOf(Set, r.PaymentCalendar),
            forwardStart,
            averagingMethod: r.Averaging ?? d.AveragingMethod,
            endOfMonth: endOfMonth,
            fixedPaymentFrequency: r.FixedLegFrequency ?? (r.PaymentFrequency is null ? d.FixedLegPaymentFrequency : paymentFrequency),
            fixedCalendar: CalendarOf(Set, r.FixedLegCalendar),
            lookbackDays: r.LookbackDays,
            lockoutDays: r.LockoutDays ?? d.LockoutDays,
            applyObservationShift: r.ObservationShift ?? d.ApplyObservationShift,
            rule: rule,
            overnightCalendar: CalendarOf(Set, r.OvernightLegCalendar),
            convention: convention,
            fixedDayCount: DayCounterOf(Set, r.FixedLegDayCounter));
    }

    // Every name the record gives resolves.
    internal void Validate()
    {
        OisConventionRecord r = Record;
        if (r.Index is not null)
        {
            _ = Index(null);
        }

        foreach (string? calendar in new[] { r.SettlementCalendar, r.PaymentCalendar, r.FixedLegCalendar, r.OvernightLegCalendar })
        {
            _ = CalendarOf(Set, calendar);
        }

        _ = DayCounterOf(Set, r.FixedLegDayCounter);
    }

    private OvernightIndex Index(YieldTermStructure? h) => Set.OvernightIndex(Required(Record, nameof(Record.Index), Record.Index), h);
}
